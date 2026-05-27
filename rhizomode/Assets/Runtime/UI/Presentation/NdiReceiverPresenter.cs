#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.UI.Contracts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.UI
{
    /// <summary>
    /// VR visual presenter for <see cref="INdiReceiverNode"/>.
    /// </summary>
    /// <remarks>
    /// Klak.NDI calls stay inside <c>KLAK_NDI</c>. When NDI is unavailable, the
    /// presenter keeps the node visual alive and reports health feedback.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NdiReceiverPresenter : MonoBehaviour
    {
        private const string MainTexProperty = "_BaseMap";
        private const float PreviewWorldWidth = 0.32f;
        private const float PreviewWorldHeight = 0.18f;
        private const float DefaultNodePanelWorldHeight = 0.12f;
        private const float MinParentScale = 0.0001f;
        private const float ColliderDepth = 0.01f;
        private const float SourceAutoPickPollSec = 1.5f;
        private const string NdiResourcesPath = "KlakNdi/NdiResources";

        // NDI source names arrive from `Klak.Ndi.NdiFinder.sourceNames`, which lists names
        // broadcast over the LAN by remote senders — untrusted network input. We clamp
        // length and strip control / DEL characters at every intake point before the value
        // flows into KlakNDI's native side, the claim set, health messages, or paramsJson.
        internal const int MaxSourceNameLength = 256;

        private static readonly HashSet<string> _claimedSources = new();

        private INdiReceiverNode? _node;
        private GameObject? _previewQuad;
        private MeshRenderer? _previewRenderer;
        private Material? _previewMaterial;
        private BoxCollider? _previewCollider;
        private NodeVisualManager? _registeredManager;
        private NdiReceiverHealth? _health;

#if KLAK_NDI
        private Klak.Ndi.NdiReceiver? _receiver;
        private Klak.Ndi.NdiResources? _ndiResources;
#endif

        private float _nextAutoPickAt;
        private float _nextSourceHealthAt;
        private string _claimedSourceName = "";
        private static Shader? CachedUnlitShader;
        private static Mesh? SharedPreviewQuad;

        [Inject]
        private void Construct(NdiReceiverHealth health)
        {
            _health = health;
        }

        /// <summary>
        /// The node this presenter is currently bound to, or null when detached.
        /// Used by <c>NodeVisualController</c> to detect a rebind to a different
        /// receiver and trigger detach + reattach (instead of leaking the old binding).
        /// </summary>
        internal INdiReceiverNode? BoundNode => _node;

        /// <summary>Attach this presenter to an NDI receiver node.</summary>
        public void Attach(INdiReceiverNode node)
        {
            if (_node != null) return;
            TryInjectDependencies();
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _node.OnSourceNameChanged += HandleSourceNameChanged;

            CreatePreviewQuad();
#if KLAK_NDI
            CreateReceiver();
#else
            Debug.LogWarning("[NdiReceiverPresenter] KLAK_NDI define not set. NDI receive disabled.");
            ReportReceiverUnavailable("KLAK_NDI define not set");
#endif

            // Seed claim set + receiver from any pre-populated SourceName (loaded graphs).
            // Re-using HandleSourceNameChanged guarantees the same sanitize → Claim →
            // apply pipeline as runtime changes, so loaded sources participate in
            // collision avoidance instead of bypassing _claimedSources.
            HandleSourceNameChanged(_node.SourceName);
        }

        /// <summary>Detach from the node and destroy runtime receiver objects.</summary>
        public void Detach()
        {
            if (_node != null)
            {
                _node.OnSourceNameChanged -= HandleSourceNameChanged;
                _node = null;
            }

            ReleaseClaim();
            UnregisterColliderWithVisualManager();

#if KLAK_NDI
            if (_receiver != null)
            {
                Destroy(_receiver);
                _receiver = null;
            }
#endif
            ReportReceiverStopped();
            if (_previewMaterial != null) { Destroy(_previewMaterial); _previewMaterial = null; }
            if (_previewQuad != null) { Destroy(_previewQuad); _previewQuad = null; }
            _previewRenderer = null;
        }

        private void Awake() => TryInjectDependencies();

        private void OnDestroy() => Detach();

        private void Update()
        {
#if KLAK_NDI
            if (_node == null) return;
            if (_receiver == null)
            {
                ReportReceiverUnavailable("Receiver component unavailable");
                return;
            }

            if (!string.IsNullOrEmpty(_node.SourceName))
            {
                PollSourceHealth(_node.SourceName);
                return;
            }

            TryAutoPickSource();
#endif
        }

        private void HandleSourceNameChanged(string newName)
        {
            // Sanitize first (the value may have arrived from paramsJson load or a future UI),
            // then re-claim so manual / loaded source names participate in the no-collision set
            // alongside auto-picks. Empty name → release any prior claim.
            var sanitized = SanitizeSourceName(newName);
            if (string.IsNullOrEmpty(sanitized)) ReleaseClaim();
            else Claim(sanitized);
#if KLAK_NDI
            ApplySourceNameToReceiver(sanitized);
#endif
        }

        /// <summary>
        /// Clamp length and strip control / DEL characters from an untrusted NDI source name.
        /// Idempotent and allocation-free for already-clean input.
        /// </summary>
        internal static string SanitizeSourceName(string? name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var src = name!;
            var max = Math.Min(src.Length, MaxSourceNameLength);
            // Fast-path: scan and only allocate if we find something to change.
            var needsCleanup = src.Length > MaxSourceNameLength;
            if (!needsCleanup)
            {
                for (int i = 0; i < max; i++)
                {
                    var c = src[i];
                    if (c < 0x20 || c == 0x7F) { needsCleanup = true; break; }
                }
            }
            if (!needsCleanup) return src;
            var sb = new System.Text.StringBuilder(max);
            for (int i = 0; i < max; i++)
            {
                var c = src[i];
                if (c < 0x20 || c == 0x7F) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private void UnregisterColliderWithVisualManager()
        {
            if (_registeredManager != null && _previewCollider != null)
                _registeredManager.UnregisterAuxiliaryCollider(_previewCollider);
            _registeredManager = null;
            _previewCollider = null;
        }

#if KLAK_NDI
        private void ApplySourceNameToReceiver(string name)
        {
            if (_receiver == null) return;
            // Belt + suspenders: even if upstream sanitized, re-sanitize at the native boundary
            // so any future caller cannot bypass cleaning on its way to KlakNDI.
            var clean = SanitizeSourceName(name);
            try
            {
                _receiver.ndiName = clean;
                _nextSourceHealthAt = 0f;
                if (string.IsNullOrEmpty(clean)) ReportReceiverReady();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverPresenter] ndiName set failed: {e.Message}");
                ReportReceiverUnavailable($"ndiName set failed: {e.Message}");
            }
        }

        private void CreateReceiver()
        {
            _ndiResources = ResolveNdiResources();
            if (_ndiResources == null)
            {
                Debug.LogWarning("[NdiReceiverPresenter] NdiResources asset not found. NDI receive disabled.");
                ReportReceiverUnavailable("NdiResources asset not found");
                return;
            }

            TryAddReceiverComponent();
        }

        private void TryAddReceiverComponent()
        {
            try
            {
                _receiver = gameObject.AddComponent<Klak.Ndi.NdiReceiver>();
                _receiver.SetResources(_ndiResources);
                BindReceiverTargetRenderer();
                ReportReceiverReady();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverPresenter] AddComponent NdiReceiver failed: {e.Message}");
                ReportReceiverUnavailable($"AddComponent failed: {e.Message}");
            }
        }

        private void BindReceiverTargetRenderer()
        {
            if (_receiver == null || _previewRenderer == null) return;
            _receiver.targetRenderer = _previewRenderer;
            _receiver.targetMaterialProperty = MainTexProperty;
        }

        private static Klak.Ndi.NdiResources? ResolveNdiResources()
        {
            var loaded = Resources.FindObjectsOfTypeAll<Klak.Ndi.NdiResources>();
            if (loaded.Length > 0) return loaded[0];

            var runtime = Resources.Load<Klak.Ndi.NdiResources>(NdiResourcesPath);
            if (runtime != null) return runtime;

#if UNITY_EDITOR
            const string ndiResourcesGuid = "69304b86950074db7ba8caba75214004";
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(ndiResourcesGuid);
            if (!string.IsNullOrEmpty(path))
                return UnityEditor.AssetDatabase.LoadAssetAtPath<Klak.Ndi.NdiResources>(path);
#endif
            return null;
        }

        private void TryAutoPickSource()
        {
            if (Time.unscaledTime < _nextAutoPickAt) return;
            _nextAutoPickAt = Time.unscaledTime + SourceAutoPickPollSec;

            // PickFreeSource now returns an already-sanitized value (or null/empty).
            var picked = PickFreeSource();
            if (string.IsNullOrEmpty(picked)) return;
            // Claim inline so a same-frame second auto-pick on another presenter sees the
            // source as claimed before the SetSourceName → HandleSourceNameChanged event round-trips.
            Claim(picked);
            _node?.SetSourceName(picked);
        }

        private void PollSourceHealth(string sourceName)
        {
            if (Time.unscaledTime < _nextSourceHealthAt) return;
            _nextSourceHealthAt = Time.unscaledTime + SourceAutoPickPollSec;

            if (IsSourceAvailable(sourceName))
                ReportReceiverReady();
            else
                _health?.ReportSourceMissing(GetInstanceID(), sourceName);
        }

        private static bool IsSourceAvailable(string sourceName)
        {
            try
            {
                foreach (var src in Klak.Ndi.NdiFinder.sourceNames)
                    if (src == sourceName) return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverPresenter] NdiFinder health check failed: {e.Message}");
            }
            return false;
        }

        private static string? PickFreeSource()
        {
            try
            {
                foreach (var src in Klak.Ndi.NdiFinder.sourceNames)
                {
                    if (string.IsNullOrEmpty(src)) continue;
                    // Sanitize BEFORE the claim-set lookup so a raw variant like
                    // "CAM\n" cannot bypass an existing claim on the sanitized "CAM".
                    var sanitized = SanitizeSourceName(src);
                    if (string.IsNullOrEmpty(sanitized)) continue;
                    if (_claimedSources.Contains(sanitized)) continue;
                    return sanitized;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverPresenter] NdiFinder enumeration failed: {e.Message}");
            }
            return null;
        }
#endif

        private void TryInjectDependencies()
        {
            if (_health != null) return;

            var scope = GetComponentInParent<LifetimeScope>() ?? LifetimeScope.Find<LifetimeScope>();
            if (scope?.Container == null) return;

            try { scope.Container.Inject(this); }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverPresenter] VContainer injection failed: {e.Message}");
            }
        }

        private void ReportReceiverReady()
        {
            _health?.ReportReceiverReady(GetInstanceID(), _node?.SourceName);
        }

        private void ReportReceiverUnavailable(string reason)
        {
            _health?.ReportReceiverUnavailable(GetInstanceID(), reason);
        }

        private void ReportReceiverStopped()
        {
            _health?.ReportReceiverStopped(GetInstanceID());
        }

        private void Claim(string sourceName)
        {
            ReleaseClaim();
            if (string.IsNullOrEmpty(sourceName)) return;
            _claimedSourceName = sourceName;
            _claimedSources.Add(sourceName);
        }

        private void ReleaseClaim()
        {
            if (string.IsNullOrEmpty(_claimedSourceName)) return;
            _claimedSources.Remove(_claimedSourceName);
            _claimedSourceName = "";
        }

        private void CreatePreviewQuad()
        {
            if (_previewQuad != null) return;
            _previewQuad = new GameObject("NdiReceiver_Preview");
            _previewQuad.transform.SetParent(transform, worldPositionStays: false);
            // inherit layer so MirrorHiddenScope and camera culling masks apply.
            _previewQuad.layer = gameObject.layer;
            ApplyPreviewTransform(_previewQuad.transform);

            var mf = _previewQuad.AddComponent<MeshFilter>();
            mf.sharedMesh = GetSharedPreviewQuad();

            _previewRenderer = _previewQuad.AddComponent<MeshRenderer>();
            if (!TryCreatePreviewMaterial()) return;
            _previewRenderer.sharedMaterial = _previewMaterial;

            _previewCollider = _previewQuad.AddComponent<BoxCollider>();
            _previewCollider.center = Vector3.zero;
            _previewCollider.size = new Vector3(1f, 1f, ColliderDepth);

            RegisterColliderWithVisualManager();
        }

        private void ApplyPreviewTransform(Transform previewTransform)
        {
            var parentScale = transform.lossyScale;
            var scaleX = SafeDivisor(parentScale.x);
            var scaleY = SafeDivisor(parentScale.y);
            var panelHeight = GetComponent<WorldPanelHost>()?.WorldHeight ?? DefaultNodePanelWorldHeight;
            var yOffset = -(PreviewWorldHeight + panelHeight) * 0.5f;

            // why: WorldPanelHost scales the node GameObject, so compensate here to keep
            // the NDI preview at its intended world size and below the panel.
            previewTransform.localPosition = new Vector3(0f, yOffset / scaleY, 0f);
            previewTransform.localRotation = Quaternion.identity;
            previewTransform.localScale = new Vector3(PreviewWorldWidth / scaleX, PreviewWorldHeight / scaleY, 1f);
        }

        private static float SafeDivisor(float scale)
        {
            return Mathf.Abs(scale) < MinParentScale ? 1f : scale;
        }

        private bool TryCreatePreviewMaterial()
        {
            var shader = GetUnlitShader();
            if (shader == null)
            {
                Debug.LogError("[NdiReceiverPresenter] Preview material shader is null. NDI preview will not render.");
                return false;
            }

            _previewMaterial = new Material(shader);
            if (_previewMaterial == null || _previewMaterial.shader == null || !_previewMaterial.shader.isSupported)
            {
                Debug.LogError("[NdiReceiverPresenter] Preview material shader is missing or unsupported. NDI preview will not render.");
                return false;
            }

            _previewMaterial.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.10f, 1f));
            return true;
        }

        private void RegisterColliderWithVisualManager()
        {
            if (_previewCollider == null) return;

            var controller = GetComponent<NodeVisualController>();
            if (controller == null) return;

            var manager = GetComponentInParent<NodeVisualManager>();
            if (manager == null) return;

            manager.RegisterAuxiliaryCollider(_previewCollider, controller);
            _registeredManager = manager;
        }

        private static Mesh GetSharedPreviewQuad()
        {
            if (SharedPreviewQuad != null) return SharedPreviewQuad;
            SharedPreviewQuad = new Mesh
            {
                name = "NdiReceiver_PreviewQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                normals = new[]
                {
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward
                }
            };
            return SharedPreviewQuad;
        }

        private static Shader? GetUnlitShader()
        {
            if (CachedUnlitShader != null) return CachedUnlitShader;

            // This shader must be in Always Included Shaders or referenced from a
            // serialized config asset so player builds do not strip it.
            CachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (CachedUnlitShader == null)
            {
                Debug.LogError("[NdiReceiverPresenter] Universal Render Pipeline/Unlit shader not found. It may be stripped from the player build.");
                CachedUnlitShader = Shader.Find("Unlit/Color");
            }

            return CachedUnlitShader;
        }
    }
}
