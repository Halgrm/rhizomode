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
    ///
    /// M6 fix (post-launch tidy): 旧 <c>Wire</c> (151 行モノリス) を責務別 helper に分割。CLAUDE.md
    /// 「Methods ≤ 30 lines」原則準拠。
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
        private bool _isDesktopMode;

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
        public void Wire(GraphContextBehaviour? graphContext, Action<string> onNodeTypeSelected)
        {
            if (_wired) return;
            if (!TrySelectActiveInput()) return;
            _wired = true;

            var roles = ResolveInputRoles(ActiveInput!);
            var raycast = _refs.SharedRaycastService;
            if (raycast == null)
                Debug.LogError("[InteractionBootstrapWiring] sharedRaycastService is not assigned!");
            raycast?.Initialize(roles.RayProvider);

            InitEdgeVisuals();
            WireEdgeHandlers(graphContext, roles, raycast);
            WireGrabHandlers(roles, raycast);
            WireObject3DGrab(roles);
            InitUiRaycastDriver(roles.Input, raycast);
            WireScrollMenu(roles, raycast, onNodeTypeSelected);
            WireIntentSinks();
        }

        /// <summary>
        /// VR / Desktop 入力ルーターを選択して <see cref="ActiveInput"/> を確定する。
        /// 入力が無ければ <c>_wired</c> を立てず、startup 順序変化時の retry を許す。
        /// </summary>
        private bool TrySelectActiveInput()
        {
            var controllerInput = _refs.ControllerInput;
            var desktopInput = _refs.DesktopInput;
            _isDesktopMode = desktopInput != null && desktopInput.gameObject.activeInHierarchy;

            if (_isDesktopMode)
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
                Debug.LogError("[InteractionBootstrapWiring] No input router available!");
                return false;
            }
            return true;
        }

        /// <summary>
        /// <see cref="IControllerInput"/> を各 role interface にキャストして束ねる。
        /// 全 handler が同じ実装に対する複数 view として参照する。
        /// </summary>
        private static InputRoles ResolveInputRoles(IControllerInput input) =>
            new(input,
                (IRayProvider)input,
                (IControllerPose)input,
                (ILeftHandRay)input,
                (ILeftHandInput)input);

        private void InitEdgeVisuals()
        {
            var edgeVisualManager = _refs.EdgeVisualManager;
            var visualManager = _refs.VisualManager;
            if (edgeVisualManager != null && visualManager != null)
                edgeVisualManager.Initialize(visualManager);
        }

        private void WireEdgeHandlers(
            GraphContextBehaviour? graphContext,
            InputRoles roles,
            SharedRaycastService? raycast)
        {
            var visualManager = _refs.VisualManager;
            var edgeVisualManager = _refs.EdgeVisualManager;
            var edgeDragHandler = _refs.EdgeDragHandler;
            var edgeCutHandler = _refs.EdgeCutHandler;
            var nodeDeleteHandler = _refs.NodeDeleteHandler;
            var nodeGrabHandler = _refs.NodeGrabHandler;

            if (edgeDragHandler != null && visualManager != null &&
                graphContext != null && edgeVisualManager != null && raycast != null)
            {
                edgeDragHandler.Initialize(
                    roles.RayProvider, roles.Input, visualManager,
                    graphContext, edgeVisualManager, raycast);
                if (nodeGrabHandler != null)
                    edgeDragHandler.SetGrabbingCheck(() => nodeGrabHandler.IsGrabbing);
            }

            if (edgeCutHandler != null && edgeVisualManager != null && graphContext != null)
                edgeCutHandler.Initialize(roles.Input, roles.RayProvider, edgeVisualManager, graphContext);

            if (nodeDeleteHandler != null && visualManager != null &&
                graphContext != null && edgeVisualManager != null && raycast != null)
            {
                nodeDeleteHandler.Initialize(
                    roles.Input, raycast, visualManager, graphContext, edgeVisualManager);
                nodeDeleteHandler.SetDeleteDependencies(edgeDragHandler, _moduleProcessor.DestroyInstance);
            }
        }

        private void WireGrabHandlers(InputRoles roles, SharedRaycastService? raycast)
        {
            var visualManager = _refs.VisualManager;
            var edgeVisualManager = _refs.EdgeVisualManager;
            var nodeGrabHandler = _refs.NodeGrabHandler;
            var pathControlPointGrabHandler = _refs.PathControlPointGrabHandler;
            var pathEditorManager = _refs.PathEditorManager;

            if (nodeGrabHandler != null && visualManager != null &&
                raycast != null && edgeVisualManager != null)
            {
                nodeGrabHandler.Initialize(
                    roles.Input, roles.ControllerPose, roles.LeftHandRay, roles.LeftHandInput,
                    raycast, visualManager, edgeVisualManager);
            }

            if (pathControlPointGrabHandler != null && pathEditorManager != null && raycast != null)
            {
                pathControlPointGrabHandler.Initialize(
                    roles.Input, roles.ControllerPose, raycast, pathEditorManager);
            }
        }

        private void WireObject3DGrab(InputRoles roles)
        {
            var object3DGrabHandler = _refs.Object3DGrabHandler;
            if (object3DGrabHandler == null) return;

            Observable<Vector2> turnInput = _isDesktopMode
                ? _refs.DesktopInput!.OnTurnInput
                : _refs.ControllerInput!.OnTurnInput;
            object3DGrabHandler.Initialize(
                roles.Input, roles.ControllerPose, roles.LeftHandRay, roles.LeftHandInput, turnInput);
        }

        private void InitUiRaycastDriver(IControllerInput input, SharedRaycastService? raycast)
        {
            var uiRaycastDriver = _refs.UIRaycastDriver;
            if (uiRaycastDriver != null && raycast != null)
                uiRaycastDriver.Initialize(input, raycast);
        }

        private void WireScrollMenu(
            InputRoles roles,
            SharedRaycastService? raycast,
            Action<string> onNodeTypeSelected)
        {
            var scrollMenuVisual = _refs.ScrollMenuVisual;
            var scrollMenuInteraction = _refs.ScrollMenuInteraction;
            var edgeDragHandler = _refs.EdgeDragHandler;
            var edgeCutHandler = _refs.EdgeCutHandler;
            var nodeDeleteHandler = _refs.NodeDeleteHandler;

            if (scrollMenuVisual != null)
            {
                scrollMenuVisual.Initialize(_typeRegistry);
                scrollMenuVisual.OnNodeTypeSelected += onNodeTypeSelected;
                _subscribedScrollMenu = scrollMenuVisual;
                _onNodeTypeSelected = onNodeTypeSelected;
            }

            if (scrollMenuInteraction == null || scrollMenuVisual == null || raycast == null) return;

            scrollMenuInteraction.Initialize(
                roles.Input, roles.RayProvider, roles.LeftHandRay, roles.LeftHandInput,
                raycast, scrollMenuVisual);

            if (_isDesktopMode) scrollMenuInteraction.SetDesktopMode(true);
            if (edgeDragHandler != null) scrollMenuInteraction.SetEdgeDragHandler(edgeDragHandler);

            // メニュー状態変更時にエッジ切断・ノード削除・クリップ発火も無効化
            scrollMenuInteraction.SetMenuStateCallback(isIdle =>
            {
                edgeCutHandler?.SetEnabled(isIdle);
                nodeDeleteHandler?.SetEnabled(isIdle);
                _refs.ClipFireHandler?.SetEnabled(isIdle);
            });
        }

        /// <summary>
        /// Plan v5.3 Phase 5 Round E: 3 handler (EdgeDrag / EdgeCut / NodeDelete) を intent emit に切替。
        /// </summary>
        private void WireIntentSinks()
        {
            _refs.EdgeDragHandler?.SetIntentSink(_intentSink);
            _refs.EdgeCutHandler?.SetIntentSink(_intentSink);
            _refs.NodeDeleteHandler?.SetIntentSink(_intentSink);
            Debug.Log("[InteractionBootstrapWiring] IntentSink wired up for 3 handlers.");
        }

        public void Dispose()
        {
            if (_subscribedScrollMenu != null && _onNodeTypeSelected != null)
                _subscribedScrollMenu.OnNodeTypeSelected -= _onNodeTypeSelected;
            _subscribedScrollMenu = null;
            _onNodeTypeSelected = null;
        }

        /// <summary>
        /// <see cref="IControllerInput"/> を複数 role interface にキャストして束ねた immutable な束。
        /// </summary>
        private readonly struct InputRoles
        {
            public readonly IControllerInput Input;
            public readonly IRayProvider RayProvider;
            public readonly IControllerPose ControllerPose;
            public readonly ILeftHandRay LeftHandRay;
            public readonly ILeftHandInput LeftHandInput;

            public InputRoles(
                IControllerInput input,
                IRayProvider rayProvider,
                IControllerPose controllerPose,
                ILeftHandRay leftHandRay,
                ILeftHandInput leftHandInput)
            {
                Input = input;
                RayProvider = rayProvider;
                ControllerPose = controllerPose;
                LeftHandRay = leftHandRay;
                LeftHandInput = leftHandInput;
            }
        }
    }
}
