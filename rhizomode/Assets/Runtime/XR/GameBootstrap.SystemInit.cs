#nullable enable

// Plan v5.3 F-8.2 抽出 6/N (Round F4): GameBootstrap god-object の system 初期化 3 メソッドを
// partial class に分離。InitializeVerticalSliceSystems / InitializeAudioDeviceSelector /
// InitializeInteractionHandlers の wiring を集約。

using System;
using R3;
using Rhizomode.UI;
using UnityEngine;

using Rhizomode.Input.Contracts;

namespace Rhizomode.XR
{
    public partial class GameBootstrap
    {
        /// <summary>
        /// Week 5: MirrorOutput, AudioDevice, StatusPanel, SaveLoadを初期化・接続する。
        /// </summary>
        private void InitializeVerticalSliceSystems()
        {
            // GraphSaveLoadManager
            if (graphSaveLoad != null && graphContext != null)
            {
                graphSaveLoad.Initialize(graphContext);

                // Phase 7 Round B: Persistence + Serialization の依存を注入。
                // factory / eventBus / nodeRuntime / executor を構築する (WireIntentSink でも使う)。
                ConfigureSaveLoad();

                // Phase 8 Codex review fix #1+#3: load 開始時に旧 module instance を破棄
                // (OnGraphLoaded 内で CleanupAll すると Executor が attach した新 module まで巻き込む)。
                graphSaveLoad.OnGraphLoading += OnGraphLoadingHandler;
                // ロード完了後にノード・エッジビジュアルを再構築
                graphSaveLoad.OnGraphLoaded += OnGraphLoaded;
            }

            // StatusPanelController
            if (statusPanel != null && graphContext != null)
                statusPanel.Initialize(graphContext);

            // CameraManagerPanelController (Round E5: IFloatOutputCatalog 経由に切替)
            if (cameraManagerPanel != null && graphContext != null)
            {
                var floatOutputCatalog = new GraphStateFloatOutputCatalog(() => graphContext.Context);
                cameraManagerPanel.Initialize(floatOutputCatalog);
                // 編集モード中はエッジ接続・切断・ノード削除を一時無効化
                cameraManagerPanel.AddEditModeListener(isEditing =>
                {
                    edgeDragHandler?.SetEnabled(!isEditing);
                    edgeCutHandler?.SetEnabled(!isEditing);
                    nodeDeleteHandler?.SetEnabled(!isEditing);
                });
            }

            // MirrorOutputController → Spout/NDI
            var headTransform = desktopInput != null && desktopInput.gameObject.activeInHierarchy
                ? desktopInput.HeadTransform
                : controllerInput?.HeadTransform;
            if (mirrorOutput != null && headTransform != null)
            {
                mirrorOutput.Initialize(headTransform);
                mirrorOutput.Activate();

                if (mirrorOutput.OutputTexture != null)
                {
                    spoutSender?.StartSending(mirrorOutput.OutputTexture);
                    ndiSender?.StartSending(mirrorOutput.OutputTexture);
                    desktopBlitter?.SetSource(mirrorOutput.OutputTexture);
                }
            }

            // CinemachinePreviewMonitor（デスクトップデバッグ時のみ）
            bool isDesktopMode = desktopInput != null && desktopInput.gameObject.activeInHierarchy;
            if (cinemachinePreview != null && isDesktopMode)
            {
                // CinemachinePreviewRig が非アクティブならアクティブ化
                var rig = cinemachinePreview.transform.root.gameObject;
                if (!rig.activeSelf)
                    rig.SetActive(true);

                cinemachinePreview.Initialize();
            }

            // AudioDeviceSelector → AudioAnalyzer
            InitializeAudioDeviceSelector();

            // Ableton OSC設定パネル＋クリップグリッド初期化
            InitializeAbletonOsc();
        }

