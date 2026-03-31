#nullable enable

using System;
using R3;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// ノードのグラブ移動を管理する。右グリップで掴み、コントローラーと共に移動する。
    /// </summary>
    public class NodeGrabHandler : MonoBehaviour
    {
        private const float RayMaxDistance = 5f;

        private IRayProvider? _rayProvider;
        private NodeVisualManager? _visualManager;
        private IDisposable? _grabSubscription;

        // グラブ状態
        private bool _isGrabbing;
        private NodeVisualController? _grabbedVisual;
        private float _grabDistance;
        private Vector3 _grabOffset;

        /// <summary>
        /// 依存関係を設定し、入力を購読する。
        /// </summary>
        public void Initialize(
            IControllerInput controllerInput,
            IRayProvider rayProvider,
            NodeVisualManager visualManager)
        {
            _rayProvider = rayProvider;
            _visualManager = visualManager;

            _grabSubscription = controllerInput.OnGrab
                .Subscribe(OnGrabChanged);
        }

        private void Update()
        {
            if (!_isGrabbing || _rayProvider == null || _grabbedVisual == null) return;

            UpdateGrabbedPosition();
        }

        private void OnGrabChanged(bool pressed)
        {
            if (pressed)
                TryStartGrab();
            else
                EndGrab();
        }

        private void TryStartGrab()
        {
            if (_rayProvider == null) return;

            var ray = new Ray(_rayProvider.RayOrigin, _rayProvider.RayDirection);
            if (!Physics.Raycast(ray, out var hit, RayMaxDistance)) return;

            var visual = hit.collider.GetComponent<NodeVisualController>();
            if (visual == null) return;

            _isGrabbing = true;
            _grabbedVisual = visual;
            _grabDistance = hit.distance;
            _grabOffset = visual.transform.position - hit.point;
        }

        private void UpdateGrabbedPosition()
        {
            if (_rayProvider == null || _grabbedVisual == null) return;

            var targetPoint = _rayProvider.RayOrigin
                + _rayProvider.RayDirection * _grabDistance;
            _grabbedVisual.transform.position = targetPoint + _grabOffset;
        }

        private void EndGrab()
        {
            _isGrabbing = false;
            _grabbedVisual = null;
        }

        private void OnDestroy()
        {
            _grabSubscription?.Dispose();
        }
    }
}
