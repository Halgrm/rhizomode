#nullable enable

// Plan v5.3 F-8.2 抽出 6/N (Round F4): GameBootstrap god-object の system 初期化 3 メソッドを
// partial class に分離。InitializeVerticalSliceSystems / InitializeAudioDeviceSelector /
// InitializeInteractionHandlers の wiring を集約。

using System;
using R3;
using Rhizomode.UI;
using UnityEngine;

using Rhizomode.Input.Contracts;
using Rhizomode.Observability.Runtime;
using Rhizomode.Audio.GraphAdapter;
using Rhizomode.OscMidi.GraphAdapter;
using Rhizomode.Ableton.GraphAdapter;

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

            // StatusPanelController (V3a: 参照は XrSceneReferences へ移送、Installer 化は V3d)
            var statusPanel = sceneRefs != null ? sceneRefs.StatusPanel : null;
            if (statusPanel != null && graphContext != null)
                statusPanel.Initialize(graphContext);

            // CameraManagerPanelController (Round E5: IFloatOutputCatalog 経由に切替)
            if (cameraManagerPanel != null && graphContext != null)
            {
                var floatOutputCatalog = new GraphStateFloatOutputCatalog(() => graphContext.Context);
                cameraManagerPanel.Initialize(floatOutputCatalog);
                // 編集モード中はエッジ接続・切断・ノード削除を一時無効化
                // V3c: handler 参照は XrSceneReferences 経由 (Installer 化済)。
                cameraManagerPanel.AddEditModeListener(isEditing =>
                {
                    sceneRefs?.EdgeDragHandler?.SetEnabled(!isEditing);
                    sceneRefs?.EdgeCutHandler?.SetEnabled(!isEditing);
                    sceneRefs?.NodeDeleteHandler?.SetEnabled(!isEditing);
                });
            }

            // MirrorOutputController → Spout/NDI
            var desktopInput = sceneRefs != null ? sceneRefs.DesktopInput : null;
            var controllerInput = sceneRefs != null ? sceneRefs.ControllerInput : null;
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

            // V3a: AudioDeviceSelector wiring は EntryPointBootstrapper が Build 後即時駆動済
            // (AudioDeviceSelectorWiring)。Ableton OSC wiring は入力ルーター / SharedRaycastService を
            // 要するためここで駆動する (InitializeSystems で InteractionBootstrapWiring.Wire が
            // _activeInput を解決済)。CompositionRoot 経由なのは Plan v5.4 §19 (VContainer 型は
            // Bootstrap asmdef のみ) を守るため — 一時的 Plan v5.4 違反、V-final で解消。
            // degraded 起動 (_typeRegistry == null で InitializeSystems が early return) では
            // _activeInput が null のまま渡るが、Wire は panel 表示を skip して fail-open する。
            _compositionRoot?.AbletonWiring.Wire(_activeInput, sceneRefs != null ? sceneRefs.SharedRaycastService : null);

            // Phase 12D: health monitor 登録は各 transport Installer + EntryPointBootstrapper が実施済。
            InitializeHealthMonitoring();
        }

        /// <summary>
        /// Phase 12D: Audio / OSC / MIDI / Ableton の <see cref="IHealthMonitor"/> を
        /// <see cref="HealthAggregator"/> に登録する。
        /// Plan v5.4 §15 (V2a): HealthAggregator の構築・所有は ObservabilityInstaller、Tick 駆動は
        /// VContainer の HealthAggregatorTickAdapter (ITickable)。本メソッドは monitor 登録と
        /// StatusPanel 購読のみ。transport が未配置でも monitor は Unknown を返すため fail-open。
        /// </summary>
        private void InitializeHealthMonitoring()
        {
            // HealthAggregator は LaunchCompositionRoot で container から resolve 済。
            // degraded 起動 (graphContext 未設定) では null のためスキップ。
            // V3a: monitor 登録 (Audio/OSC/MIDI/Ableton) は各 transport Installer +
            // EntryPointBootstrapper の Build 後 eager step へ移送済。本メソッドは StatusPanel への
            // 購読のみを担う (StatusPanel の Installer 化は V3d)。
            if (_healthAggregator == null) return;

            var statusPanel = sceneRefs != null ? sceneRefs.StatusPanel : null;
            if (statusPanel != null)
                _healthSubscription = _healthAggregator.OnHealthChange.Subscribe(statusPanel.SetHealth);
        }

        // V3c: 旧 InitializeInteractionHandlers (~130 行) は Bootstrap asmdef の
        // InteractionBootstrapWiring へ移送。GameBootstrap.InitializeSystems が
        // _compositionRoot.InteractionWiring.Wire(graphContext, OnScrollMenuNodeSelected) で駆動する。
    }
}
