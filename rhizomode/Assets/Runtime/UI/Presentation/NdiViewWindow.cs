#nullable enable

using System;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// NDI 受信映像を表示する独立 GameObject。node とは親子付けされず scene root 配置。
    /// </summary>
    /// <remarks>
    /// <para>Plan v0.3 §NdiViewWindow component の実装。</para>
    /// <para>flicker 回避規約: spawn 直後は <see cref="SetRendererActive"/>(false) → pose 適用 →
    /// Klak.NDI receiver の <c>targetRenderer</c> assign → <see cref="SetRendererActive"/>(true)
    /// の順を厳守する (presenter 側の責務)。</para>
    /// <para>2-hand scale 中の中間値は <see cref="ApplyTransform"/> で直接 transform を更新するが
    /// node への commit は <c>WindowGrabHandle</c> の grab end タイミングで 1 回だけ
    /// (<see cref="OnTransformChanged"/> を発火) → presenter → node.SetWindowTransform。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public sealed class NdiViewWindow : MonoBehaviour
    {
        /// <summary>16:9 aspect ratio (NDI source は基本 16:9 を想定、4:3 source は黒帯)。</summary>
        public const float Aspect = 16f / 9f;

        /// <summary>scale=1 で 1.0m × 0.5625m 相当。</summary>
        public const float BaseWidth = 1.0f;

        /// <summary>scale clamp 下限 (極小化を防ぐ)。</summary>
        public const float MinScale = 0.1f;

        /// <summary>scale clamp 上限 (4m 超の巨大化を防ぐ)。</summary>
        public const float MaxScale = 4.0f;

        private static Mesh? _sharedQuadMesh;

        private MeshRenderer? _renderer;
        private BoxCollider? _collider;

        /// <summary>NDI receiver の targetRenderer に渡す renderer (Klak.NDI が毎フレーム MPB blit する)。</summary>
        public MeshRenderer Renderer => _renderer!;

        /// <summary>VR raycast 用 collider。WindowGrabHandle がこの transform を grab する。</summary>
        public BoxCollider Collider => _collider!;

        /// <summary>grab handle が pose 確定したときに presenter へ通知。</summary>
        public event Action<Vector3 /*pos*/, Vector3 /*euler*/, float /*scale*/>? OnTransformChanged;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<BoxCollider>();
            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh == null) mf.sharedMesh = GetOrCreateSharedQuad();
            // BoxCollider は Quad と同サイズの薄い板 (Z 方向は 1cm)
            _collider.center = Vector3.zero;
            _collider.size = new Vector3(1f, 1f / Aspect, 0.01f);
            // 受信前は不可視 (presenter が pose 適用後に明示 enable)
            _renderer.enabled = false;
        }

        /// <summary>外部 (presenter / cue load) から transform を即時適用。clamp 適用済の値を渡すこと。</summary>
        public void ApplyTransform(Vector3 position, Vector3 eulerAngles, float scale)
        {
            transform.position = position;
            transform.eulerAngles = eulerAngles;
            // 横幅 BaseWidth × scale、縦は Aspect で割る (16:9 維持)。z は 1 (薄板)。
            var clamped = Mathf.Clamp(scale, MinScale, MaxScale);
            transform.localScale = new Vector3(BaseWidth * clamped, BaseWidth * clamped / Aspect, 1f);
        }

        /// <summary>renderer の表示 ON/OFF。flicker 回避のため presenter が制御する。</summary>
        public void SetRendererActive(bool active)
        {
            if (_renderer != null) _renderer.enabled = active;
        }

        /// <summary>grab handle から「pose 確定したよ」と通知される (uniform scale で commit)。</summary>
        internal void RaiseTransformChanged(Vector3 position, Vector3 eulerAngles, float scale)
        {
            try { OnTransformChanged?.Invoke(position, eulerAngles, scale); }
            catch (Exception e)
            {
                Debug.LogWarning($"[NdiViewWindow] OnTransformChanged handler threw: {e.Message}");
            }
        }

        /// <summary>共有 1×1 quad mesh (Z 方向 forward face、UV [0,1])。</summary>
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
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.normals = new[]
            {
                -Vector3.forward, -Vector3.forward,
                -Vector3.forward, -Vector3.forward,
            };
            m.RecalculateBounds();
            _sharedQuadMesh = m;
            return m;
        }
    }
}
