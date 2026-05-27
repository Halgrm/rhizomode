#nullable enable

using System;
using R3;
using Rhizomode.Input.Contracts;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Interaction
{
    /// <summary>
    /// <see cref="NdiViewWindow"/> を VR で grab + 2-hand scale するハンドラ。
    /// </summary>
    /// <remarks>
    /// <para>Plan v0.3 §WindowGrabHandle 実装 (Phase F3)。</para>
    /// <para>仕組み:</para>
    /// <list type="bullet">
    ///   <item>1-hand grab (右 Grip + window collider 上で raycast hit):
    ///         controller の pose offset を保存し、リリースまで 1:1 で追従。
    ///         roll は 0 lock、pitch は ±60° clamp。</item>
    ///   <item>2-hand grab (右 + 左の両 Grip + 両 ray が window collider hit):
    ///         両 controller の距離比で uniform scale。translate / rotate は
    ///         凍結 (現案、Plan §2-hand scale translate suppression)。
    ///         scale clamp は MinScale=0.1 / MaxScale=4.0。</item>
    ///   <item>grab end (両手 release): <see cref="NdiViewWindow.RaiseTransformChanged"/>
    ///         を呼んで presenter → node.SetWindowTransform に commit (cue save 経路)。</item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WindowGrabHandle : MonoBehaviour
    {
        /// <summary>roll lock + pitch clamp の上限 (Codex review §lock policy 準拠)。</summary>
        public const float MaxPitchDeg = 60f;

        /// <summary>左手 raycast の最大距離 (NodeGrabHandler と揃え)。</summary>
        public const float LeftRayMaxDistance = 3f;

        [SerializeField] private NdiViewWindow? window;
        [SerializeField] private BoxCollider? boxCollider;

        // injected deps (NdiWindowsRoot から Initialize で push される)
        private IControllerInput? _input;
        private IControllerPose? _pose;
        private ILeftHandInput? _leftInput;
        private ILeftHandRay? _leftRay;
        private SharedRaycastService? _sharedRaycast;
        private IDisposable? _subscriptions;

        // grab state
        private bool _rightGrabbing;
        private bool _leftGrabbing;
        // 1-hand offset (controller frame で window pose を保持 → 毎フレーム再計算)
        private Vector3 _grabOffsetPosLocal;
        private Quaternion _grabOffsetRotLocal;
        // 2-hand scale baseline (両手の距離 + 当時の uniform scale)
        private float _scaleBaselineDist;
        private float _scaleBaselineValue;

        /// <summary>NdiWindowsRoot から呼ばれる。VContainer 経由で取得した依存を受け取る。</summary>
        internal void Initialize(
            IControllerInput input,
            IControllerPose pose,
            ILeftHandInput leftInput,
            ILeftHandRay leftRay,
            SharedRaycastService sharedRaycast)
        {
            if (_subscriptions != null) return;
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _pose = pose ?? throw new ArgumentNullException(nameof(pose));
            _leftInput = leftInput ?? throw new ArgumentNullException(nameof(leftInput));
            _leftRay = leftRay ?? throw new ArgumentNullException(nameof(leftRay));
            _sharedRaycast = sharedRaycast ?? throw new ArgumentNullException(nameof(sharedRaycast));

            var d = Disposable.CreateBuilder();
            input.OnGrab.Subscribe(OnRightGrab).AddTo(ref d);
            leftInput.OnLeftGrab.Subscribe(OnLeftGrab).AddTo(ref d);
            _subscriptions = d.Build();
        }

        private void Awake()
        {
            if (window == null) window = GetComponent<NdiViewWindow>();
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
            _subscriptions = null;
        }

        private void OnRightGrab(bool pressed)
        {
            if (window == null || boxCollider == null) return;
            if (pressed)
            {
                if (_sharedRaycast != null && _sharedRaycast.HasHit &&
                    _sharedRaycast.CurrentHit.collider == boxCollider)
                {
                    _rightGrabbing = true;
                    CacheOneHandOffset();
                    if (_leftGrabbing) StartTwoHandScale();
                }
            }
            else
            {
                if (_rightGrabbing)
                {
                    _rightGrabbing = false;
                    CommitTransform();
                }
            }
        }

        private void OnLeftGrab(bool pressed)
        {
            if (window == null || boxCollider == null) return;
            if (pressed)
            {
                // 左手 ray は SharedRaycastService に乗っていない (右手専用) ため Physics.Raycast 直叩き。
                if (_leftRay == null) return;
                if (!Physics.Raycast(_leftRay.LeftRayOrigin, _leftRay.LeftRayDirection,
                        out var hit, LeftRayMaxDistance)) return;
                if (hit.collider != boxCollider) return;
                _leftGrabbing = true;
                if (_rightGrabbing) StartTwoHandScale();
            }
            else
            {
                if (_leftGrabbing)
                {
                    _leftGrabbing = false;
                    CommitTransform();
                }
            }
        }

        private void Update()
        {
            if (window == null || _pose == null) return;
            if (_rightGrabbing && _leftGrabbing) UpdateTwoHandScale();
            else if (_rightGrabbing) UpdateOneHandGrab();
        }

        private void CacheOneHandOffset()
        {
            if (_pose == null || window == null) return;
            var invRot = Quaternion.Inverse(_pose.ControllerRotation);
            _grabOffsetPosLocal = invRot * (window.transform.position - _pose.RayOrigin);
            _grabOffsetRotLocal = invRot * window.transform.rotation;
        }

        private void UpdateOneHandGrab()
        {
            if (_pose == null || window == null) return;
            var newPos = _pose.RayOrigin + _pose.ControllerRotation * _grabOffsetPosLocal;
            var newRot = _pose.ControllerRotation * _grabOffsetRotLocal;
            var euler = LockRollClampPitch(newRot.eulerAngles);
            var currentScale = CurrentUniformScale();
            window.ApplyTransform(newPos, euler, currentScale);
        }

        private void StartTwoHandScale()
        {
            if (_pose == null || _leftRay == null || window == null) return;
            _scaleBaselineDist = Mathf.Max(0.01f, Vector3.Distance(_pose.RayOrigin, _leftRay.LeftRayOrigin));
            _scaleBaselineValue = CurrentUniformScale();
        }

        private void UpdateTwoHandScale()
        {
            if (_pose == null || _leftRay == null || window == null) return;
            var cur = Vector3.Distance(_pose.RayOrigin, _leftRay.LeftRayOrigin);
            var raw = _scaleBaselineValue * (cur / _scaleBaselineDist);
            var newScale = Mathf.Clamp(raw, NdiViewWindow.MinScale, NdiViewWindow.MaxScale);
            // 2-hand scale 中は translate / rotate を凍結 (Plan §UX 判断)
            window.ApplyTransform(window.transform.position, window.transform.eulerAngles, newScale);
        }

        private void CommitTransform()
        {
            if (window == null) return;
            var p = window.transform.position;
            var e = window.transform.eulerAngles;
            var s = CurrentUniformScale();
            window.RaiseTransformChanged(p, e, s);
        }

        private float CurrentUniformScale()
        {
            if (window == null) return 1f;
            return window.transform.localScale.x / NdiViewWindow.BaseWidth;
        }

        /// <summary>roll を 0 にし、pitch を ±<see cref="MaxPitchDeg"/> に clamp する。yaw は自由。</summary>
        internal static Vector3 LockRollClampPitch(Vector3 euler)
        {
            // Unity の eulerAngles は [0, 360)。clamp は signed (-180, 180] で行う。
            var x = NormalizeToSigned(euler.x);
            x = Mathf.Clamp(x, -MaxPitchDeg, MaxPitchDeg);
            return new Vector3(x, euler.y, 0f);
        }

        private static float NormalizeToSigned(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            else if (deg < -180f) deg += 360f;
            return deg;
        }

        /// <summary>2-hand scale formula (test 用 pure func)。
        /// baseline=0 ガード + MinScale/MaxScale clamp 込み。</summary>
        internal static float ComputeTwoHandScale(float baselineDist, float currentDist, float baselineValue)
        {
            var safeBaseline = Mathf.Max(0.01f, baselineDist);
            var ratio = currentDist / safeBaseline;
            return Mathf.Clamp(baselineValue * ratio, NdiViewWindow.MinScale, NdiViewWindow.MaxScale);
        }
    }
}
