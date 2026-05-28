#nullable enable

using System;
using R3;
using Rhizomode.Input.Contracts;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Interaction
{
    /// <summary>
    /// Handles one-hand grab and two-hand scale for an <see cref="NdiViewWindow"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WindowGrabHandle : MonoBehaviour
    {
        /// <summary>Maximum pitch applied during roll-locked window grab.</summary>
        public const float MaxPitchDeg = 60f;

        /// <summary>Maximum distance for the left-hand raycast.</summary>
        public const float LeftRayMaxDistance = 3f;

        // Defensive NaN/Inf outer bounds only — the authoritative window-size limit is
        // NdiViewWindow.MinScale / MaxScale (tunable via NdiWindowsRoot). Keep this ceiling
        // generous so a raised MaxScale isn't pre-clamped here.
        private const float MinFiniteScale = 0.01f;
        private const float MaxFiniteScale = 64f;
        private const float MinScaleBaselineDistance = 0.01f;

        [SerializeField] private NdiViewWindow? window;
        [SerializeField] private BoxCollider? boxCollider;

        private IControllerInput? _input;
        private IControllerPose? _pose;
        private ILeftHandInput? _leftInput;
        private ILeftHandRay? _leftRay;
        private SharedRaycastService? _sharedRaycast;
        private IDisposable? _subscriptions;

        private bool _rightGrabbing;
        private bool _leftGrabbing;
        private Vector3 _grabOffsetPosLocal;
        private Quaternion _grabOffsetRotLocal = Quaternion.identity;
        private float _scaleBaselineDist;
        private float _scaleBaselineValue = 1f;

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
                return;
            }

            if (!_rightGrabbing) return;
            _rightGrabbing = false;
            CommitTransform();
        }

        private void OnLeftGrab(bool pressed)
        {
            if (window == null || boxCollider == null) return;
            if (pressed)
            {
                if (_leftRay == null) return;
                if (!IsFinite(_leftRay.LeftRayOrigin) || !IsFinite(_leftRay.LeftRayDirection)) return;
                if (!Physics.Raycast(_leftRay.LeftRayOrigin, _leftRay.LeftRayDirection,
                        out var hit, LeftRayMaxDistance)) return;
                if (hit.collider != boxCollider) return;

                _leftGrabbing = true;
                if (_rightGrabbing) StartTwoHandScale();
                return;
            }

            if (!_leftGrabbing) return;
            _leftGrabbing = false;
            CommitTransform();
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
            if (!IsFinite(_pose.RayOrigin) || !IsFinite(_pose.ControllerRotation)) return;
            if (!IsFinite(window.transform.position) || !IsFinite(window.transform.rotation)) return;

            var invRot = Quaternion.Inverse(_pose.ControllerRotation);
            if (!IsFinite(invRot)) return;

            _grabOffsetPosLocal = invRot * (window.transform.position - _pose.RayOrigin);
            _grabOffsetRotLocal = invRot * window.transform.rotation;
            if (!IsFinite(_grabOffsetPosLocal)) _grabOffsetPosLocal = Vector3.zero;
            if (!IsFinite(_grabOffsetRotLocal)) _grabOffsetRotLocal = Quaternion.identity;
        }

        private void UpdateOneHandGrab()
        {
            if (_pose == null || window == null) return;
            if (!IsFinite(_pose.RayOrigin) || !IsFinite(_pose.ControllerRotation)) return;

            var newPos = _pose.RayOrigin + _pose.ControllerRotation * _grabOffsetPosLocal;
            var newRot = _pose.ControllerRotation * _grabOffsetRotLocal;
            if (!IsFinite(newPos) || !IsFinite(newRot)) return;

            var euler = LockRollClampPitch(newRot.eulerAngles);
            if (!IsFinite(euler)) euler = window.transform.eulerAngles;
            var currentScale = ClampScaleForApply(CurrentUniformScale());
            window.ApplyTransform(newPos, euler, currentScale);
        }

        private void StartTwoHandScale()
        {
            if (_pose == null || _leftRay == null || window == null) return;
            if (!IsFinite(_pose.RayOrigin) || !IsFinite(_leftRay.LeftRayOrigin)) return;

            var dist = Vector3.Distance(_pose.RayOrigin, _leftRay.LeftRayOrigin);
            if (!float.IsFinite(dist)) return;

            _scaleBaselineDist = Mathf.Max(MinScaleBaselineDistance, dist);
            _scaleBaselineValue = ClampScaleForApply(CurrentUniformScale());
        }

        private void UpdateTwoHandScale()
        {
            if (_pose == null || _leftRay == null || window == null) return;
            if (!IsFinite(_pose.RayOrigin) || !IsFinite(_leftRay.LeftRayOrigin)) return;

            var cur = Vector3.Distance(_pose.RayOrigin, _leftRay.LeftRayOrigin);
            var newScale = ComputeTwoHandScale(_scaleBaselineDist, cur, _scaleBaselineValue);
            if (!IsScaleFiniteForApply(newScale)) return;

            var position = window.transform.position;
            var euler = window.transform.eulerAngles;
            if (!IsFinite(position) || !IsFinite(euler)) return;
            window.ApplyTransform(position, euler, newScale);
        }

        private void CommitTransform()
        {
            if (window == null) return;
            var p = window.transform.position;
            var e = window.transform.eulerAngles;
            var s = CurrentUniformScale();
            if (!IsFinite(p) || !IsFinite(e) || !IsScaleFiniteForApply(s)) return;
            window.RaiseTransformChanged(p, e, s);
        }

        private float CurrentUniformScale()
        {
            if (window == null) return 1f;
            var value = window.transform.localScale.x / NdiViewWindow.BaseWidth;
            return float.IsFinite(value) ? value : 1f;
        }

        /// <summary>Locks roll to zero and clamps pitch to the supported window range.</summary>
        internal static Vector3 LockRollClampPitch(Vector3 euler)
        {
            if (!IsFinite(euler)) return Vector3.zero;
            var x = NormalizeToSigned(euler.x);
            x = Mathf.Clamp(x, -MaxPitchDeg, MaxPitchDeg);
            var y = float.IsFinite(euler.y) ? euler.y : 0f;
            return new Vector3(x, y, 0f);
        }

        private static float NormalizeToSigned(float deg)
        {
            if (!float.IsFinite(deg)) return 0f;
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            else if (deg < -180f) deg += 360f;
            return deg;
        }

        /// <summary>Pure two-hand scale formula used by EditMode tests.</summary>
        internal static float ComputeTwoHandScale(float baselineDist, float currentDist, float baselineValue)
        {
            if (!float.IsFinite(currentDist)) return ClampScaleForApply(baselineValue);

            var safeBaseline = float.IsFinite(baselineDist)
                ? Mathf.Max(MinScaleBaselineDistance, baselineDist)
                : MinScaleBaselineDistance;
            var safeBaselineValue = ClampScaleForApply(baselineValue);
            var ratio = currentDist / safeBaseline;
            if (!float.IsFinite(ratio)) return safeBaselineValue;

            return ClampScaleForApply(safeBaselineValue * ratio);
        }

        internal static bool IsFinite(Vector3 v)
            => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        internal static bool IsFinite(Quaternion q)
            => float.IsFinite(q.x) && float.IsFinite(q.y) &&
               float.IsFinite(q.z) && float.IsFinite(q.w);

        private static bool IsScaleFiniteForApply(float scale)
            => float.IsFinite(scale) && scale >= MinFiniteScale && scale <= MaxFiniteScale;

        private static float ClampScaleForApply(float scale)
        {
            if (!float.IsFinite(scale)) return 1f;
            var bounded = Mathf.Clamp(scale, MinFiniteScale, MaxFiniteScale);
            return Mathf.Clamp(bounded, NdiViewWindow.MinScale, NdiViewWindow.MaxScale);
        }
    }
}
