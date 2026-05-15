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
        /// Week 5: GraphSaveLoad を初期化し、VerticalSlice / Ableton wiring を駆動する。
        /// V3d 以降、UI / Cameras 系 (StatusPanel / CameraManagerPanel / MirrorOutput / Spout / NDI /
        /// DesktopBlitter / CinemachinePreview / health→StatusPanel 購読) は VerticalSliceBootstrapWiring へ移送。
        /// 本メソッドに残るのは GraphSaveLoad と Ableton wiring の駆動のみ — どちらも GameBootstrap-resident
        /// 状態 (NodeRuntime / coordinator / _activeInput) を要するため transitional に残置 (V-final で解消)。
        /// </summary>
        private void InitializeVerticalSliceSystems()
        {
            // GraphSaveLoadManager (V-final: HydrationPlanExecutor 構築と OnGraphLoaded のための
            // coordinator / BindObject3DProxyObservables 依存があり、Installer 化は GameBootstrap 解体時)。
            if (graphSaveLoad != null && graphContext != null)
            {
                graphSaveLoad.Initialize(graphContext);

                // Phase 7 Round B: Persistence + Serialization の依存を注入。
                ConfigureSaveLoad();

                // Phase 8 Codex review fix #1+#3: load 開始時に旧 module instance を破棄
                // (OnGraphLoaded 内で CleanupAll すると Executor が attach した新 module まで巻き込む)。
                graphSaveLoad.OnGraphLoading += OnGraphLoadingHandler;
                // ロード完了後にノード・エッジビジュアルを再構築
                graphSaveLoad.OnGraphLoaded += OnGraphLoaded;
            }

            // V3d: UI / Cameras / health subscription 系の wiring は VerticalSliceBootstrapWiring へ移送。
            // graphContext は transitional 引数として渡す (V-final で Installer 化)。
            _compositionRoot?.VerticalSliceWiring.Wire(graphContext);

            // V3a: AudioDeviceSelector wiring は EntryPointBootstrapper が Build 後即時駆動済。
            // Ableton OSC wiring は入力ルーター / SharedRaycastService を要するためここで駆動する
            // (InitializeSystems で InteractionBootstrapWiring.Wire が _activeInput を解決済)。
            // 一時的 Plan v5.4 違反 — V-final で解消。
            _compositionRoot?.AbletonWiring.Wire(_activeInput, sceneRefs != null ? sceneRefs.SharedRaycastService : null);
        }

        // V3c: 旧 InitializeInteractionHandlers (~130 行) は Bootstrap.Wiring の InteractionBootstrapWiring へ。
        // V3d: 旧 InitializeHealthMonitoring + UI/Cameras 系初期化は Bootstrap.Wiring の VerticalSliceBootstrapWiring へ。
    }
}
