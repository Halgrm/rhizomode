#nullable enable

using System;
using R3;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// ノードグラブ移動・回転を管理する。左右グリップでノードを掴み、離すまで追従させる。
    /// Rector/myine方式: コントローラーの位置＋回転を1:1で追従（trackPosition + trackRotation）。
    /// </summary>
    public class NodeGrabHandler : MonoBehaviour
    {
        [SerializeField, Range(0.5f, 10f), Tooltip("左手レイの最大グラブ距離（メートル）")]
        private float leftRayMaxDistance = 3f;

        private IControllerPose? _controllerPose;
        private ILeftHandRay? _leftHandRay;
        private SharedRaycastService? _sharedRaycast;
        private NodeVisualManager? _visualManager;
        private EdgeVisualManager? _edgeVisualManager;
        private IDisposable? _subscriptions;

        // グラブ状態
        private bool _isGrabbing;
        private string? _grabbedNodeId;
        private Quaternion _grabControllerRotation;
        private Quaternion _grabNodeRotation;
        private Vector3 _grabLocalOffset;
        private bool _isLeftHandGrab;

        /// <summary>現在グラブ中かどうか。</summary>
        public bool IsGrabbing => _isGrabbing;

        /// <summary>
        /// 依存関係を設定し、入力を購読する。
        /// </summary>
        public void Initialize(
            IControllerInput controllerInput,
            IControllerPose controllerPose,
            ILeftHandRay leftHandRay,
            ILeftHandInput leftHandInput,
            SharedRaycastService sharedRaycast,
            NodeVisualManager visualManager,
            EdgeVisualManager edgeVisualManager)
        {
            _controllerPose = controllerPose;
            _leftHandRay = leftHandRay;
            _sharedRaycast = sharedRaycast;
            _visualManager = visualManager;
            _edgeVisualManager = edgeVisualManager;

            var d = Disposable.CreateBuilder();

            // 右グリップ
            controllerInput.OnGrab
                .Subscribe(pressed => OnRightGrab(pressed))
                .AddTo(ref d);

            // 左グリップ
            leftHandInput.OnLeftGrab
                .Subscribe(pressed => OnLeftGrab(pressed))
                .AddTo(ref d);

            _subscriptions = d.Build();
        }

        private void Update()
        {
            if (!_isGrabbing || _visualManager == null) return;
            if (_grabbedNodeId == null) return;

            var visual = _visualManager.GetVisual(_grabbedNodeId);
            if (visual == null)
            {
                ReleaseGrab();
                return;
            }

            Vector3 controllerOrigin;
            Quaternion controllerRotation;

            if (_isLeftHandGrab && _leftHandRay != null)
            {
                controllerOrigin = _leftHandRay.LeftRayOrigin;
                controllerRotation = Quaternion.LookRotation(_leftHandRay.LeftRayDirection);
            }
            else if (_controllerPose != null)
            {
                controllerOrigin = _controllerPose.RayOrigin;
                controllerRotation = _controllerPose.ControllerRotation;
            }
            else
            {
                return;
            }

            var rotationDelta = controllerRotation * Quaternion.Inverse(_grabControllerRotation);
            var rotatedOffset = rotationDelta * _grabLocalOffset;
            visual.transform.position = controllerOrigin + rotatedOffset;
            visual.transform.rotation = rotationDelta * _grabNodeRotation;

            _edgeVisualManager?.MarkNodeDirty(_grabbedNodeId);
        }

        private void OnRightGrab(bool pressed)
        {
            if (pressed)
                TryStartGrabFromRightHand();
            else if (!_isLeftHandGrab)
                ReleaseGrab();
        }

        private void OnLeftGrab(bool pressed)
        {
            if (pressed)
                TryStartGrabFromLeftHand();
            else if (_isLeftHandGrab)
                ReleaseGrab();
        }

        private void TryStartGrabFromRightHand()
        {
            if (_isGrabbing) return;
            if (_sharedRaycast == null || _visualManager == null || _controllerPose == null) return;
            if (!_sharedRaycast.HasHit) return;

            var visual = _visualManager.GetVisualByCollider(_sharedRaycast.CurrentHit.collider);
            if (visual?.Node == null) return;

            _grabbedNodeId = visual.Node.Id;
            _grabControllerRotation = _controllerPose.ControllerRotation;
            _grabNodeRotation = visual.transform.rotation;
            _grabLocalOffset = visual.transform.position - _controllerPose.RayOrigin;
            _isLeftHandGrab = false;
            _isGrabbing = true;
        }

        private void TryStartGrabFromLeftHand()
        {
            if (_isGrabbing) return;
            if (_leftHandRay == null || _visualManager == null) return;

            var ray = new Ray(_leftHandRay.LeftRayOrigin, _leftHandRay.LeftRayDirection);
            if (!Physics.Raycast(ray, out var hit, leftRayMaxDistance)) return;

            var visual = _visualManager.GetVisualByCollider(hit.collider);
            if (visual?.Node == null) return;

            _grabbedNodeId = visual.Node.Id;
            _grabControllerRotation = Quaternion.LookRotation(_leftHandRay.LeftRayDirection);
            _grabNodeRotation = visual.transform.rotation;
            _grabLocalOffset = visual.transform.position - _leftHandRay.LeftRayOrigin;
            _isLeftHandGrab = true;
            _isGrabbing = true;
        }

        private void ReleaseGrab()
        {
            _isGrabbing = false;
            _grabbedNodeId = null;
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
        }
    }
}
