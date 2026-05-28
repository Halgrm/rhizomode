#nullable enable

using System;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// Runtime world-space window that displays NDI receiver output.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public sealed class NdiViewWindow : MonoBehaviour
    {
        /// <summary>Expected video aspect ratio.</summary>
        public const float Aspect = 16f / 9f;

        /// <summary>Base width in meters when scale is 1.</summary>
        public const float BaseWidth = 1.0f;

        /// <summary>Default minimum window scale (0.1 = 0.1m wide).</summary>
        public const float DefaultMinScale = 0.1f;

        /// <summary>Default maximum window scale (4.0 = 4m wide / 2.25m tall at 16:9).</summary>
        public const float DefaultMaxScale = 4.0f;

        /// <summary>
        /// Minimum window scale (1.0 = <see cref="BaseWidth"/> m). Mutable so the limit can be
        /// tuned from <c>NdiWindowsRoot</c> Inspector without recompiling. Global to all windows.
        /// </summary>
        public static float MinScale = DefaultMinScale;

        /// <summary>
        /// Maximum window scale (1.0 = <see cref="BaseWidth"/> m). Tune via <c>NdiWindowsRoot</c>.
        /// At scale N the window is N×16:9 meters (N=4 → 4m × 2.25m). Global to all windows.
        /// </summary>
        public static float MaxScale = DefaultMaxScale;

        private static Mesh? _sharedQuadMesh;

        private MeshRenderer? _renderer;
        private BoxCollider? _collider;
        private bool _hasLoggedInvalidTransform;

        /// <summary>Renderer that displays this window's NDI target texture.</summary>
        public MeshRenderer Renderer => _renderer!;

        /// <summary>Collider used by VR ray and grab interaction.</summary>
        public BoxCollider Collider => _collider!;

        /// <summary>Raised when grab interaction commits a new transform.</summary>
        public event Action<Vector3, Vector3, float>? OnTransformChanged;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<BoxCollider>();
            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh == null) mf.sharedMesh = GetOrCreateSharedQuad();

            _collider.center = Vector3.zero;
            _collider.size = new Vector3(1f, 1f / Aspect, 0.01f);
            _renderer.enabled = false;
        }

        /// <summary>Applies a validated transform supplied by presenter, cue load, or grab interaction.</summary>
        public void ApplyTransform(Vector3 position, Vector3 eulerAngles, float scale)
        {
            if (!CanApplyTransform(position, eulerAngles, scale, out var rotation, out var clamped))
                return;

            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = new Vector3(BaseWidth * clamped, BaseWidth * clamped / Aspect, 1f);
        }

        /// <summary>Enables or disables the renderer after receiver binding.</summary>
        public void SetRendererActive(bool active)
        {
            if (_renderer != null) _renderer.enabled = active;
        }

        internal void RaiseTransformChanged(Vector3 position, Vector3 eulerAngles, float scale)
        {
            if (!IsFinite(position) || !IsFinite(eulerAngles) || !float.IsFinite(scale)) return;

            try { OnTransformChanged?.Invoke(position, eulerAngles, scale); }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiViewWindow] OnTransformChanged handler threw: {e.Message}");
            }
        }

        private bool CanApplyTransform(
            Vector3 position,
            Vector3 eulerAngles,
            float scale,
            out Quaternion rotation,
            out float clampedScale)
        {
            rotation = Quaternion.identity;
            clampedScale = 1f;
            if (!IsFinite(position) || !IsFinite(eulerAngles) || !float.IsFinite(scale))
                return WarnInvalidTransform();

            clampedScale = Mathf.Clamp(scale, MinScale, MaxScale);
            var localScale = new Vector3(BaseWidth * clampedScale, BaseWidth * clampedScale / Aspect, 1f);
            rotation = Quaternion.Euler(eulerAngles);
            if (!IsFinite(rotation) || !IsFinite(localScale))
                return WarnInvalidTransform();

            return true;
        }

        private bool WarnInvalidTransform()
        {
            if (!_hasLoggedInvalidTransform)
            {
                Debug.LogWarning("[NdiViewWindow] Skipped non-finite transform.");
                _hasLoggedInvalidTransform = true;
            }

            return false;
        }

        private static bool IsFinite(Vector3 v)
            => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        private static bool IsFinite(Quaternion q)
            => float.IsFinite(q.x) && float.IsFinite(q.y) &&
               float.IsFinite(q.z) && float.IsFinite(q.w);

        private static Mesh GetOrCreateSharedQuad()
        {
            if (_sharedQuadMesh != null) return _sharedQuadMesh;

            var m = new Mesh { name = "NdiViewWindow_Quad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.normals = new[]
            {
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
            };
            m.RecalculateBounds();
            _sharedQuadMesh = m;
            return m;
        }
    }
}
