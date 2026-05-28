#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Runtime;
using Rhizomode.Graph.Serialization;
using Rhizomode.Persistence.Contracts;
using Rhizomode.SharedKernel;
using Rhizomode.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// グラフのセーブ/ロード facade。I/O は <see cref="IGraphRepository"/>、Deserialize は
    /// <see cref="HydrationPlanExecutor"/> に委譲。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 7: 旧版は File.WriteAllText / JsonUtility / GraphState.Deserialize を直接持つ
    /// god-helper だったが、本 Phase で I/O と deserialize 責務を完全分離。
    /// SaveLoadManager は ファイル名解決 + OnSaved/OnLoaded イベント発火のみを担う薄い facade。
    /// </remarks>
    public class GraphSaveLoadManager : MonoBehaviour
    {
        private const string FileExtension = ".json";
        private const string DefaultPrefix = "live_set_";

        private GraphContextBehaviour? _graphContext;
        private IGraphRepository? _repository;
        private GraphHydrator? _hydrator;
        private HydrationPlanExecutor? _executor;
        private INodeFactory? _factory;
        private ISavePathProvider? _pathProvider;
        private ICameraStatePersistence? _cameraPersistence;
        private INodeVisualRotationProvider? _rotationProvider;
        private readonly Dictionary<string, Quaternion> _lastLoadedRotations = new();

        /// <summary>
        /// 直近 <see cref="LoadGraph"/> で読み込んだ NodeData.rotation を id 経由で取得する map。
        /// </summary>
        /// <remarks>
        /// cue 復元時の表裏破綻 fix: <c>GraphLoadCoordinator.Rebuild</c> 直前に subscriber
        /// (<c>GraphSaveLoadBootstrapWiring.OnLoaded</c>) が読み出し、Rebuild に渡して visual の
        /// 回転を保存値で復元する。rotation 未保存 (旧形式 cue) の node は dict に含まれない。
        /// </remarks>
        public IReadOnlyDictionary<string, Quaternion> LastLoadedRotations => _lastLoadedRotations;

        /// <summary>グラフ保存完了時に発火するイベント。引数はファイル名。</summary>
        public event Action<string>? OnGraphSaved;

        /// <summary>
        /// グラフ読み込み開始時 (state.Clear() / hydrator.Build / executor.Execute の直前) に発火する。
        /// </summary>
        /// <remarks>
        /// Phase 8 Codex review fix #1+#3: 旧来 OnGraphLoaded 内で <c>ModuleLifecycleProcessor.CleanupAll</c>
        /// を呼んでいたが、その時点では既に Executor が新 module を attach 済 → 新 module まで破棄される
        /// バグがあった。本イベントを load 開始時に発火させることで CleanupAll を正しく "旧 module の破棄"
        /// にできる。
        /// </remarks>
        public event Action? OnGraphLoading;

        /// <summary>グラフ読み込み完了時に発火するイベント。</summary>
        public event Action? OnGraphLoaded;

        /// <summary>セーブディレクトリの絶対パス (Configure 後に有効)。</summary>
        public string SaveDirectoryPath => _pathProvider?.SaveDirectoryPath ?? "";

        /// <summary>GraphContext を注入する初期化 (legacy 互換用)。</summary>
        public void Initialize(GraphContextBehaviour graphContext)
        {
            _graphContext = graphContext;
        }

        /// <summary>
        /// Phase 7: Persistence + Serialization の依存を注入する。GameBootstrap から呼ばれる。
        /// </summary>
        public void Configure(
            IGraphRepository repository,
            GraphHydrator hydrator,
            HydrationPlanExecutor executor,
            INodeFactory factory,
            ISavePathProvider pathProvider)
        {
            _repository = repository;
            _hydrator = hydrator;
            _executor = executor;
            _factory = factory;
            _pathProvider = pathProvider;
        }

        /// <summary>
        /// カメラ状態の永続化サービスを注入する (Phase 4)。GraphSaveLoadBootstrapWiring から呼ぶ。
        /// 未注入でもセーブ/ロードは動作する (カメラ状態のみスキップ)。
        /// </summary>
        public void SetCameraPersistence(ICameraStatePersistence cameraPersistence)
        {
            _cameraPersistence = cameraPersistence;
        }

        /// <summary>
        /// ノード visual の rotation 取得 provider を注入する (cue 表裏 fix)。
        /// 未注入でもセーブ/ロードは動作する (rotation enrichment のみスキップ → LookRotation fallback)。
        /// </summary>
        public void SetNodeVisualRotationProvider(INodeVisualRotationProvider provider)
        {
            _rotationProvider = provider;
        }

        /// <summary>現在のグラフを JSON で保存する。</summary>
        /// <param name="fileName">ファイル名 (拡張子不要)。null/空なら timestamp で自動生成。</param>
        public bool SaveGraph(string? fileName = null)
        {
            if (_graphContext == null || _repository == null)
            {
                Debug.LogError("[GraphSaveLoadManager] Not configured. Call Initialize + Configure first.");
                return false;
            }

            var resolved = ResolveFileName(fileName);
            var data = _graphContext.Context.Serialize();
            EnrichNodeRotations(data);
            CaptureCameraState(data);
            CaptureActiveSceneName(data);

            if (!_repository.SaveGraph(resolved, data)) return false;
            OnGraphSaved?.Invoke(resolved);
            return true;
        }

        /// <summary>JSON ファイルからグラフを復元する。既存グラフはクリアされる。</summary>
        public bool LoadGraph(string fileName)
        {
            if (_graphContext == null || _repository == null ||
                _hydrator == null || _executor == null || _factory == null)
            {
                Debug.LogError("[GraphSaveLoadManager] Not configured. Call Initialize + Configure first.");
                return false;
            }

            var resolved = EnsureExtension(fileName);
            var data = _repository.LoadGraph(resolved);
            if (data == null) return false;

            // Scene 切替が必要なら graph mutation せず deferred load に切り替える。
            // 新シーン bootstrap が PendingCueLoad を消費して再度 LoadGraph を呼ぶ。
            if (TryScheduleSceneSwitch(resolved, data)) return true;

            CapturePendingRotations(data);

            // Phase 8 Codex review fix #1+#3: load 開始通知。subscriber (例: GameBootstrap) が
            // 旧 module instance の破棄を行う。state.Clear() より前に呼ぶ — Executor が新 module を
            // attach した後の OnGraphLoaded で CleanupAll すると新 module まで巻き込み破棄してしまう。
            OnGraphLoading?.Invoke();

            // Codex review fix #3: 部分適用 orphan を避けるため Clear 前に backup を取り、
            // Executor 失敗時は backup から復元する (旧 GraphState.Deserialize の安全網パターン)。
            // Phase 8: Clear は internal 化 (UI.GraphAdapter は InternalsVisibleTo で許可、transitional)。
            var backup = _graphContext.Context.Serialize();
            _graphContext.Context.Clear();

            try
            {
                var plan = _hydrator.Build(data);
                _executor.Execute(plan, _factory);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphSaveLoadManager] Hydration failed: {resolved} — rolling back. {e.Message}");
                RestoreFromBackup(backup);
                return false;
            }

            // カメラ状態の復元は OnGraphLoaded より前 — パネルが OnGraphLoaded で
            // 復元済みの binding / priority を読み直して購読を張り直すため。
            RestoreCameraState(data);

            Debug.Log($"[GraphSaveLoadManager] Loaded: {resolved} ({data.nodes.Count} nodes, {data.edges.Count} edges)");
            OnGraphLoaded?.Invoke();
            return true;
        }

        /// <summary>
        /// Codex review fix #3: Executor 失敗時の rollback。backup 自体が壊れている可能性があるため
        /// 二重 try で 失敗してもエラーログだけで Video は止めない (映像継続原則)。
        /// </summary>
        private void RestoreFromBackup(GraphData backup)
        {
            if (_graphContext == null || _hydrator == null || _executor == null || _factory == null) return;
            try
            {
                _graphContext.Context.Clear();
                var plan = _hydrator.Build(backup);
                _executor.Execute(plan, _factory);
                Debug.LogWarning($"[GraphSaveLoadManager] Rolled back to backup ({backup.nodes.Count} nodes).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphSaveLoadManager] Rollback also failed — graph may be empty: {e.Message}");
            }
        }

        /// <summary>
        /// Undo/Redo path から graph rebuild event を再利用するための raise API。
        /// </summary>
        /// <remarks>
        /// Save/Load の <see cref="OnGraphLoading"/> / <see cref="OnGraphLoaded"/> subscriber は
        /// 「旧 module CleanupAll → visual rebuild」を担う (GraphSaveLoadBootstrapWiring 参照)。
        /// Undo/Redo も graph state が完全差し替えになるため、同じ visual rebuild 経路を再利用する。
        /// 直接呼ばず CueLibraryService 経由で発火する。
        /// </remarks>
        internal void RaiseGraphLoading() => OnGraphLoading?.Invoke();
        internal void RaiseGraphLoaded() => OnGraphLoaded?.Invoke();

        public string[] GetSaveFiles() => _repository?.GetSaveFiles() ?? Array.Empty<string>();

        /// <summary>
        /// 指定 cue (JSON) を I/O だけして <see cref="GraphData.sceneName"/> を返す軽量プローブ。
        /// </summary>
        /// <remarks>
        /// CueListPanel の scene タブ集計用。GraphState への適用 (hydrate / executor) は行わないため
        /// graph 状態を破壊しない。ファイルが存在しない / 旧形式で sceneName が空 / 例外 のいずれの場合も
        /// 空文字を返す (fail-open)。
        /// </remarks>
        public string PeekCueSceneName(string fileName)
        {
            if (_repository == null || string.IsNullOrWhiteSpace(fileName)) return "";
            try
            {
                var resolved = EnsureExtension(fileName);
                var data = _repository.LoadGraph(resolved);
                return data?.sceneName ?? "";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GraphSaveLoadManager] PeekCueSceneName failed for '{fileName}': {e.Message}");
                return "";
            }
        }

        public bool DeleteSave(string fileName)
        {
            if (_repository == null) return false;
            return _repository.DeleteSave(EnsureExtension(fileName));
        }

        /// <summary>
        /// 全ノードの visual rotation を <see cref="NodeData.rotation"/> に書き込む。
        /// </summary>
        /// <remarks>
        /// provider 未注入 / visual 未存在のノードは rotation 空のまま (旧形式) → ロード時に
        /// <c>GraphLoadCoordinator</c> が LookRotation fallback に流す。fail-open。
        /// </remarks>
        private void EnrichNodeRotations(GraphData data)
        {
            if (_rotationProvider == null) return;
            foreach (var nd in data.nodes)
            {
                try
                {
                    if (_rotationProvider.TryGetRotation(nd.id, out var q))
                        nd.rotation = NodeData.FromQuaternion(q);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GraphSaveLoadManager] Rotation enrich failed: {nd.id} — {e.Message}");
                }
            }
        }

        /// <summary>
        /// 直近 LoadGraph で読み込んだ <see cref="NodeData.rotation"/> を id 経由 dict に保持する。
        /// </summary>
        /// <remarks>
        /// <c>GraphLoadCoordinator.Rebuild</c> は OnGraphLoaded subscriber 経由で呼ばれるため、
        /// その時点で <c>data</c> 自体は scope 外。subscriber が <see cref="LastLoadedRotations"/>
        /// を読み取れるよう、stash しておく。
        /// </remarks>
        private void CapturePendingRotations(GraphData data)
        {
            _lastLoadedRotations.Clear();
            foreach (var nd in data.nodes)
            {
                if (nd.HasRotation)
                    _lastLoadedRotations[nd.id] = nd.ToQuaternion();
            }
        }

        /// <summary>
        /// cue が別の base シーンを要求する場合のみ、cue 名を <see cref="PendingCueLoad"/> に
        /// 予約してシーン切替を発行し true を返す (caller は graph mutation を skip する)。
        /// env-isolation 構成では env は additive 管理のため通常は false を返す。
        /// </summary>
        /// <remarks>
        /// <para><b>env-scene-isolation との整合 (2026-05-28 fix):</b> env シーンは
        /// <c>AdditiveSceneLoader</c> が additive ロードし、Skybox/GI 計算のため
        /// <see cref="SceneManager.SetActiveScene"/> で「active scene」に設定する。そのため
        /// 旧来の「active scene を保存 → load 時に Single ロード」方式だと env 名 (例 "concrete")
        /// が保存され、ロード時に base シーン (起動シーン = index 0、XR rig / cameras / bootstrap
        /// を保持) ごと Single ロードで破棄してしまう ("cue ロードでカメラが消える" の原因)。</para>
        ///
        /// <para>対策として base シーン (index 0) を基準に判定し、env / 既ロード scene への
        /// Single 切替は抑止して base を保護する。env 切替は SceneSwitch ノードが additive に担う。</para>
        /// </remarks>
        private static bool TryScheduleSceneSwitch(string resolvedFileName, GraphData data)
        {
            if (string.IsNullOrEmpty(data.sceneName)) return false;

            // base シーン = 起動シーン (ロード順 index 0)。env シーンは additive で index>0 に載る。
            string baseName;
            try { baseName = SceneManager.GetSceneAt(0).name ?? ""; }
            catch (Exception e)
            {
                Debug.LogWarning($"[GraphSaveLoadManager] Base scene query failed: {e.Message}");
                return false;
            }

            // 既に同じ base シーン上 → 切替不要 (in-place ロード)。
            if (string.Equals(data.sceneName, baseName, StringComparison.Ordinal)) return false;

            // cue.sceneName が現在ロード済 (= env が additive で載っている) → Single ロードしない。
            // Single ロードは base シーンごと巻き込み cameras / XR rig / bootstrap を破棄する。
            var alreadyLoaded = SceneManager.GetSceneByName(data.sceneName);
            if (alreadyLoaded.IsValid() && alreadyLoaded.isLoaded) return false;

            // base と異なり未ロードの scene 名。旧形式 cue は active scene (= env 名) を保存して
            // いるため Single ロードすると base を破壊する。base 保護のため切替を抑止し in-place
            // ロードへ倒す (fail-safe)。env は SceneSwitch ノード / env パネルで切替える。
            Debug.LogWarning(
                $"[GraphSaveLoadManager] Cue scene '{data.sceneName}' は base '{baseName}' と異なり未ロード " +
                "(旧形式 cue の env 名と推定)。base シーン (cameras/XR rig) 保護のため Single 切替を抑止し " +
                "in-place ロードします。env は SceneSwitch ノード / env パネルで切替えてください。");
            return false;
        }

        /// <summary>
        /// base シーン名 (起動シーン = index 0) を GraphData に書き込む。失敗時は空のまま。
        /// </summary>
        /// <remarks>
        /// env-isolation では env シーンが additive で active scene になるため、active scene を
        /// 保存すると env 名が記録され load 時に base ごと Single ロードで破棄してしまう。
        /// base シーン (index 0) を記録することで cue は常に正しい base を指す。
        /// </remarks>
        private static void CaptureActiveSceneName(GraphData data)
        {
            try
            {
                data.sceneName = SceneManager.GetSceneAt(0).name ?? "";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GraphSaveLoadManager] Base scene name capture failed: {e.Message}");
                data.sceneName = "";
            }
        }

        /// <summary>カメラ状態を捕捉して GraphData に書き込む。失敗してもセーブは続行 (fail-open)。</summary>
        private void CaptureCameraState(GraphData data)
        {
            if (_cameraPersistence == null) return;
            try
            {
                data.cameraState = _cameraPersistence.Capture();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphSaveLoadManager] Camera state capture failed: {e.Message}");
            }
        }

        /// <summary>
        /// GraphData のカメラ状態をシーンへ復元する。失敗してもグラフロードは成功扱いで続行する
        /// (fail-open — カメラ復元失敗を映像停止に伝播させない)。
        /// </summary>
        private void RestoreCameraState(GraphData data)
        {
            if (_cameraPersistence == null) return;
            try
            {
                _cameraPersistence.Restore(data.cameraState);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphSaveLoadManager] Camera state restore failed: {e.Message}");
            }
        }

        private static string ResolveFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                var ts = DateTime.Now.ToString("yyyyMMdd_HHmm");
                return DefaultPrefix + ts + FileExtension;
            }
            return EnsureExtension(fileName!);
        }

        private static string EnsureExtension(string fileName) =>
            fileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)
                ? fileName : fileName + FileExtension;
    }
}