        private void InitializeAudioDeviceSelector()
        {
            if (audioDeviceSelector == null || audioDriver?.Analyzer == null) return;

            var analyzer = audioDriver.Analyzer;
            audioDeviceSelector.Initialize(analyzer.AvailableDevices, analyzer.CurrentDevice);

            _onDeviceSelected = deviceName =>
            {
                analyzer.Initialize(deviceName);
                audioDeviceSelector.SetCurrentDevice(analyzer.CurrentDevice);
                statusPanel?.SetAudioDevice(deviceName);
            };
            audioDeviceSelector.OnDeviceSelected += _onDeviceSelected;

            _onRefreshRequested = () =>
            {
                audioDeviceSelector.UpdateDeviceList(analyzer.AvailableDevices);
            };
            audioDeviceSelector.OnRefreshRequested += _onRefreshRequested;

            // 初期デバイスがあればステータスパネルに反映
            if (analyzer.CurrentDevice != null)
                statusPanel?.SetAudioDevice(analyzer.CurrentDevice);
        }

        private void InitializeInteractionHandlers()
        {
            // VR / デスクトップ入力ルーター切替
            bool isDesktop = desktopInput != null && desktopInput.gameObject.activeInHierarchy;

            if (isDesktop)
            {
                _activeInput = desktopInput;
                // VR入力ルーターを無効化（競合防止）
                if (controllerInput != null) controllerInput.enabled = false;
                Debug.Log("[GameBootstrap] Desktop debug mode active");
            }
            else
            {
                _activeInput = controllerInput;
            }

            if (_activeInput == null)
            {
                Debug.LogError("[GameBootstrap] No input router available!");
                return;
            }

            if (sharedRaycastService == null)
                Debug.LogError("[GameBootstrap] sharedRaycastService is not assigned!");

            var input = _activeInput;
            IRayProvider rayProvider = (IRayProvider)input;
            IControllerPose controllerPose = (IControllerPose)input;
            ILeftHandRay leftHandRay = (ILeftHandRay)input;
            ILeftHandInput leftHandInput = (ILeftHandInput)input;

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

                // グラブ中はエッジ接続をスキップ
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
                // Phase 6 Round A: DestroyModuleInstance → _moduleProcessor.DestroyInstance に委譲
                nodeDeleteHandler.SetDeleteDependencies(edgeDragHandler,
                    _moduleProcessor != null ? _moduleProcessor.DestroyInstance : (Action<string>?)null);
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
                // Turn入力: VRならControllerInputRouter.OnTurnInput、デスクトップならDesktopInputRouter.OnTurnInput
                Observable<Vector2> turnInput = isDesktop
                    ? desktopInput!.OnTurnInput
                    : controllerInput!.OnTurnInput;
                object3DGrabHandler.Initialize(
                    input, controllerPose, leftHandRay, leftHandInput, turnInput);
            }

            if (uiRaycastDriver != null && sharedRaycastService != null)
                uiRaycastDriver.Initialize(input, sharedRaycastService);

            // 巻物メニューの初期化
            if (scrollMenuVisual != null && _typeRegistry != null)
            {
                scrollMenuVisual.Initialize(_typeRegistry);

                // ノード選択イベント: GraphContextのファクトリでノード生成
                scrollMenuVisual.OnNodeTypeSelected += OnScrollMenuNodeSelected;
            }

            if (scrollMenuInteraction != null && scrollMenuVisual != null &&
                sharedRaycastService != null)
            {
                scrollMenuInteraction.Initialize(
                    input, leftHandRay, leftHandInput,
                    sharedRaycastService, scrollMenuVisual);

                if (isDesktop)
                    scrollMenuInteraction.SetDesktopMode(true);

                // メニューオープン中にエッジ接続を無効化するための連携
                if (edgeDragHandler != null)
                    scrollMenuInteraction.SetEdgeDragHandler(edgeDragHandler);

                // メニュー状態変更時にエッジ切断・ノード削除・クリップ発火も無効化
                scrollMenuInteraction.SetMenuStateCallback(isIdle =>
                {
                    edgeCutHandler?.SetEnabled(isIdle);
                    nodeDeleteHandler?.SetEnabled(isIdle);
                    clipFireHandler?.SetEnabled(isIdle);
                });
            }

            // Plan v5.3 Phase 5 Round E: SpatialIntentToCommandTranslator wiring。
            // 3 handler (EdgeDrag / EdgeCut / NodeDelete) を intent emit に切替。
            WireIntentSink();
        }
    }
}
