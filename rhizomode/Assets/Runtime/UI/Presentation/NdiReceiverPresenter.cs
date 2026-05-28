#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// VR visual presenter for <see cref="INdiReceiverNode"/>.
    /// </summary>
    /// <remarks>
    /// <para>Plan v0.3 Phase F2 で大幅 refactor: preview Quad を node から撤去し、
    /// 独立 <see cref="NdiViewWindow"/> (scene root 配置、grabbable + scalable) に
    /// RenderTexture を流す。node visual の panel には source name の表示のみ残す。</para>
    ///
    /// <para>flicker 回避規約: window spawn → renderer disabled → ApplyTransform →
    /// receiver.targetTexture assign → renderer enable の順を厳守する。</para>
    ///
    /// <para>Klak.NDI calls stay inside <c>KLAK_NDI</c>. When NDI is unavailable, the
    /// presenter keeps the node visual alive and reports health feedback.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NdiReceiverPresenter : MonoBehaviour
    {
        private const string MainTexProperty = "_BaseMap";
        private const int DefaultTextureWidth = 1920;
        private const int DefaultTextureHeight = 1080;
        private const float SourceAutoPickPollSec = 1.5f;
        private const string NdiResourcesPath = "KlakNdi/NdiResources";
        private const int ContextRetryFrames = 3;

        // NDI source names arrive from `Klak.Ndi.NdiFinder.sourceNames`, which lists names
        // broadcast over the LAN by remote senders — untrusted network input. We clamp
        // length and strip control / DEL characters at every intake point before the value
        // flows into KlakNDI's native side, the claim set, health messages, or paramsJson.
        internal const int MaxSourceNameLength = 256;

        private static readonly HashSet<string> _claimedSources = new();

        private INdiReceiverNode? _node;
        private INdiViewWindowState? _windowState;
        private string _nodeId = "";
        private NdiViewWindow? _window;
        private NdiWindowsRoot? _windowsRoot;
        private NdiReceiverHealth? _health;
        private RenderTexture? _renderTexture;

#if KLAK_NDI
        private Klak.Ndi.NdiReceiver? _receiver;
        private Klak.Ndi.NdiResources? _ndiResources;
#endif

        private float _nextAutoPickAt;
        private float _nextSourceHealthAt;
        private string _claimedSourceName = "";
        private int _contextRetryFramesRemaining;

        // Plan v5.4 §15 「VContainer は Bootstrap 専用」境界規則のため、本クラスは [Inject]
        // を使わない。Bootstrap 側 wirer が <see cref="NdiPresentationContext"/> static フィールドに
        // 注入し、<see cref="TryPullFromContext"/> で取得する。</summary>
        private bool TryPullFromContext()
        {
            NdiPresentationContext.EnsureResolved();
            if (_health == null) _health = NdiPresentationContext.Health;
            if (_windowsRoot == null) _windowsRoot = NdiPresentationContext.WindowsRoot;
            return _windowsRoot != null;
        }

        /// <summary>
        /// The node this presenter is currently bound to, or null when detached.
        /// Used by <c>NodeVisualController</c> to detect a rebind to a different
        /// receiver and trigger detach + reattach.
        /// </summary>
        internal INdiReceiverNode? BoundNode => _node;

        /// <summary>spawn 済 window (test 検証用)。</summary>
        internal NdiViewWindow? Window => _window;

        /// <summary>node に attach する。
        /// <paramref name="nodeId"/> は graph 一意 ID で window registry key として使われる。</summary>
        public void Attach(INodeView nodeView)
        {
            if (_node != null) return;
            if (nodeView == null) throw new ArgumentNullException(nameof(nodeView));
            var receiver = nodeView.AsNdiReceiver();
            if (receiver == null) throw new ArgumentException("node is not INdiReceiverNode", nameof(nodeView));
            var windowState = nodeView.AsNdiViewWindowState();
            if (windowState == null) throw new ArgumentException("node is not INdiViewWindowState", nameof(nodeView));

            TryPullFromContext();
            _node = receiver;
            _windowState = windowState;
            _nodeId = nodeView.NodeId;
            if (_windowsRoot == null) ScheduleContextRetry();

            _node.OnSourceNameChanged += HandleSourceNameChanged;
            _node.OnNextSourceRequested += HandleNextSourceRequested;
            _windowState.OnWindowTransformChanged += HandleWindowTransformChanged;

            CreateWindow();

#if KLAK_NDI
            CreateReceiver();
            EnsureRenderTexture();
            BindReceiverTargetTexture();
#else
            Debug.LogWarning("[NdiReceiverPresenter] KLAK_NDI define not set. NDI receive disabled.");
            ReportReceiverUnavailable("KLAK_NDI define not set");
#endif

            // Seed claim set + receiver from any pre-populated SourceName (loaded graphs).
            HandleSourceNameChanged(_node.SourceName);

            // flicker 回避規約の最終段: pose 適用 + receiver assign 完了後に renderer enable
            _window?.SetRendererActive(true);
        }

        /// <summary>Detach from the node and destroy runtime receiver objects.</summary>
        public void Detach()
        {
            // 1. unsubscribe (presenter は inert 化)
            if (_node != null)
            {
                _node.OnSourceNameChanged -= HandleSourceNameChanged;
                _node.OnNextSourceRequested -= HandleNextSourceRequested;
            }
            if (_windowState != null) _windowState.OnWindowTransformChanged -= HandleWindowTransformChanged;

            ReleaseClaim();

#if KLAK_NDI
            // 2. receiver teardown (targetTexture を null にしてから Destroy で 1-frame 黒回避)
            if (_receiver != null)
            {
                _receiver.targetTexture = null;
                Destroy(_receiver);
                _receiver = null;
            }
#endif
            DestroyRenderTexture();

            ReportReceiverStopped();

            // 3. window 破棄 (registry 経由)
            if (_windowsRoot != null && !string.IsNullOrEmpty(_nodeId))
                _windowsRoot.DestroyFor(_nodeId);
            _window = null;

            // 4. clear refs (idempotent 再呼出ガード)
            _node = null;
            _windowState = null;
            _nodeId = "";
        }

        private void Awake() => TryPullFromContext();
        private void OnDestroy() => Detach();

        private void Update()
        {
            RetryContextPullIfNeeded();
#if KLAK_NDI
            if (_node == null) return;
            if (_receiver == null)
            {
                ReportReceiverUnavailable("Receiver component unavailable");
                return;
            }
            RecreateRenderTextureIfSourceSizeChanged();
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
        /// ユーザーが node UI の "Next Source" ボタンを押した経路。
        /// 現 source の「次」を Klak.NDI source 一覧から選び、auto-pick の collision を回避するため
        /// 一旦 Release してから新 source を Claim + SetSourceName する。候補ゼロなら no-op。
        /// </summary>
        private void HandleNextSourceRequested()
        {
            if (_node == null) return;
#if KLAK_NDI
            var current = _node.SourceName;
            // Codex review hint: 自 source が既に claimed 集合に居ると "未 claim" 候補が
            // 全部除外され「自分自身」も候補に残せなくなる。enumerate 前に 1 度 release し、
            // 失敗 fallback で claim を戻す (auto-pick との race を最小化)。
            var prevClaim = _claimedSourceName;
            ReleaseClaim();

            TryRefreshNdiSources();
            var next = PickNextSourceAfter(current);
            if (string.IsNullOrEmpty(next))
            {
                // 候補ゼロ: 直前 claim を復元して終了 (state を壊さない)。
                if (!string.IsNullOrEmpty(prevClaim)) Claim(prevClaim);
                _health?.ReportSourceMissing(GetInstanceID(), "(no NDI sources found)");
                return;
            }

            // SetSourceName → HandleSourceNameChanged → Claim + ApplySourceNameToReceiver が
            // 全部一連で走るので claim 重ねがけしない。
            _node.SetSourceName(next!);
#else
            _health?.ReportReceiverUnavailable("KLAK_NDI define not set");
#endif
        }

        private void HandleWindowTransformChanged()
        {
            // node の paramsJson が外部 (cue load, UI 操作等) で更新されたとき window に追従させる
            if (_window == null || _windowState == null) return;
            _window.ApplyTransform(_windowState.WindowPosition, _windowState.WindowEulerAngles, _windowState.WindowScale);
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

        private void CreateWindow()
        {
            if (_window != null) return;
            if (_windowsRoot == null || _node == null || _windowState == null) return;

            _window = _windowsRoot.CreateFor(_nodeId, _node, _windowState);
            // grab handle が pose 確定したら node に commit (cue save 経路に乗る)
            _window.OnTransformChanged += HandleWindowGrabConfirmed;
        }

        private void ScheduleContextRetry()
        {
            _contextRetryFramesRemaining = ContextRetryFrames;
        }

        private void RetryContextPullIfNeeded()
        {
            if (_contextRetryFramesRemaining <= 0) return;
            _contextRetryFramesRemaining--;
            if (!TryPullFromContext()) return;

            CreateWindow();
#if KLAK_NDI
            EnsureRenderTexture();
            BindReceiverTargetTexture();
#endif
            _window?.SetRendererActive(true);
            _contextRetryFramesRemaining = 0;
        }

        private void HandleWindowGrabConfirmed(Vector3 pos, Vector3 euler, float scale)
        {
            _windowState?.SetWindowTransform(pos, euler, scale);
        }

#if KLAK_NDI
        private void ApplySourceNameToReceiver(string name)
        {
            if (_receiver == null) return;
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
                ReportReceiverReady();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverPresenter] AddComponent NdiReceiver failed: {e.Message}");
                ReportReceiverUnavailable($"AddComponent failed: {e.Message}");
            }
        }

        private void BindReceiverTargetTexture()
        {
            if (_receiver == null) return;
            EnsureRenderTexture();
            if (_renderTexture == null) return;

            _receiver.targetTexture = _renderTexture;
            if (_window == null || _window.Renderer == null) return;
            _window.Renderer.material.SetTexture(MainTexProperty, _renderTexture);
        }

        private void EnsureRenderTexture()
        {
            if (_renderTexture != null) return;
            _renderTexture = CreateRenderTexture(DefaultTextureWidth, DefaultTextureHeight);
        }

        private void RecreateRenderTextureIfSourceSizeChanged()
        {
            if (_receiver == null || _receiver.texture == null) return;
            var source = _receiver.texture;
            if (source.width <= 0 || source.height <= 0) return;
            if (_renderTexture != null &&
                _renderTexture.width == source.width &&
                _renderTexture.height == source.height) return;

            DestroyRenderTexture();
            _renderTexture = CreateRenderTexture(source.width, source.height);
            BindReceiverTargetTexture();
        }

        private static RenderTexture CreateRenderTexture(int width, int height)
        {
            var rt = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "NdiReceiverPresenter_Target",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
            return rt;
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
            var picked = PickFreeSource();
            if (string.IsNullOrEmpty(picked)) return;
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
                TryRefreshNdiSources();
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
                TryRefreshNdiSources();
                foreach (var src in Klak.Ndi.NdiFinder.sourceNames)
                {
                    if (string.IsNullOrEmpty(src)) continue;
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

        /// <summary>
        /// <paramref name="current"/> の「次」の sanitized source を返す。
        /// 候補が <paramref name="current"/> 自身しかなければそのまま再 claim、
        /// 一覧が空なら null。一覧の wrap-around は許容 (一周しても見つからなければ最初へ戻る)。
        /// </summary>
        private static string? PickNextSourceAfter(string current)
        {
            var ordered = new List<string>();
            try
            {
                TryRefreshNdiSources();
                foreach (var src in Klak.Ndi.NdiFinder.sourceNames)
                {
                    if (string.IsNullOrEmpty(src)) continue;
                    var sanitized = SanitizeSourceName(src);
                    if (string.IsNullOrEmpty(sanitized)) continue;
                    ordered.Add(sanitized);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverPresenter] NdiFinder enumeration failed: {e.Message}");
                return null;
            }
            if (ordered.Count == 0) return null;

            // 現 source の index を見つけて +1 から走査。自分自身を skip しつつ、
            // 他 presenter が claim 済の source は除外。全 claim 済なら自分の現値に戻る。
            int startIdx = 0;
            if (!string.IsNullOrEmpty(current))
            {
                var idx = ordered.IndexOf(current);
                if (idx >= 0) startIdx = idx + 1;
            }
            for (int i = 0; i < ordered.Count; i++)
            {
                var candidate = ordered[(startIdx + i) % ordered.Count];
                if (candidate == current) continue;
                if (_claimedSources.Contains(candidate)) continue;
                return candidate;
            }
            // 全候補が他 presenter に claim されている → 現値そのまま (no-op を呼ぶ側で扱う)
            return string.IsNullOrEmpty(current) ? ordered[0] : null;
        }

        private static void TryRefreshNdiSources()
        {
            try
            {
                var method = typeof(Klak.Ndi.NdiFinder).GetMethod("RefreshSources");
                method?.Invoke(null, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiReceiverPresenter] NdiFinder refresh failed: {e.Message}");
            }
        }
#endif

        private void DestroyRenderTexture()
        {
            if (_renderTexture == null) return;
            if (Application.isPlaying) Destroy(_renderTexture);
            else DestroyImmediate(_renderTexture);
            _renderTexture = null;
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
    }
}
