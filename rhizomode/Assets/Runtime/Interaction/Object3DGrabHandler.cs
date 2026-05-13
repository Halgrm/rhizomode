#nullable enable

using System;
using System.Collections.Generic;
using R3;
using Rhizomode.Modules;
using Rhizomode.UI;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.XR
{
    /// <summary>
    /// VR空間の3Dオブジェクト（Object3DProxy）のグラブ移動・スケール変更を管理する。
    /// NodeGrabHandlerと同構造だが、対象はノードパネルではなくObject3DProxyコライダー。
    /// </summary>
    public class Object3DGrabHandler : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 5f), Tooltip("スケール変更速度（/秒）")]
        private float scaleSpeed = 1f;

        private IControllerPose? _controllerPose;
        private ILeftHandRay? _leftHandRay;
        private IDisposable? _subscriptions;

        private readonly Dictionary<Collider, Object3DProxy> _proxyMap = new();

        // グラブ状態
        private bool _isGrabbing;
        private Object3DProxy? _grabbedProxy;
        private Quaternion _grabControllerRotation;
        private Quaternion _grabObjectRotation;
        private Vector3 _grabLocalOffset;
        private bool _isLeftHandGrab;
        private float _stickY;

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
            Observable<Vector2> turnInput)
        {
            _controllerPose = controllerPose;
            _leftHandRay = leftHandRay;

            var d = Disposable.CreateBuilder();

            // 右グリップ
            controllerInput.OnGrab
                .Subscribe(pressed => OnRightGrab(pressed))
                .AddTo(ref d);

            // 左グリップ
            leftHandInput.OnLeftGrab
                .Subscribe(pressed => OnLeftGrab(pressed))
                .AddTo(ref d);

            // 右スティックY軸でスケール変更（グラブ中のみ）
            turnInput
                .Subscribe(v => _stickY = v.y)
                .AddTo(ref d);

            _subscriptions = d.Build();
        }

        /// <summary>Object3DProxyをグラブ対象として登録する。</summary>
        public void Register(Object3DProxy proxy)
        {
            var colliders = proxy.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
                _proxyMap[col] = proxy;
        }

        /// <summary>Object3DProxyのグラブ対象登録を解除する。</summary>
        public void Unregister(Object3DProxy proxy)
        {
            var toRemove = new List<Collider>();
            foreach (var kvp in _proxyMap)
            {
                if (kvp.Value == proxy)
                    toRemove.Add(kvp.Key);
            }
            foreach (var col in toRemove)
                _proxyMap.Remove(col);
        }

        private void Update()
        {
            if (!_isGrabbing || _grabbedProxy == null) return;

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

            // 位置・回転追従
            var rotationDelta = controllerRotation * Quaternion.Inverse(_grabControllerRotation);
            var rotatedOffset = rotationDelta * _grabLocalOffset;
            _grabbedProxy.transform.position = controllerOrigin + rotatedOffset;
            _grabbedProxy.transform.rotation = rotationDelta * _grabObjectRotation;

            // スティックY軸でスケール変更
            if (Mathf.Abs(_stickY) > 0.1f)
            {
                var currentScale = _grabbedProxy.transform.localScale.x;
                var newScale = currentScale + _stickY * scaleSpeed * UnityEngine.Time.deltaTime;
                _grabbedProxy.SetScale(newScale);
            }
        }

        private void OnRightGrab(bool pressed)
        {
            if (pressed)
                TryStartGrab(false);
            else if (!_isLeftHandGrab)
                ReleaseGrab();
        }

        private void OnLeftGrab(bool pressed)
        {
            if (pressed)
                TryStartGrab(true);
            else if (_isLeftHandGrab)
                ReleaseGrab();
        }

        private void TryStartGrab(bool isLeftHand)
        {
            if (_isGrabbing) return;

            Vector3 rayOrigin;
            Vector3 rayDirection;
            Quaternion controllerRotation;

            if (isLeftHand && _leftHandRay != null)
            {
                rayOrigin = _leftHandRay.LeftRayOrigin;
                rayDirection = _leftHandRay.LeftRayDirection;
                controllerRotation = Quaternion.LookRotation(rayDirection);
            }
            else if (!isLeftHand && _controllerPose != null)
            {
                rayOrigin = _controllerPose.RayOrigin;
                rayDirection = _controllerPose.ControllerRotation * Vector3.forward;
                controllerRotation = _controllerPose.ControllerRotation;
            }
            else
            {
                return;
            }

            if (!Physics.Raycast(new Ray(rayOrigin, rayDirection), out var hit, 10f)) return;
            if (!_proxyMap.TryGetValue(hit.collider, out var proxy)) return;

            _grabbedProxy = proxy;
            _grabControllerRotation = controllerRotation;
            _grabObjectRotation = proxy.transform.rotation;
            _grabLocalOffset = proxy.transform.position - rayOrigin;
            _isLeftHandGrab = isLeftHand;
            _isGrabbing = true;
        }

        private void ReleaseGrab()
        {
            _isGrabbing = false;
            _grabbedProxy = null;
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
        }
    }
}
