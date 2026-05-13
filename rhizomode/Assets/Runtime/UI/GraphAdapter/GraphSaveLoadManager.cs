#nullable enable

using System;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Runtime;
using Rhizomode.Graph.Serialization;
using Rhizomode.Persistence.Contracts;
using Rhizomode.SharedKernel;
using UnityEngine;

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

        public string[] GetSaveFiles() => _repository?.GetSaveFiles() ?? Array.Empty<string>();

        public bool DeleteSave(string fileName)
        {
            if (_repository == null) return false;
            return _repository.DeleteSave(EnsureExtension(fileName));
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
