#nullable enable

using System;
using R3;
using Rhizomode.Cameras;
using Rhizomode.Input.Contracts;
using Rhizomode.Input.Desktop;
using Rhizomode.Input.XR;
using Rhizomode.Interaction.GraphAdapter;
using Rhizomode.Modules;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.UI;
using Rhizomode.XR;
using UnityEngine;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// VR / Desktop 入力ルーターの選択と、レイキャスト / エッジ / ノード / グラブ / スクロールメニュー
    /// 各 handler の相互配線を担う post-Build wiring。Plan v5.4 §15 (V3c): 旧
    /// <c>GameBootstrap.InitializeInteractionHandlers</c> (~130 行) を Bootstrap asmdef へ移送。
    /// </summary>
    /// <remarks>
    /// V2 踏襲: <see cref="InteractionInstaller"/> は本クラスを container に登録するのみ。副作用を伴う
    /// <see cref="Wire"/> は Build 後の eager step が駆動する。<see cref="Wire"/> は
    /// <see cref="GraphContextBehaviour"/> と、ScrollMenu のノード選択コールバック (GameBootstrap 側の
    /// visual 創出ロジック) を transitional に引数で受け取る — GraphContextBehaviour の Installer 化と
    /// NodeSpawnService / coordinator の Bootstrap 移送は V-final。
    ///
    /// <see cref="ActiveInput"/> は <see cref="Wire"/> 完了後に確定する。GameBootstrap / 他 wiring
    /// (Ableton) はこれを参照する。<see cref="Dispose"/> で ScrollMenu の購読を解除する
    /// (container 所有 Lifetime.Singleton)。
    /// </remarks>
    public sealed class InteractionBootstrapWiring : IDisposable
    {
        private readonly XrSceneReferences _refs;
        private readonly NodeTypeRegistry _typeRegistry;
        private readonly ModuleLifecycleProcessor _moduleProcessor;
        private readonly SpatialIntentToCommandTranslator _intentSink;

        private ScrollMenuVisualController? _subscribedScrollMenu;
        private Action<string>? _onNodeTypeSelected;
        private bool _wired;

        /// <summary>VR / Desktop の選択結果。<see cref="Wire"/> 完了後に確定 (degraded scene では null)。</summary>
        public IControllerInput? ActiveInput { get; private set; }

        public InteractionBootstrapWiring(
            XrSceneReferences refs,
            NodeTypeRegistry typeRegistry,
            ModuleLifecycleProcessor moduleProcessor,
            SpatialIntentToCommandTranslator intentSink)
        {
            _refs = refs;
            _typeRegistry = typeRegistry;
            _moduleProcessor = moduleProcessor;
            _intentSink = intentSink;
        }

        /// <summary>
        /// 入力ルーターを選択し、全 interaction handler を初期化・配線する。
        /// </summary>
        /// <param name="graphContext">handler が要する GraphContextBehaviour (transitional 引数 — V-final で Installer 化)。</param>
        /// <param name="onNodeTypeSelected">
        /// ScrollMenu のノードタイプ選択コールバック。GameBootstrap 側の visual 創出ロジック
        /// (OnScrollMenuNodeSelected) を transitional に受け取る。
        /// </param>
        public void Wire(GraphContextBehaviour? graphContext, Action<string> onNodeTypeSelected)
        {
            if (_wired) return;

            var controllerInput = _refs.ControllerInput;
            var desktopInput = _refs.DesktopInput;
            bool isDesktop = desktopInput != null && desktopInput.gameObject.activeInHierarchy;

            if (isDesktop)
            {
                ActiveInput = desktopInput;
                if (controllerInput != null) controllerInput.enabled = false;
                Debug.Log("[InteractionBootstrapWiring] Desktop debug mode active");
            }
            else
            {
                ActiveInput = controllerInput;
            }

            if (ActiveInput == null)
            {
                // 入力ルーター未配置 — _wired は立てず、startup 順序が変わった場合の retry を許す。
                Debug.LogError("[InteractionBootstrapWiring] No input router available!");
                return;
            }

            // 入力ルーターが確定したら配線済みとマークする (再入は no-op)。
            _wired = true;

            var sharedRaycastService = _refs.SharedRaycastService;
            if (sharedRaycastService == null)
                Debug.LogError("[InteractionBootstrapWiring] sharedRaycastService is not assigned!");

            var input = ActiveInput;
            IRayProvider rayProvider = (IRayProvider)input;
            IControllerPose controllerPose = (IControllerPose)input;
            ILeftHandRay leftHandRay = (ILeftHandRay)input;
            ILeftHandInput leftHandInput = (ILeftHandInput)input;

            var visualManager = _refs.VisualManager;
            var edgeVisualManager = _refs.EdgeVisualManager;
            var edgeDragHandler = _refs.EdgeDragHandler;
            var edgeCutHandler = _refs.EdgeCutHandler;
            var nodeDeleteHandler = _refs.NodeDeleteHandler;
            var nodeGrabHandler = _refs.NodeGrabHandler;
            var pathControlPointGrabHandler = _refs.PathControlPointGrabHandler;
            var pathEditorManager = _refs.PathEditorManager;
            var object3DGrabHandler = _refs.Object3DGrabHandler;
            var uiRaycastDriver = _refs.UIRaycastDriver;
            var scrollMenuVisual = _refs.ScrollMenuVisual;
            var scrollMenuInteraction = _refs.ScrollMenuInteraction;

            // 共有レイキャストサービスの初期化（全ハンドラの前に）
            if (sharedRaycastService != null)
                sharedRaycastService.Initialize(rayProvider);

            if (edgeVisualManager != null && visualManager != null)
                edgeVisualManager.Initialize(visualManager);

            if (edgeDragHandler != null && visualManager != null &&
                graphContext != null && edgeVisualManager != null &&
                sharedRaycastService != null)
            {
                edgeDragHandler.Initialize(
                    rayProvider, input, visualManager,
                    graphContext, edgeVisualManager, sharedRaycastService);

                if (nodeGrabHandler != null)
                    edgeDragHandler.SetGrabbingCheck(() => nodeGrabHandler.IsGrabbing);
            }

            if (edgeCutHandler != null && edgeVisualManager != null && graphContext != null)
            {
                edgeCutHandler.Initialize(input, rayProvider, edgeVisualManager, graphContext);
            }

            if (nodeDeleteHandler != null && visualManager != null &&
                graphContext != null && edgeVisualManager != null &&
                sharedRaycastService != null)
            {
                nodeDeleteHandler.Initialize(
                    input, sharedRaycastService, visualManager,
                    graphContext, edgeVisualManager);
                nodeDeleteHandler.SetDeleteDependencies(edgeDragHandler, _moduleProcessor.DestroyInstance);
            }

            if (nodeGrabHandler != null && visualManager != null &&
                sharedRaycastService != null && edgeVisualManager != null)
            {
                nodeGrabHandler.Initialize(
                    input, controllerPose, leftHandRay, leftHandInput,
                    sharedRaycastService, visualManager, edgeVisualManager);
            }

            if (pathControlPointGrabHandler != null && pathEditorManager != null &&
                sharedRaycastService != null)
            {
                pathControlPointGrabHandler.Initialize(
                    input, controllerPose, sharedRaycastService, pathEditorManager);
            }

            if (object3DGrabHandler != null)
            {
                Observable<Vector2> turnInput = isDesktop
                    ? desktopInput!.OnTurnInput
                    : controllerInput!.OnTurnInput;
                object3DGrabHandler.Initialize(
                    input, controllerPose, leftHandRay, leftHandInput, turnInput);
            }

            if (uiRaycastDriver != null && sharedRaycastService != null)
                uiRaycastDriver.Initialize(input, sharedRaycastService);

            // 巻物メニューの初期化
            if (scrollMenuVisual != null)
            {
                scrollMenuVisual.Initialize(_typeRegistry);
                scrollMenuVisual.OnNodeTypeSelected += onNodeTypeSelected;
                _subscribedScrollMenu = scrollMenuVisual;
                _onNodeTypeSelected = onNodeTypeSelected;
            }

            if (scrollMenuInteraction != null && scrollMenuVisual != null &&
                sharedRaycastService != null)
            {
                scrollMenuInteraction.Initialize(
                    input, leftHandRay, leftHandInput,
                    sharedRaycastService, scrollMenuVisual);

                if (isDesktop)
                    scrollMenuInteraction.SetDesktopMode(true);

                if (edgeDragHandler != null)
                    scrollMenuInteraction.SetEdgeDragHandler(edgeDragHandler);

                // メニュー状態変更時にエッジ切断・ノード削除・クリップ発火も無効化
                scrollMenuInteraction.SetMenuStateCallback(isIdle =>
                {
                    edgeCutHandler?.SetEnabled(isIdle);
                    nodeDeleteHandler?.SetEnabled(isIdle);
                    _refs.ClipFireHandler?.SetEnabled(isIdle);
                });
            }

            // Plan v5.3 Phase 5 Round E: IntentSink wiring。
            // 3 handler (EdgeDrag / EdgeCut / NodeDelete) を intent emit に切替。
            edgeDragHandler?.SetIntentSink(_intentSink);
            edgeCutHandler?.SetIntentSink(_intentSink);
            nodeDeleteHandler?.SetIntentSink(_intentSink);
            Debug.Log("[InteractionBootstrapWiring] IntentSink wired up for 3 handlers.");
        }

        public void Dispose()
        {
            if (_subscribedScrollMenu != null && _onNodeTypeSelected != null)
                _subscribedScrollMenu.OnNodeTypeSelected -= _onNodeTypeSelected;
            _subscribedScrollMenu = null;
            _onNodeTypeSelected = null;
        }
    }
}
