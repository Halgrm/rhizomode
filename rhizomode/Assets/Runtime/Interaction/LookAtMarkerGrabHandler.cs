#nullable enable

using System;
using R3;
using Rhizomode.Cameras;
using Rhizomode.Interaction;
using Rhizomode.UI;
using UnityEngine;

using Rhizomode.Input.Contracts;

namespace Rhizomode.XR
{
    /// <summary>
    /// Phase 2-A (2026-05-18): VR で配置した <see cref="LookAtMarkerVisual"/> を Right-Grip で grab 移動する handler。
    /// <see cref="PathControlPointGrabHandler"/> と同構造で、対象 manager が異なるだけ。
    /// </summary>
    /// <remarks>
    /// edit mode (<see cref="LookAtMarkerVisualManager.IsEditing"/>) 中のみ grab を受け付け、
    /// それ以外は raycast hit が marker collider に当たっても無視する (誤掴み防止)。
    /// </remarks>
    public class LookAtMarkerGrabHandler : MonoBehaviour
    {
        private IControllerPose? _controllerPose;
        private SharedRaycastService? _sharedRaycast;
        private LookAtMarkerVisualManager? _manager;
        private IDisposable? _subscriptions;

        private bool _isGrabbing;
        private LookAtMarkerVisual? _grabbedVisual;
        private GrabPoseSolver.GrabPose _grabPose;

        public bool IsGrabbing => _isGrabbing;

        public void Initialize(
            IControllerInput controllerInput,
            IControllerPose controllerPose,
            SharedRaycastService sharedRaycast,
            LookAtMarkerVisualManager manager)
        {
            _controllerPose = controllerPose;
            _sharedRaycast = sharedRaycast;
            _manager = manager;

            var d = Disposable.CreateBuilder();
            controllerInput.OnGrab
                .Subscribe(pressed => OnRightGrab(pressed))
                .AddTo(ref d);
            _subscriptions = d.Build();
        }

        private void Update()
        {
            if (!_isGrabbing || _grabbedVisual == null || _controllerPose == null) return;

            var newPosition = GrabPoseSolver.SolvePosition(
                in _grabPose,
                _controllerPose.RayOrigin,
                _controllerPose.ControllerRotation);
            _grabbedVisual.UpdateWorldPosition(newPosition);
        }

        private void OnRightGrab(bool pressed)
        {
            if (pressed) TryStartGrab();
            else ReleaseGrab();
        }

        private void TryStartGrab()
        {
            if (_isGrabbing) return;
            if (_manager == null || !_manager.IsEditing) return;
            if (_sharedRaycast == null || _controllerPose == null) return;
            if (!_sharedRaycast.HasHit) return;

            var visual = _manager.GetVisualByCollider(_sharedRaycast.CurrentHit.collider);
            if (visual == null) return;

            _grabbedVisual = visual;
            _grabPose = GrabPoseSolver.Capture(
                visual.transform.position,
                visual.transform.rotation,
                _controllerPose.RayOrigin,
                _controllerPose.ControllerRotation);
            _isGrabbing = true;
        }

        private void ReleaseGrab()
        {
            _isGrabbing = false;
            _grabbedVisual = null;
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
        }
    }
}
