#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;
using Rhizomode.Presentation.Layering;

namespace Rhizomode.UI
{
    /// <summary>
    /// WorldSpace UIToolkitパネルをホストする。RenderTexture経由で
    /// 3D空間上のQuadにUIを表示し、XRレイキャストとの橋渡しを行う。
    /// </summary>
    [RequireMirrorHidden]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public class WorldPanelHost : MonoBehaviour
    {
        private const int DefaultTextureWidth = 256;
        private const int DefaultTextureHeight = 154;

        [SerializeField] private PanelSettings? panelSettingsTemplate;
        [SerializeField] private float worldWidth = 0.20f;
        [SerializeField] private float worldHeight = 0.12f;

        /// <summary>PanelSettingsテンプレートを外部から設定する（ランタイム生成時用）。</summary>
        public PanelSettings? PanelSettingsTemplate
        {
            get => panelSettingsTemplate;
            set => panelSettingsTemplate = value;
        }

        private static Mesh? SharedQuadMesh;
        private static Shader? CachedShader;

        [SerializeField] private Shader? quadShader;

        private UIDocument? _uiDocument;
        private RenderTexture? _renderTexture;
        private PanelSettings? _panelSettings;
        private MeshRenderer? _meshRenderer;
        private Material? _materialInstance;
        private StyleSheet? _pendingStyleSheet;
        private bool _styleSheetApplied;

        /// <summary>
        /// 初回 Initialize 後に UIToolkit が少なくとも 1 フレームの layout + paint を完了したか。
        /// </summary>
        /// <remarks>
        /// cue 透明ノード fix: ロード直後 LOD が <see cref="SetUIActive"/>(false) を呼ぶと、
        /// UIDocument が一度も描画していない RT が targetTexture から外され空のまま残り、
        /// quad が透明描画になる。LOD は本フラグが true になるまで deactivate を保留する。
        /// </remarks>
        private const int FramesBeforeFirstRender = 2;
        private int _initFrame = -1;
        private bool _hasRenderedAtLeastOnce;

        /// <inheritdoc cref="_hasRenderedAtLeastOnce"/>
        public bool HasRenderedAtLeastOnce => _hasRenderedAtLeastOnce;

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
            _initFrame = Time.frameCount;

            // [RequireMirrorHidden] + 同 GameObject 上の MirrorHiddenScope (Scene 配置 or
            // runtime spawn 元の reparent) によって layer は自動適用されるため明示呼出は不要。
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
        /// RenderTextureの解像度を変更する。LODシステムから呼び出される。
        /// アスペクト比は維持される。
        /// </summary>
        /// <remarks>
        /// UI 非アクティブ時 (PanelSettings.targetTexture == null) は no-op。
        /// Release/Create で RT を黒に初期化した直後に panel が描画しないと
        /// 最後のフレームが失われ Quad が黒くなるため。
        /// </remarks>
        public void ChangeResolution(int newWidth)
        {
            if (_renderTexture == null || _panelSettings == null) return;
            if (_panelSettings.targetTexture == null) return;
            if (_renderTexture.width == newWidth) return;

            var aspectRatio = (float)_renderTexture.height / _renderTexture.width;
            var newHeight = Mathf.Max(1, Mathf.RoundToInt(newWidth * aspectRatio));

            _renderTexture.Release();
            _renderTexture.width = newWidth;
            _renderTexture.height = newHeight;
            _renderTexture.Create();

            if (_materialInstance != null)
                _materialInstance.mainTexture = _renderTexture;
        }

        /// <summary>
        /// レイキャストヒット位置からパネル内のピクセル座標を計算する。
        /// </summary>
        public Vector2 RayHitToPanelPosition(RaycastHit hit)
        {
            // ヒット座標をローカル空間に変換（Quadは-0.5〜0.5）
            var local = transform.InverseTransformPoint(hit.point);
            float u = local.x + 0.5f;
            float v = local.y + 0.5f;
            // UIToolkitは左上原点なのでY反転
            return new Vector2(
                u * TextureWidth,
                (1f - v) * TextureHeight
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

            _pendingStyleSheet = styleSheet;
        }

        private void SetupQuad()
        {
            // 共有Quadメッシュ（全ノードで1つ）
            var meshFilter = GetComponent<MeshFilter>();
            if (SharedQuadMesh == null)
                SharedQuadMesh = CreateQuadMesh();
            meshFilter.sharedMesh = SharedQuadMesh;

            // スケールでワールドサイズを反映
            transform.localScale = new Vector3(worldWidth, worldHeight, 1f);

            // マテリアルにRenderTextureを設定
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_materialInstance == null)
            {
                // シェーダーキャッシュ（Shader.Findは文字列検索で低速）
                if (CachedShader == null)
                {
                    CachedShader = quadShader != null ? quadShader
                        : Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Unlit/Transparent");
                }
                if (CachedShader != null)
                {
                    _materialInstance = new Material(CachedShader);
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

            // BoxCollider（Quadと同サイズ、薄い板）
            var boxCollider = GetComponent<BoxCollider>();
            boxCollider.center = Vector3.zero;
            boxCollider.size = new Vector3(1f, 1f, 0.01f);
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

        /// <summary>
        /// パネル描画の有効/無効を切り替える。LODによる負荷軽減に使用。
        /// </summary>
        /// <remarks>
        /// 無効化は <c>PanelSettings.targetTexture</c> を null にすることで実現する。
        /// <c>UIDocument.enabled = false</c> を使うと clearColor=true との組み合わせで
        /// RT が一度黒 clear された後 dirty 化されず、再有効化しても黒のまま戻らない
        /// 挙動になるため避ける。targetTexture 方式なら描画パスが走らないので
        /// RT は最後のフレームを保持し、有効化時に <see cref="VisualElement.MarkDirtyRepaint"/>
        /// で確実に再描画される。
        /// </remarks>
        public void SetUIActive(bool active)
        {
            if (_panelSettings == null || _renderTexture == null) return;

            if (active)
            {
                if (_panelSettings.targetTexture != _renderTexture)
                    _panelSettings.targetTexture = _renderTexture;
                _uiDocument?.rootVisualElement?.MarkDirtyRepaint();
            }
            else
            {
                _panelSettings.targetTexture = null;
            }
        }

        /// <summary>パネルが現在 RT に描画中かどうか。</summary>
        public bool IsUIActive => _panelSettings != null && _panelSettings.targetTexture != null;

        private void Update()
        {
            if (!_styleSheetApplied && _pendingStyleSheet != null && _uiDocument?.rootVisualElement != null)
            {
                _uiDocument.rootVisualElement.styleSheets.Add(_pendingStyleSheet);
                _pendingStyleSheet = null;
                _styleSheetApplied = true;
            }

            // 初回 layout + paint 完了判定 (sticky flag)。targetTexture が active な状態で
            // FramesBeforeFirstRender フレーム経過したら、UIToolkit の repaint pass を経た想定。
            // LOD が deactivate した状態ではカウントしない (RT に新規描画されないため)。
            if (!_hasRenderedAtLeastOnce && _initFrame >= 0 &&
                _panelSettings != null && _panelSettings.targetTexture != null &&
                Time.frameCount - _initFrame >= FramesBeforeFirstRender)
            {
                _hasRenderedAtLeastOnce = true;
            }
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
