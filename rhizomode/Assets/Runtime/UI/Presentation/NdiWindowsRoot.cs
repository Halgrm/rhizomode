#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// SampleScene 常設の NDI window 管理 root。
    /// </summary>
    /// <remarks>
    /// <para>Plan v0.3 §"Persistent graph-owned root"。env scene (additive ロード) では
    /// なく base scene (SampleScene) に置くことで、env 切替で window が宙ぶらりんに
    /// ならない。</para>
    ///
    /// <para>本 commit (F2) では <c>NdiViewWindowFactory</c> を分離せず本 component に
    /// factory 機能を merge している。F3 以降で必要なら分離。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NdiWindowsRoot : MonoBehaviour
    {
        [SerializeField] private NdiViewWindow? windowPrefab;
        [SerializeField] private Material? windowMaterial;

        // nodeId → window の registry。node delete / cue clear で確実に Destroy するため
        // strong ref を保持し、loader-owned パターンに準ずる (env-scene-isolation と同流儀)。
        private readonly Dictionary<string, NdiViewWindow> _windows = new();

        /// <summary>
        /// 新 window が生成された直後に発火。Interaction 層 (WindowGrabBootstrap) が subscribe
        /// して <c>WindowGrabHandle</c> を attach する経路。UI.Presentation が
        /// Interaction を参照しないよう、event を介した non-circular wiring。
        /// </summary>
        public event Action<NdiViewWindow>? OnWindowSpawned;

        /// <summary>
        /// node 用 window を生成 (既にあれば既存を返す)。<paramref name="nodeId"/> は
        /// graph 一意の ID で window registry の key として使う (presenter から渡される)。
        /// </summary>
        public NdiViewWindow CreateFor(string nodeId, INdiReceiverNode node, INdiViewWindowState state)
        {
            if (string.IsNullOrEmpty(nodeId)) throw new ArgumentException("nodeId required", nameof(nodeId));
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (_windows.TryGetValue(nodeId, out var existing) && existing != null)
                return existing;

            var go = windowPrefab != null
                ? Instantiate(windowPrefab, transform)
                : CreateDefaultWindow(transform);
            go.name = $"NdiViewWindow_{nodeId}";

            // material 割当の優先順位: (1) Inspector で指定された windowMaterial、
            // (2) Klak.NDI が MaterialPropertyBlock 経由で _BaseMap を書き込めるよう、
            //     runtime 生成の URP/Unlit ベース material をフォールバックで割当てる。
            // material が null だと MeshRenderer が描画自体されない (pink / 透明) ため必須。
            if (go.Renderer != null)
            {
                Material mat = windowMaterial != null ? windowMaterial : EnsureFallbackMaterial();
                go.Renderer.sharedMaterial = mat;
            }

            ApplyInitialPose(go, nodeId, state);

            _windows[nodeId] = go;
            try { OnWindowSpawned?.Invoke(go); }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiWindowsRoot] OnWindowSpawned subscriber threw: {e.Message}");
            }
            return go;
        }

        /// <summary>node 用 window を破棄。registry からも除外する。idempotent。</summary>
        public void DestroyFor(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            if (!_windows.TryGetValue(nodeId, out var w))
                return;
            _windows.Remove(nodeId);
            if (w != null)
            {
                if (Application.isPlaying) Destroy(w.gameObject);
                else                       DestroyImmediate(w.gameObject);
            }
        }

        /// <summary>graph clear (cue 切替) のとき registry の全 window を破棄する defensive 経路。</summary>
        public void DestroyAll()
        {
            foreach (var pair in _windows)
            {
                if (pair.Value == null) continue;
                if (Application.isPlaying) Destroy(pair.Value.gameObject);
                else                       DestroyImmediate(pair.Value.gameObject);
            }
            _windows.Clear();
        }

        /// <summary>登録済か (presenter の Detach 経路で参照済確認に使う)。</summary>
        public bool TryGet(string nodeId, out NdiViewWindow window)
        {
            if (_windows.TryGetValue(nodeId, out var w) && w != null)
            {
                window = w;
                return true;
            }
            window = default!;
            return false;
        }

        /// <summary>registry の登録数 (test 用)。</summary>
        internal int Count => _windows.Count;

        private void OnDestroy()
        {
            // SampleScene unload (= 終了時) の defensive cleanup。
            DestroyAll();
        }

        /// <summary>
        /// state.HasExplicitWindowTransform = true なら state の pose を、false なら
        /// HMD-forward + cascade offset を初期 pose に採用する。flicker 回避のため
        /// renderer は inactive のままにする (presenter が assign 後に明示 enable)。
        /// </summary>
        private void ApplyInitialPose(NdiViewWindow window, string nodeId, INdiViewWindowState state)
        {
            window.SetRendererActive(false);

            if (state.HasExplicitWindowTransform)
            {
                window.ApplyTransform(state.WindowPosition, state.WindowEulerAngles, state.WindowScale);
                return;
            }

            // HMD reference: Camera.main → hardcoded (0, 1.5, 1.5) fallback。
            // 完全な IXrHmdReference 三段は将来追加 (Plan v0.3 §HMD null fallback、
            // 本 commit では 2 段で運用)。
            Vector3 hmdPos;
            Vector3 hmdForward;
            Vector3 hmdRight;
            var cam = Camera.main;
            if (cam != null)
            {
                hmdPos     = cam.transform.position;
                hmdForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                if (hmdForward.sqrMagnitude < 0.0001f) hmdForward = Vector3.forward;
                hmdRight   = Vector3.Cross(Vector3.up, hmdForward);
            }
            else
            {
                hmdPos     = new Vector3(0f, 1.5f, 0f);
                hmdForward = Vector3.forward;
                hmdRight   = Vector3.right;
            }

            var basePos = hmdPos + hmdForward * 1.5f + Vector3.up * 0.2f;
            var offset  = NdiViewWindowMath.CascadeOffset(nodeId, hmdForward, hmdRight);
            var pos     = basePos + offset;

            // ユーザーに正対する yaw を計算 (window 法線 = HMD への方向)
            var lookDir = (hmdPos - pos);
            lookDir.y = 0f;
            var rot = lookDir.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                : Quaternion.identity;

            window.ApplyTransform(pos, rot.eulerAngles, 1.0f);
        }

        private static NdiViewWindow CreateDefaultWindow(Transform parent)
        {
            // prefab が未アサインでも spawn できる fallback (test / canary 用)。
            var go = new GameObject("NdiViewWindow_Default");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<BoxCollider>();
            return go.AddComponent<NdiViewWindow>();
        }

        // runtime 生成 fallback material (URP/Unlit、_BaseColor=black)。Klak.NDI の
        // MaterialPropertyBlock で _BaseMap を書き込めるよう base material を確保。
        private static Material? _fallbackMaterial;

        private static Material EnsureFallbackMaterial()
        {
            if (_fallbackMaterial != null) return _fallbackMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Texture") ??
                         Shader.Find("Hidden/InternalErrorShader");
            _fallbackMaterial = new Material(shader) { name = "NdiWindow_FallbackMat" };
            if (_fallbackMaterial.HasProperty("_BaseColor"))
                _fallbackMaterial.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.02f, 1f));
            // URP Unlit が _MainTex 経由 sampling する場合のフォールバック
            if (_fallbackMaterial.HasProperty("_Color"))
                _fallbackMaterial.SetColor("_Color", new Color(0.02f, 0.02f, 0.02f, 1f));
            return _fallbackMaterial;
        }
    }
}
