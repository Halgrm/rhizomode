#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// WorldSpace UIToolkitパネルをホストする。RenderTexture経由で
    /// 3D空間上のQuadにUIを表示し、XRレイキャストとの橋渡しを行う。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class WorldPanelHost : MonoBehaviour
    {
        private const int DefaultTextureWidth = 512;
        private const int DefaultTextureHeight = 308;

        [SerializeField] private PanelSettings? panelSettingsTemplate;
        [SerializeField] private float worldWidth = 0.20f;
        [SerializeField] private float worldHeight = 0.12f;

        /// <summary>PanelSettingsテンプレートを外部から設定する（ランタイム生成時用）。</summary>
        public PanelSettings? PanelSettingsTemplate
        {
            get => panelSettingsTemplate;
            set => panelSettingsTemplate = value;
        }

        private UIDocument? _uiDocument;
        private RenderTexture? _renderTexture;
        private PanelSettings? _panelSettings;
        private MeshRenderer? _meshRenderer;
        private Material? _materialInstance;

        /// <summary>UIDocumentのルートVisualElementへのアクセス。</summary>
        public VisualElement? Root => _uiDocument?.rootVisualElement;

        /// <summary>パネルのワールド幅（メートル）。</summary>
        public float WorldWidth => worldWidth;

        /// <summary>パネルのワールド高さ（メートル）。</summary>
        public float WorldHeight => worldHeight;

        /// <summary>パネルのテクスチャ幅（ピクセル）。</summary>
        public int TextureWidth => _renderTexture?.width ?? DefaultTextureWidth;

        /// <summary>パネルのテクスチャ高さ（ピクセル）。</summary>
        public int TextureHeight => _renderTexture?.height ?? DefaultTextureHeight;

        /// <summary>初期化済みかどうか。</summary>
        public bool IsInitialized => _uiDocument != null;

        /// <summary>
        /// パネルを初期化し、RenderTexture・UIDocument・Quadを構成する。
        /// 二重呼び出しは無視される。
        /// </summary>
        public void Initialize(VisualTreeAsset uxml, StyleSheet? styleSheet = null, int textureWidth = DefaultTextureWidth, int textureHeight = DefaultTextureHeight)
        {
            if (IsInitialized) return;

            CreateRenderTexture(textureWidth, textureHeight);
            CreatePanelSettings();
            CreateUIDocument(uxml, styleSheet);
            SetupQuad();
        }

        /// <summary>
        /// パネルサイズを変更する。
        /// </summary>
        public void Resize(float width, float height)
        {
            worldWidth = width;
            worldHeight = height;
            SetupQuad();
        }

        /// <summary>
        /// レイキャストヒット位置からパネル内のピクセル座標を計算する。
        /// </summary>
        public Vector2 RayHitToPanelPosition(RaycastHit hit)
        {
            // MeshCollider上のUV座標を取得
            var uv = hit.textureCoord;
            // UIToolkitは左上原点、UVは左下原点なのでY反転
            return new Vector2(
                uv.x * TextureWidth,
                (1f - uv.y) * TextureHeight
            );
        }

        private void CreateRenderTexture(int width, int height)
        {
            _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = $"NodePanel_RT_{gameObject.name}"
            };
            _renderTexture.Create();
        }

        private void CreatePanelSettings()
        {
            // テンプレートからクローンしてテーマを継承、なければ新規生成
            if (panelSettingsTemplate != null)
            {
                _panelSettings = Instantiate(panelSettingsTemplate);
            }
            else
            {
                _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            }
            _panelSettings.targetTexture = _renderTexture;
            _panelSettings.clearColor = true;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
        }

        private void CreateUIDocument(VisualTreeAsset uxml, StyleSheet? styleSheet)
        {
            _uiDocument = gameObject.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;
            _uiDocument.visualTreeAsset = uxml;

            if (styleSheet != null && _uiDocument.rootVisualElement != null)
            {
                _uiDocument.rootVisualElement.styleSheets.Add(styleSheet);
            }
        }

        private void SetupQuad()
        {
            // Quadメッシュ設定
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter.sharedMesh == null)
            {
                meshFilter.sharedMesh = CreateQuadMesh();
            }

            // スケールでワールドサイズを反映
            transform.localScale = new Vector3(worldWidth, worldHeight, 1f);

            // マテリアルにRenderTextureを設定
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_materialInstance == null)
            {
                // URP Unlitシェーダーを使用（Transparent）
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    _materialInstance = new Material(shader);
                    _materialInstance.SetFloat("_Surface", 1f); // Transparent
                    _materialInstance.SetFloat("_Blend", 0f);   // Alpha
                    _materialInstance.SetFloat("_Cull", 0f);    // Off (両面)
                    _materialInstance.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    _materialInstance.renderQueue = 3000;
                }
            }

            if (_materialInstance != null && _renderTexture != null)
            {
                _materialInstance.mainTexture = _renderTexture;
                _meshRenderer.material = _materialInstance;
            }

            // MeshCollider更新（XRレイキャスト用）
            var collider = GetComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh
            {
                name = "WorldPanel_Quad",
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
            return mesh;
        }

        private void OnDestroy()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }
            if (_materialInstance != null)
            {
                Destroy(_materialInstance);
            }
            if (_panelSettings != null)
            {
                Destroy(_panelSettings);
            }
        }
    }
}
