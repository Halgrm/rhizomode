#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// Registry and factory root for runtime NDI view windows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NdiWindowsRoot : MonoBehaviour
    {
        [SerializeField] private NdiViewWindow? windowPrefab;
        [SerializeField] private Material? windowMaterial;

        [Header("Window Scale Limits (2-hand scale)")]
        [Tooltip("最小スケール (1.0 = 1m 幅)。2 手スケールの下限。")]
        [SerializeField, Min(0.01f)] private float minWindowScale = NdiViewWindow.DefaultMinScale;
        [Tooltip("最大スケール (1.0 = 1m 幅、16:9)。例: 8 = 最大 8m 幅 × 4.5m 高。2 手スケールの上限。")]
        [SerializeField, Min(0.1f)] private float maxWindowScale = 8.0f;

        private const float DefaultForwardDistance = 1.5f;
        private const float DefaultVerticalOffset = 0.2f;
        private const float HmdForwardMinSqrMagnitude = 1e-4f;
        private static readonly Color FallbackColor = new(1f, 1f, 1f, 1f);

        private readonly Dictionary<string, NdiViewWindow> _windows = new();

        private static Material? _fallbackMaterial;

        /// <summary>Raised after a new window is created so interaction wiring can attach grab handles.</summary>
        public event Action<NdiViewWindow>? OnWindowSpawned;

        /// <summary>Creates or returns the registered window for a node id.</summary>
        public NdiViewWindow CreateFor(string nodeId, INdiReceiverNode node, INdiViewWindowState state)
        {
            if (string.IsNullOrEmpty(nodeId)) throw new ArgumentException("nodeId required", nameof(nodeId));
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (_windows.TryGetValue(nodeId, out var existing) && existing != null)
                return existing;

            var window = windowPrefab != null
                ? Instantiate(windowPrefab, transform)
                : CreateDefaultWindow(transform);
            window.name = $"NdiViewWindow_{nodeId}";

            var renderer = window.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.material = CreateWindowMaterialInstance();

            ApplyInitialPose(window, nodeId, state);

            _windows[nodeId] = window;
            try { OnWindowSpawned?.Invoke(window); }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiWindowsRoot] OnWindowSpawned subscriber threw: {e.Message}");
            }

            return window;
        }

        /// <summary>Destroys and unregisters the window for a node id.</summary>
        public void DestroyFor(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            if (!_windows.TryGetValue(nodeId, out var window)) return;

            _windows.Remove(nodeId);
            if (window == null) return;
            if (Application.isPlaying) Destroy(window.gameObject);
            else DestroyImmediate(window.gameObject);
        }

        /// <summary>Destroys all registered windows.</summary>
        public void DestroyAll()
        {
            foreach (var pair in _windows)
            {
                if (pair.Value == null) continue;
                if (Application.isPlaying) Destroy(pair.Value.gameObject);
                else DestroyImmediate(pair.Value.gameObject);
            }

            _windows.Clear();
        }

        /// <summary>Returns the registered window for a node id.</summary>
        public bool TryGet(string nodeId, out NdiViewWindow window)
        {
            if (_windows.TryGetValue(nodeId, out var registered) && registered != null)
            {
                window = registered;
                return true;
            }

            window = default!;
            return false;
        }

        internal int Count => _windows.Count;

        private void Awake()
        {
            ApplyScaleLimits();
        }

        private void OnValidate()
        {
            // Inspector で値を変えたら即反映 (Play 中も). 静的 limit は domain reload で既定に
            // 戻るため Awake でも適用する。
            ApplyScaleLimits();
        }

        /// <summary>Inspector の min/max を <see cref="NdiViewWindow"/> の静的 scale limit に反映する。</summary>
        private void ApplyScaleLimits()
        {
            float min = Mathf.Max(0.01f, minWindowScale);
            float max = Mathf.Max(min, maxWindowScale);
            NdiViewWindow.MinScale = min;
            NdiViewWindow.MaxScale = max;
        }

        private void OnDestroy()
        {
            DestroyAll();
        }

        private void ApplyInitialPose(NdiViewWindow window, string nodeId, INdiViewWindowState state)
        {
            window.SetRendererActive(false);
            if (state.HasExplicitWindowTransform)
            {
                window.ApplyTransform(state.WindowPosition, state.WindowEulerAngles, state.WindowScale);
                return;
            }

            var (hmdPos, hmdForward, hmdRight) = GetSafeHmdFrame();
            var basePos = hmdPos + hmdForward * DefaultForwardDistance +
                          Vector3.up * DefaultVerticalOffset;
            var pos = basePos + NdiViewWindowMath.CascadeOffset(nodeId, hmdForward, hmdRight);
            var rot = ComputeFacingRotation(hmdPos, pos);
            window.ApplyTransform(pos, rot.eulerAngles, 1.0f);
        }

        private static (Vector3 Position, Vector3 Forward, Vector3 Right) GetSafeHmdFrame()
        {
            var cam = Camera.main;
            if (cam == null)
                return (new Vector3(0f, 1.5f, 0f), Vector3.forward, Vector3.right);

            var hmdPos = cam.transform.position;
            var hmdForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (!IsFinite(hmdPos) || !IsFinite(hmdForward) ||
                hmdForward.sqrMagnitude < HmdForwardMinSqrMagnitude)
            {
                return (new Vector3(0f, 1.5f, 0f), Vector3.forward, Vector3.right);
            }

            hmdForward.Normalize();
            var hmdRight = Vector3.Cross(Vector3.up, hmdForward);
            if (!IsFinite(hmdRight) || hmdRight.sqrMagnitude < HmdForwardMinSqrMagnitude)
                hmdRight = Vector3.right;

            return (hmdPos, hmdForward, hmdRight.normalized);
        }

        private static Quaternion ComputeFacingRotation(Vector3 hmdPos, Vector3 pos)
        {
            var lookDir = hmdPos - pos;
            lookDir.y = 0f;
            if (!IsFinite(lookDir) || lookDir.sqrMagnitude <= HmdForwardMinSqrMagnitude)
                return Quaternion.identity;

            return Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }

        private static NdiViewWindow CreateDefaultWindow(Transform parent)
        {
            var go = new GameObject("NdiViewWindow_Default");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<BoxCollider>();
            return go.AddComponent<NdiViewWindow>();
        }

        private Material ResolveWindowMaterial()
        {
            if (windowMaterial != null && windowMaterial.HasProperty("_BaseMap"))
                return windowMaterial;

            return EnsureFallbackMaterial();
        }

        private Material CreateWindowMaterialInstance()
        {
            var source = ResolveWindowMaterial();
            return new Material(source) { name = source.name };
        }

        private static Material EnsureFallbackMaterial()
        {
            if (_fallbackMaterial != null) return _fallbackMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Hidden/InternalErrorShader");
            _fallbackMaterial = new Material(shader) { name = "NdiWindow_FallbackMat" };
            if (_fallbackMaterial.HasProperty("_BaseMap"))
                _fallbackMaterial.SetTexture("_BaseMap", Texture2D.blackTexture);
            if (_fallbackMaterial.HasProperty("_BaseColor"))
                _fallbackMaterial.SetColor("_BaseColor", FallbackColor);
            if (_fallbackMaterial.HasProperty("_Color"))
                _fallbackMaterial.SetColor("_Color", FallbackColor);
            return _fallbackMaterial;
        }

        private static bool IsFinite(Vector3 v)
            => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
