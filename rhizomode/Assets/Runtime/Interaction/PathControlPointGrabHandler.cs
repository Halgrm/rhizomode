#nullable enable

using System;
using R3;
using Rhizomode.Cameras;
using Rhizomode.Interaction;
using Rhizomode.UI;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

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
        private GrabPoseSolver.GrabPose _grabPose;

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

            var newPosition = GrabPoseSolver.SolvePosition(
                in _grabPose,
                _controllerPose.RayOrigin,
                _controllerPose.ControllerRotation);
            _grabbedVisual.UpdateWorldPosition(newPosition);
        }

        private void OnRightGrab(bool pressed)
        {
            Debug.Log($"[PathGrab] OnRightGrab pressed={pressed} isEditing={_visualManager?.IsEditing} hasHit={_sharedRaycast?.HasHit}");
            if (pressed) TryStartGrab();
            else ReleaseGrab();
        }

        private void TryStartGrab()
        {
            if (_isGrabbing) { Debug.Log("[PathGrab] skip: already grabbing"); return; }
            if (_visualManager == null) { Debug.Log("[PathGrab] skip: visualManager null"); return; }
            if (!_visualManager.IsEditing) { Debug.Log("[PathGrab] skip: not editing"); return; }
            if (_sharedRaycast == null || _controllerPose == null) { Debug.Log("[PathGrab] skip: no raycast or pose"); return; }
            if (!_sharedRaycast.HasHit) { Debug.Log("[PathGrab] skip: no raycast hit"); return; }

            var collider = _sharedRaycast.CurrentHit.collider;
            var visual = _visualManager.GetVisualByCollider(collider);
            if (visual == null)
            {
                Debug.Log($"[PathGrab] skip: collider '{collider?.name}' not a path visual");
                return;
            }

            _grabbedVisual = visual;
            _grabPose = GrabPoseSolver.Capture(
                visual.transform.position,
                visual.transform.rotation,
                _controllerPose.RayOrigin,
                _controllerPose.ControllerRotation);
            _isGrabbing = true;
            Debug.Log($"[PathGrab] grabbed knot={visual.KnotIndex}");
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
