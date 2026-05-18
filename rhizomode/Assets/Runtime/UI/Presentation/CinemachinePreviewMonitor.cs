#nullable enable

using UnityEngine;
using UnityEngine.Rendering.Universal;

using Rhizomode.Cameras;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// Cinemachineカメラの出力をシーン内のQuad（浮遊モニター）に表示する。
    /// デスクトップデバッグモードでパフォーマンスカメラのプレビューに使用。
    /// </summary>
    public class CinemachinePreviewMonitor : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera? previewCamera;

        [Header("Monitor")]
        [SerializeField] private Vector2 monitorSize = new(0.8f, 0.45f);
        [SerializeField] private int textureWidth = 1280;
        [SerializeField] private int textureHeight = 720;

        private RenderTexture? _renderTexture;
        private Material? _material;
        private MeshFilter? _meshFilter;
        private MeshRenderer? _meshRenderer;

        /// <summary>プレビュー用RenderTexture。外部からの参照用。</summary>
        public RenderTexture? OutputTexture => _renderTexture;

        /// <summary>
        /// プレビューモニターを初期化する。RenderTexture作成・カメラ設定・Quad生成。
        /// </summary>
        public void Initialize()
        {
            if (previewCamera == null)
            {
                Debug.LogWarning("[CinemachinePreviewMonitor] previewCamera is not assigned");
                return;
            }

            // RenderTexture作成
            _renderTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "CinemachinePreview_RT",
                filterMode = FilterMode.Bilinear
            };
            _renderTexture.Create();

            // カメラ設定
            previewCamera.targetTexture = _renderTexture;
            previewCamera.enabled = true;

            var urpData = previewCamera.GetUniversalAdditionalCameraData();
            if (urpData != null)
                urpData.renderType = CameraRenderType.Base;

            // Quad生成
            CreateQuad();

            // Mirror カメラに preview Quad が映り込まないよう PerformerUI layer に揃える。
            PerformerUILayer.ApplyRecursive(gameObject);

            Debug.Log("[CinemachinePreviewMonitor] Initialized");
        }

        private void CreateQuad()
        {
            // メッシュ
            _meshFilter = gameObject.GetComponent<MeshFilter>();
            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();

            _meshFilter.sharedMesh = CreateQuadMesh();

            // レンダラー
            _meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Texture");
            _material = new Material(shader!);
            _material.SetFloat("_Surface", 1f);
            _material.SetFloat("_Blend", 0f);
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.renderQueue = 3000;
            _material.mainTexture = _renderTexture;
            _meshRenderer.material = _material;

            // ワールドサイズ設定
            transform.localScale = new Vector3(monitorSize.x, monitorSize.y, 1f);
        }

        private static Mesh CreateQuadMesh()
        {
            return new Mesh
            {
                name = "PreviewMonitor_Quad",
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

            if (_renderTexture != null)
            {
                if (previewCamera != null)
                    previewCamera.targetTexture = null;

                _renderTexture.Release();
                Destroy(_renderTexture);
            }
        }
    }
}
