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
#pragma warning disable CS0618 // GraphState.Serialize は Phase 8 で internal 化予定
            var data = _graphContext.Context.Serialize();
#pragma warning restore CS0618

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

            // 既存グラフをクリアしてから Executor で復元
#pragma warning disable CS0618 // GraphState.Clear は Phase 8 で internal 化予定
            _graphContext.Context.Clear();
#pragma warning restore CS0618

            try
            {
                var plan = _hydrator.Build(data);
                _executor.Execute(plan, _factory);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphSaveLoadManager] Hydration failed: {resolved} — {e.Message}");
                return false;
            }

            Debug.Log($"[GraphSaveLoadManager] Loaded: {resolved} ({data.nodes.Count} nodes, {data.edges.Count} edges)");
            OnGraphLoaded?.Invoke();
            return true;
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
