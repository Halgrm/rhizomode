#nullable enable

using UnityEngine;

using Rhizomode.Cameras;

namespace Rhizomode.UI
{
    /// <summary>
    /// MirrorOutputController が生成した RenderTexture を VR シーン内 Quad に表示する。
    /// 演者が HMD 越しに観客出力 (Spout/NDI/Desktop と同じ映像) を確認するためのプレビュー。
    /// </summary>
    /// <remarks>
    /// 自身は RenderTexture を所有しない (MirrorOutputController が所有)。<see cref="Initialize"/>
    /// で受け取った source をマテリアルに設定するのみ。CinemachinePreviewMonitor とは
    /// 「RT を作るか、受け取るか」の違いで責務分離している。
    /// </remarks>
    public class MirrorPreviewMonitor : MonoBehaviour
    {
        [Header("Monitor")]
        [Tooltip("Quad のワールドサイズ (m)。16:9 を想定。")]
        [SerializeField] private Vector2 monitorSize = new(1.6f, 0.9f);

        private RenderTexture? _source;
        private Material? _material;
        private MeshFilter? _meshFilter;
        private MeshRenderer? _meshRenderer;

        /// <summary>初期化済みかどうか。</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// プレビューモニターを初期化する。MirrorOutput の OutputTexture を表示する。
        /// </summary>
        /// <param name="source">表示する RenderTexture。MirrorOutputController.OutputTexture を渡す。</param>
        public void Initialize(RenderTexture source)
        {
            if (IsInitialized)
            {
                // ソース差し替え。
                _source = source;
                if (_material != null)
                    _material.mainTexture = source;
                return;
            }

            _source = source;
            CreateQuad();
            // Mirror カメラに自身が再帰的に映り込まないよう MirrorHidden layer に揃える
            // (preview Quad は VR HMD だけに見せる)。
            MirrorHiddenLayer.ApplyRecursive(gameObject);
            IsInitialized = true;
        }

        private void CreateQuad()
        {
            _meshFilter = gameObject.GetComponent<MeshFilter>();
            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();

            _meshFilter.sharedMesh = CreateQuadMesh();

            _meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Texture");
            _material = new Material(shader!);
            _material.mainTexture = _source;
            _meshRenderer.material = _material;

            transform.localScale = new Vector3(monitorSize.x, monitorSize.y, 1f);
        }

        private static Mesh CreateQuadMesh()
        {
            return new Mesh
            {
                name = "MirrorPreview_Quad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(1f, 1f), new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                normals = new[]
                {
                    -Vector3.forward, -Vector3.forward,
                    -Vector3.forward, -Vector3.forward
                }
            };
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);

            _source = null;
        }
    }
}
