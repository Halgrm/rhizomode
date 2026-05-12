#nullable enable

using System;
using R3;
using Rhizomode.Cameras;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// パス制御点 (PathControlPointVisual) を Right-Grip でグラブ移動するハンドラ。
    /// NodeGrabHandler 同様、コントローラーに 1:1 で位置追従させ、Visual.UpdateWorldPosition
    /// 経由で Spline に書き戻す。
    /// </summary>
    public class PathControlPointGrabHandler : MonoBehaviour
    {
        private IControllerPose? _controllerPose;
        private SharedRaycastService? _sharedRaycast;
        private PathControlPointVisualManager? _visualManager;
        private IDisposable? _subscriptions;

        private bool _isGrabbing;
        private PathControlPointVisual? _grabbedVisual;
        private Vector3 _grabLocalOffset;
        private Quaternion _grabControllerRotation;

        /// <summary>現在グラブ中かどうか。</summary>
        public bool IsGrabbing => _isGrabbing;

        public void Initialize(
            IControllerInput controllerInput,
            IControllerPose controllerPose,
            SharedRaycastService sharedRaycast,
            PathControlPointVisualManager visualManager)
        {
            _controllerPose = controllerPose;
            _sharedRaycast = sharedRaycast;
            _visualManager = visualManager;

            var d = Disposable.CreateBuilder();
            controllerInput.OnGrab
                .Subscribe(pressed => OnRightGrab(pressed))
                .AddTo(ref d);
            _subscriptions = d.Build();
        }

        private void Update()
        {
            if (!_isGrabbing || _grabbedVisual == null || _controllerPose == null) return;

            var origin = _controllerPose.RayOrigin;
            var rotation = _controllerPose.ControllerRotation;
            var rotationDelta = rotation * Quaternion.Inverse(_grabControllerRotation);
            var rotatedOffset = rotationDelta * _grabLocalOffset;
            _grabbedVisual.UpdateWorldPosition(origin + rotatedOffset);
        }

        private void OnRightGrab(bool pressed)
        {
            if (pressed) TryStartGrab();
            else ReleaseGrab();
        }

        private void TryStartGrab()
        {
            if (_isGrabbing) return;
            if (_visualManager == null || !_visualManager.IsEditing) return;
            if (_sharedRaycast == null || _controllerPose == null) return;
            if (!_sharedRaycast.HasHit) return;

            var visual = _visualManager.GetVisualByCollider(_sharedRaycast.CurrentHit.collider);
            if (visual == null) return;

            _grabbedVisual = visual;
            _grabControllerRotation = _controllerPose.ControllerRotation;
            _grabLocalOffset = visual.transform.position - _controllerPose.RayOrigin;
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
