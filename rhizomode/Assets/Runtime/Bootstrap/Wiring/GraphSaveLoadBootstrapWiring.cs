#nullable enable

using System;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Runtime;
using Rhizomode.Graph.Serialization;
using Rhizomode.Input.Contracts;
using Rhizomode.Modules;
using Rhizomode.Persistence.Contracts;
using Rhizomode.UI;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// GraphSaveLoadManager の Initialize / Configure / OnGraphLoading/OnGraphLoaded 購読を担う
    /// post-Build wiring。Plan v5.4 §15 V-final (Vf-a): 旧 <c>GameBootstrap.ConfigureSaveLoad</c> +
    /// <c>OnGraphLoading/OnGraphLoaded</c> + <c>InitializeVerticalSliceSystems</c> の GraphSaveLoad 駆動部を
    /// Bootstrap asmdef へ移送。
    /// </summary>
    /// <remarks>
    /// 担当する配線:
    /// <list type="bullet">
    ///   <item><see cref="GraphSaveLoadManager.Initialize"/> (graphContext 注入)</item>
    ///   <item><see cref="GraphSaveLoadManager.Configure"/> (Repository / Hydrator / Executor /
    ///     NodeFactory / SavePathProvider 注入)</item>
    ///   <item>OnGraphLoading: 旧 module instance を <see cref="ModuleLifecycleProcessor.CleanupAll"/>
    ///     で全破棄 (Executor が新 module を attach する前)</item>
    ///   <item>OnGraphLoaded: <see cref="Object3DProxyBindService.BindAllInGraph"/> +
    ///     <see cref="GraphLoadCoordinator.Rebuild"/> で visual 全再構築 + 回転</item>
    /// </list>
    /// <see cref="Wire"/> は <see cref="InteractionBootstrapWiring"/> 完了後の eager step で駆動 —
    /// activeInput は OnGraphLoaded 時の visual 回転に使う。Wire 時に確定済 activeInput を受け取る。
    /// <see cref="Dispose"/> でイベント購読を解除する (container 所有 Lifetime.Singleton)。
    /// </remarks>
    public sealed class GraphSaveLoadBootstrapWiring : IDisposable
    {
        private readonly XrSceneReferences _refs;
        private readonly NodeRuntime _nodeRuntime;
        private readonly INodeFactory _nodeFactory;
        private readonly IGraphRepository _graphRepository;
        private readonly GraphHydrator _graphHydrator;
        private readonly ISavePathProvider _savePathProvider;
        private readonly ModuleLifecycleProcessor _moduleProcessor;
        private readonly Object3DProxyBindService _proxyBindService;
        private readonly GraphLoadCoordinator _loadCoordinator;
        private readonly ICameraStatePersistence _cameraPersistence;
        private readonly INodeVisualRotationProvider _rotationProvider;

        private GraphSaveLoadManager? _subscribedSaveLoad;
        private Action? _onLoadingHandler;
        private Action? _onLoadedHandler;
        private IControllerInput? _activeInput;
        private bool _wired;

        public GraphSaveLoadBootstrapWiring(
            XrSceneReferences refs,
            NodeRuntime nodeRuntime,
            INodeFactory nodeFactory,
            IGraphRepository graphRepository,
            GraphHydrator graphHydrator,
            ISavePathProvider savePathProvider,
            ModuleLifecycleProcessor moduleProcessor,
            Object3DProxyBindService proxyBindService,
            GraphLoadCoordinator loadCoordinator,
            ICameraStatePersistence cameraPersistence,
            INodeVisualRotationProvider rotationProvider)
        {
            _refs = refs;
            _nodeRuntime = nodeRuntime;
            _nodeFactory = nodeFactory;
            _graphRepository = graphRepository;
            _graphHydrator = graphHydrator;
            _savePathProvider = savePathProvider;
            _moduleProcessor = moduleProcessor;
            _proxyBindService = proxyBindService;
            _loadCoordinator = loadCoordinator;
            _cameraPersistence = cameraPersistence;
            _rotationProvider = rotationProvider;
        }

        /// <summary>
        /// graphSaveLoad の Initialize / Configure を実行し、Loading/Loaded 購読を張る。
        /// </summary>
        /// <param name="activeInput">OnGraphLoaded 時の visual 回転用。Wire 時点で確定済を渡す。</param>
        public void Wire(IControllerInput? activeInput)
        {
            if (_wired) return;
            _wired = true;
            _activeInput = activeInput;

            var graphSaveLoad = _refs.GraphSaveLoad;
            var graphContext = _refs.GraphContext;
            if (graphSaveLoad == null || graphContext == null) return;

            graphSaveLoad.Initialize(graphContext);

            var executor = new HydrationPlanExecutor(_nodeRuntime);
            graphSaveLoad.Configure(
                _graphRepository, _graphHydrator, executor, _nodeFactory, _savePathProvider);
            graphSaveLoad.SetCameraPersistence(_cameraPersistence);
            graphSaveLoad.SetNodeVisualRotationProvider(_rotationProvider);
            Debug.Log("[GraphSaveLoadBootstrapWiring] SaveLoad configured (Repository + Hydrator + Executor).");

            _onLoadingHandler = OnGraphLoadingHandler;
            _onLoadedHandler = OnGraphLoaded;
            graphSaveLoad.OnGraphLoading += _onLoadingHandler;
            graphSaveLoad.OnGraphLoaded += _onLoadedHandler;
            _subscribedSaveLoad = graphSaveLoad;

            // Cue 起因のシーン切替後 — 旧シーンの GraphSaveLoadManager.LoadGraph が
            // PendingCueLoad に cue 名を予約してから SceneManager.LoadSceneAsync を発行している。
            // 新シーン bootstrap (本 Wire) が予約を消費して LoadGraph を再発火する。
            if (PendingCueLoad.TryConsume(out var pendingCueName))
            {
                Debug.Log($"[GraphSaveLoadBootstrapWiring] Resuming deferred cue load after scene switch: '{pendingCueName}'");
                graphSaveLoad.LoadGraph(pendingCueName);
            }
        }

        private void OnGraphLoadingHandler()
        {
            // 旧 module instance を全破棄 (Executor が新 module を attach する前)
            _moduleProcessor.CleanupAll();
        }

        private void OnGraphLoaded()
        {
            var graphContext = _refs.GraphContext;
            if (graphContext == null) return;

            var ctx = graphContext.Context;

            // HydrationPlanExecutor が processors (SceneLoader / Module) を自動駆動するため、
            // 手動 processor 呼び出しは不要。Object3D の GraphState 観測 bind のみ実施。
            _proxyBindService.BindAllInGraph();

            // visual rebuild + プレイヤー方向への回転
            // cue 表裏 fix: 保存済 rotation があれば優先復元、無い node のみ LookRotation fallback。
            var savedRotations = _refs.GraphSaveLoad?.LastLoadedRotations;
            _loadCoordinator.Rebuild(ctx, _activeInput, savedRotations);
        }

        public void Dispose()
        {
            if (_subscribedSaveLoad != null)
            {
                if (_onLoadingHandler != null)
                    _subscribedSaveLoad.OnGraphLoading -= _onLoadingHandler;
                if (_onLoadedHandler != null)
                    _subscribedSaveLoad.OnGraphLoaded -= _onLoadedHandler;
            }
            _subscribedSaveLoad = null;
            _onLoadingHandler = null;
            _onLoadedHandler = null;
        }
    }
}
