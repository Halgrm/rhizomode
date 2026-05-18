#nullable enable

using System;
using R3;
using Rhizomode.Graph.Mutation;
using Rhizomode.Observability.Runtime;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// Week 5 vertical-slice の UI / Cameras 系配線を担う post-Build wiring。Plan v5.4 §15 (V3d):
    /// 旧 <c>GameBootstrap.InitializeVerticalSliceSystems</c> の cleanly movable 部 + 旧
    /// <c>InitializeHealthMonitoring</c> の StatusPanel 購読をここへ移送。
    /// </summary>
    /// <remarks>
    /// 担当する配線:
    /// <list type="bullet">
    ///   <item><see cref="StatusPanelController"/> の <c>Initialize(graphContext)</c></item>
    ///   <item><see cref="CameraManagerPanelController"/> の <c>Initialize</c> + edit-mode listener
    ///     (編集モード中はエッジ操作・ノード削除を無効化)</item>
    ///   <item><see cref="MirrorOutputController"/> の Initialize/Activate + Spout/NDI/Desktop blitter</item>
    ///   <item><see cref="CinemachinePreviewMonitor"/> (デスクトップモード時のみ Initialize)</item>
    ///   <item><see cref="HealthAggregator.OnHealthChange"/> → StatusPanel の購読</item>
    /// </list>
    /// V3d 時点で残置する責務 (GameBootstrap が引き続き担当 — V-final で解消):
    /// <list type="bullet">
    ///   <item>GraphSaveLoadManager の Initialize / ConfigureSaveLoad / event 購読
    ///     (HydrationPlanExecutor 構築・NodeRuntime / coordinator 依存)</item>
    ///   <item><see cref="Rhizomode.Bootstrap.Wiring.AbletonBootstrapWiring"/> の Wire 駆動
    ///     (ActiveInput 配線の Installer 化と同時に解消)</item>
    /// </list>
    /// <c>Wire</c> は <see cref="GraphContextBehaviour"/> を transitional 引数で受け取る (V-final で
    /// Installer 化)。<see cref="Dispose"/> で HealthAggregator 購読を解放する (container 所有
    /// Lifetime.Singleton)。
    /// </remarks>
    public sealed class VerticalSliceBootstrapWiring : IDisposable
    {
        private readonly XrSceneReferences _refs;
        private readonly HealthAggregator _healthAggregator;
        private readonly GraphCommandDispatcher _dispatcher;

        private IDisposable? _healthSubscription;
        private CameraManagerPanelController? _editModePanel;
        private Action<bool>? _editModeListener;
        private bool _wired;

        public VerticalSliceBootstrapWiring(
            XrSceneReferences refs,
            HealthAggregator healthAggregator,
            GraphCommandDispatcher dispatcher)
        {
            _refs = refs;
            _healthAggregator = healthAggregator;
            _dispatcher = dispatcher;
        }

        public void Wire(GraphContextBehaviour? graphContext)
        {
            if (_wired) return;
            _wired = true;

            var statusPanel = _refs.StatusPanel;
            if (statusPanel != null && graphContext != null)
                statusPanel.Initialize(graphContext);

            var cameraManagerPanel = _refs.CameraManagerPanel;
            if (cameraManagerPanel != null && graphContext != null)
            {
                var floatOutputCatalog = new GraphStateFloatOutputCatalog(() => graphContext.Context);
                cameraManagerPanel.Initialize(floatOutputCatalog);

                // 編集モード中はエッジ接続・切断・ノード削除を一時無効化。
                // F-Vf-c.1: lambda を field 保持して Dispose で解除する。
                _editModePanel = cameraManagerPanel;
                _editModeListener = isEditing =>
                {
                    _refs.EdgeDragHandler?.SetEnabled(!isEditing);
                    _refs.EdgeCutHandler?.SetEnabled(!isEditing);
                    _refs.NodeDeleteHandler?.SetEnabled(!isEditing);
                    // F2 fix (Codex review, 2026-05-18): LookAt/Path edit 中は Node/Object3D grab も無効化。
                    _refs.NodeGrabHandler?.SetEnabled(!isEditing);
                    _refs.Object3DGrabHandler?.SetEnabled(!isEditing);
                };
                cameraManagerPanel.AddEditModeListener(_editModeListener);
            }

            // MirrorOutputController → Spout/NDI
            var desktopInput = _refs.DesktopInput;
            var controllerInput = _refs.ControllerInput;
            var headTransform = desktopInput != null && desktopInput.gameObject.activeInHierarchy
                ? desktopInput.HeadTransform
                : controllerInput != null ? controllerInput.HeadTransform : null;
            var mirrorOutput = _refs.MirrorOutput;
            if (mirrorOutput != null && headTransform != null)
            {
                mirrorOutput.Initialize(headTransform);
                mirrorOutput.Activate();

                if (mirrorOutput.OutputTexture != null)
                {
                    _refs.SpoutSender?.StartSending(mirrorOutput.OutputTexture);
                    _refs.NdiSender?.StartSending(mirrorOutput.OutputTexture);
                    _refs.DesktopBlitter?.SetSource(mirrorOutput.OutputTexture);
                    _refs.MirrorPreview?.Initialize(mirrorOutput.OutputTexture);
                }

                // CameraManagerPanel の "Show UI in Mirror" toggle と双方向同期。
                // Activate 時点で default は IsUIVisible=false (clean show output)。
                cameraManagerPanel?.BindMirrorOutput(mirrorOutput);
            }

            // CinemachinePreviewMonitor (デスクトップデバッグ時のみ)
            bool isDesktopMode = desktopInput != null && desktopInput.gameObject.activeInHierarchy;
            var cinemachinePreview = _refs.CinemachinePreview;
            if (cinemachinePreview != null && isDesktopMode)
            {
                var rig = cinemachinePreview.transform.root.gameObject;
                if (!rig.activeSelf)
                    rig.SetActive(true);

                cinemachinePreview.Initialize();
            }

            // Cue (graph snapshot) WorldPanel — GraphSaveLoad は前段の Wiring で Configure 済。
            var cueListPanel = _refs.CueListPanel;
            var graphSaveLoad = _refs.GraphSaveLoad;
            if (cueListPanel != null && graphSaveLoad != null)
            {
                var cueService = new CueLibraryService(graphSaveLoad, _dispatcher, mirrorOutput);
                cueListPanel.Initialize(cueService);
            }

            // Phase 13C: health → StatusPanel 購読 (旧 GameBootstrap.InitializeHealthMonitoring)。
            if (statusPanel != null)
                _healthSubscription = _healthAggregator.OnHealthChange.Subscribe(statusPanel.SetHealth);
        }

        public void Dispose()
        {
            _healthSubscription?.Dispose();
            _healthSubscription = null;

            if (_editModePanel != null && _editModeListener != null)
            {
                _editModePanel.RemoveEditModeListener(_editModeListener);
            }
            _editModePanel = null;
            _editModeListener = null;
        }
    }
}
