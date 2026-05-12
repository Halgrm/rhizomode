#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// 巻物メニューの視覚コンポーネント。カテゴリバー（常時表示・軽量Quad）と
    /// スクロールパネル（展開時のみ・WorldPanelHost）を管理する。
    /// バーはUIToolkit不使用（メモリ削減）。スクロールのみWorldPanelHost。
    /// </summary>
    public class ScrollMenuVisualController : MonoBehaviour
    {
        [Header("Bar Size")]
        [SerializeField] private float barWorldWidth = 0.15f;
        [SerializeField] private float barWorldHeight = 0.03f;

        [Header("Scroll Panel")]
        [SerializeField] private float scrollWorldWidth = 0.22f;
        [SerializeField] private float scrollMaxWorldHeight = 0.35f;
        [SerializeField] private int scrollTextureWidth = 300;
        [SerializeField] private int scrollTextureHeight = 500;

        [Header("Layout")]
        [SerializeField] private float arcRadius = 0.40f;
        [SerializeField] private float arcSpanDegrees = 120f;
        [SerializeField] private float waistOffsetY = -0.55f;
        [SerializeField] private float waistForwardOffset = 0.30f;
        [SerializeField] private float scrollGapAboveBar = 0.005f;

        [SerializeField] private VisualTreeAsset? scrollUxml;
        [SerializeField] private StyleSheet? scrollStyleSheet;
        [SerializeField] private PanelSettings? panelSettingsTemplate;

        [SerializeField, Tooltip("メニューに表示するカテゴリとアクセントカラーの定義")]
        private CategoryDefinition[] categoryDefinitions = new[]
        {
            new CategoryDefinition(NodeCategory.Input, "Input", new Color(0.63f, 0.82f, 0.94f, 0.9f)),
            new CategoryDefinition(NodeCategory.Math, "Math", new Color(0.32f, 0.79f, 0.42f, 1f)),
            new CategoryDefinition(NodeCategory.VFX, "VFX", new Color(0.88f, 0.42f, 0.62f, 1f)),
            new CategoryDefinition(NodeCategory.Shader, "Shader", new Color(0.68f, 0.46f, 0.88f, 1f)),
            new CategoryDefinition(NodeCategory.Time, "Time", new Color(0.88f, 0.78f, 0.32f, 1f)),
            new CategoryDefinition(NodeCategory.Utility, "Utility", new Color(1f, 1f, 1f, 0.3f)),
            new CategoryDefinition(NodeCategory.Scene, "Scene", new Color(0.30f, 0.70f, 0.70f, 1f)),
        };

        private NodeTypeRegistry? _typeRegistry;
        private readonly List<CategoryBarEntry> _bars = new();
        private ScrollPanelEntry? _activeScroll;
        private NodeCategory? _activeCategory;
        private bool _isInitialized;

        private static Shader? CachedUnlitShader;
        private static Mesh? SharedQuadMesh;

        /// <summary>ノードタイプが選択された時に発火する。引数はタイプ名。</summary>
        public event Action<string>? OnNodeTypeSelected;

        /// <summary>現在展開中のカテゴリ。nullなら閉じている。</summary>
        public NodeCategory? ActiveCategory => _activeCategory;

        public NodeCategory? GetCategoryFromCollider(Collider collider)
        {
            foreach (var bar in _bars)
            {
                if (bar.Collider == collider)
                    return bar.Category;
            }
            return null;
        }

        public bool IsScrollCollider(Collider collider)
        {
            return _activeScroll != null && _activeScroll.Collider == collider;
        }

        public void Initialize(NodeTypeRegistry typeRegistry)
        {
            if (_isInitialized) return;
            _typeRegistry = typeRegistry;

            CreateCategoryBars();
            _isInitialized = true;
        }

        public void OpenScroll(NodeCategory category)
        {
            if (_typeRegistry == null) return;
            if (_activeCategory == category && _activeScroll != null) return;

            CloseScroll();

            var nodes = _typeRegistry.GetByCategory(category).ToList();
            if (nodes.Count == 0) return;

            _activeCategory = category;

            var barEntry = _bars.Find(b => b.Category == category);
            if (barEntry == null) return;

            _activeScroll = CreateScrollPanel(barEntry, nodes);
            SetScrollHeight(0f);
        }

        public void CloseScroll()
        {
            if (_activeScroll == null) return;

            if (_activeScroll.GameObject != null)
                Destroy(_activeScroll.GameObject);

            _activeScroll = null;
            _activeCategory = null;
        }

        public void SetScrollHeight(float t)
        {
            if (_activeScroll == null) return;

            t = Mathf.Clamp01(t);
            var scaleY = Mathf.Max(t * scrollMaxWorldHeight, 0.001f);

            var scrollTransform = _activeScroll.GameObject.transform;
            var basePos = _activeScroll.BaseLocalPosition;
            scrollTransform.localPosition = new Vector3(basePos.x, basePos.y + scaleY * 0.5f, basePos.z);
            scrollTransform.localScale = new Vector3(scrollWorldWidth, scaleY, 1f);
        }

        public void UpdateWaistFollow(Vector3 headPosition, Quaternion rigRotation)
        {
            var rigForward = rigRotation * Vector3.forward;
            var flatForward = new Vector3(rigForward.x, 0f, rigForward.z).normalized;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;

            transform.position = headPosition
                                 + Vector3.up * waistOffsetY
                                 + flatForward * waistForwardOffset;
            transform.rotation = Quaternion.LookRotation(flatForward);
        }

        /// <summary>レイヒットしたバーをハイライト（Materialの色変更）。</summary>
        public void SetBarHighlight(Collider? hitCollider)
        {
            foreach (var bar in _bars)
            {
                bool isHit = hitCollider != null && bar.Collider == hitCollider;
                if (bar.Material != null)
                    bar.Material.color = isHit ? bar.HighlightColor : bar.BaseColor;
            }
        }

        public void ClearBarHighlight()
        {
            foreach (var bar in _bars)
            {
                if (bar.Material != null)
                    bar.Material.color = bar.BaseColor;
            }
        }

        public WorldPanelRayBridge? GetScrollRayBridge()
        {
            return _activeScroll?.RayBridge;
        }

        private void CreateCategoryBars()
        {
            var active = new List<CategoryDefinition>();
            foreach (var c in categoryDefinitions)
            {
                if (_typeRegistry != null && _typeRegistry.GetByCategory(c.category).Any())
                    active.Add(c);
            }

            int count = active.Count;
            if (count == 0) return;

            float startAngle = -arcSpanDegrees * 0.5f;
            float step = count > 1 ? arcSpanDegrees / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angleDeg = startAngle + step * i;
                var def = active[i];
                Debug.Log($"[ScrollMenu] CreateBar: {def.category} label=\"{def.label}\" angle={angleDeg:F1}");
                _bars.Add(CreateBar(def.category, def.label, def.accent, angleDeg));
            }
        }

        private CategoryBarEntry CreateBar(NodeCategory category, string label, Color accent, float angleDeg)
        {
            var go = new GameObject($"Bar_{category}");
            go.transform.SetParent(transform, false);

            float rad = angleDeg * Mathf.Deg2Rad;
            go.transform.localPosition = new Vector3(Mathf.Sin(rad) * arcRadius, 0f, Mathf.Cos(rad) * arcRadius);
            go.transform.localRotation = Quaternion.Euler(0f, angleDeg, 0f);
            go.transform.localScale = new Vector3(barWorldWidth, barWorldHeight, 1f);

            // 共有Quadメッシュ
            var mf = go.AddComponent<MeshFilter>();
            if (SharedQuadMesh == null) SharedQuadMesh = CreateQuadMesh();
            mf.sharedMesh = SharedQuadMesh;

            // Unlitマテリアル（バーごとに1つ、軽量）
            var mr = go.AddComponent<MeshRenderer>();
            if (CachedUnlitShader == null)
                CachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            var baseColor = new Color(0f, 0f, 0f, 0.7f);
            var mat = new Material(CachedUnlitShader!);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.color = baseColor;
            mr.material = mat;

            var col = go.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = new Vector3(1f, 1f, 0.01f);

            // 左端にアクセントカラーの小さいバーを子として追加
            CreateAccentStrip(go.transform, accent);

            // テキストラベル（テクスチャ生成）
            CreateBarLabel(go.transform, label);

            var highlightColor = new Color(
                Mathf.Lerp(baseColor.r, accent.r, 0.6f),
                Mathf.Lerp(baseColor.g, accent.g, 0.6f),
                Mathf.Lerp(baseColor.b, accent.b, 0.6f),
                0.9f);

            return new CategoryBarEntry(category, go, col, mat, baseColor, highlightColor, angleDeg);
        }

        private static void CreateAccentStrip(Transform parent, Color color)
        {
            var strip = new GameObject("Accent");
            strip.transform.SetParent(parent, false);
            // 左端に細い帯（親のローカル空間は-0.5..0.5）
            strip.transform.localPosition = new Vector3(-0.48f, 0f, -0.001f);
            strip.transform.localScale = new Vector3(0.04f, 0.8f, 1f);

            var mf = strip.AddComponent<MeshFilter>();
            mf.sharedMesh = SharedQuadMesh;

            var mr = strip.AddComponent<MeshRenderer>();
            var mat = new Material(CachedUnlitShader!);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3001;
            mat.color = color;
            mr.material = mat;
        }

        /// <summary>TextMeshでバーにラベルを追加（親の非一様スケールを打ち消す）。</summary>
        private void CreateBarLabel(Transform parent, string text)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent, false);

            // 親スケール(barWorldWidth, barWorldHeight, 1)を打ち消して均一ワールドスケールにする
            float uniformSize = 0.001f;
            labelGo.transform.localScale = new Vector3(
                uniformSize / barWorldWidth,
                uniformSize / barWorldHeight,
                uniformSize);
            labelGo.transform.localPosition = new Vector3(0.05f, 0f, -0.002f);

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 100;
            tm.characterSize = 1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 1f, 1f, 0.75f);
            tm.fontStyle = FontStyle.Normal;

            Debug.Log($"[ScrollMenu] Label created: \"{text}\" go={labelGo.name} parent={parent.name}");
        }

        private ScrollPanelEntry CreateScrollPanel(CategoryBarEntry bar, List<NodeTypeInfo> nodes)
        {
            var go = new GameObject($"Scroll_{bar.Category}");
            go.transform.SetParent(transform, false);

            float rad = bar.AngleDeg * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * arcRadius;
            float z = Mathf.Cos(rad) * arcRadius;
            float baseY = barWorldHeight * 0.5f + scrollGapAboveBar;
            var baseLocalPos = new Vector3(x, baseY, z);

            go.transform.localPosition = baseLocalPos;
            go.transform.localRotation = Quaternion.Euler(0f, bar.AngleDeg, 0f);

            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<BoxCollider>();

            var panelHost = go.AddComponent<WorldPanelHost>();
            if (panelSettingsTemplate != null)
                panelHost.PanelSettingsTemplate = panelSettingsTemplate;

            var rayBridge = go.AddComponent<WorldPanelRayBridge>();

            if (scrollUxml != null)
                panelHost.Initialize(scrollUxml, scrollStyleSheet, scrollTextureWidth, scrollTextureHeight);

            // rootVisualElementは同一フレームではnullの場合がある。遅延バインドする
            StartCoroutine(PopulateScrollWhenReady(panelHost, nodes));

            var collider = go.GetComponent<BoxCollider>();

            return new ScrollPanelEntry(go, panelHost, rayBridge, collider, baseLocalPos);
        }

        /// <summary>
        /// panelHost.Rootが利用可能になるまで待ち、ノードボタンを生成する。
        /// </summary>
        private IEnumerator PopulateScrollWhenReady(WorldPanelHost panelHost, List<NodeTypeInfo> nodes)
        {
            // rootVisualElementが準備できるまで最大10フレーム待機
            const int maxRetries = 10;
            for (int i = 0; i < maxRetries; i++)
            {
                var root = panelHost.Root;
                if (root != null)
                {
                    PopulateNodeList(root, nodes);
                    yield break;
                }
                yield return null;
            }

            Debug.LogWarning("[ScrollMenu] Root was not ready after retry — scroll panel will be empty");
        }

        private void PopulateNodeList(VisualElement root, List<NodeTypeInfo> nodes)
        {
            var scrollBody = root.Q("scroll-body");
            var viewport = root.Q("node-viewport");
            var nodeList = root.Q("node-list");
            if (scrollBody == null || viewport == null || nodeList == null) return;

            const float itemHeight = 44f;
            float totalContentHeight = nodes.Count * itemHeight;

            // ノードボタン
            foreach (var info in nodes)
            {
                var typeName = info.TypeName;
                var button = new Button(() => OnNodeTypeSelected?.Invoke(typeName))
                {
                    text = info.DisplayName
                };
                button.AddToClassList("scroll-node-button");
                nodeList.Add(button);
            }

            // 右サイドバーにスライダーをC#で生成
            var sidebar = new VisualElement();
            sidebar.AddToClassList("scroll-sidebar");

            var slider = new Slider(0f, 1f, SliderDirection.Vertical);
            slider.value = 0f;
            slider.style.flexGrow = 1;

            slider.RegisterValueChangedCallback(evt =>
            {
                var viewportH = viewport.resolvedStyle.height;
                if (viewportH < 1f) viewportH = scrollTextureHeight;
                var maxScroll = Mathf.Max(0, totalContentHeight - viewportH);
                // スライダー上=0(先頭), 下=1(末尾) にするため反転
                var offset = (1f - evt.newValue) * maxScroll;
                nodeList.style.top = -offset;
            });

            sidebar.Add(slider);
            scrollBody.Add(sidebar);
        }

        private static Mesh CreateQuadMesh()
        {
            return new Mesh
            {
                name = "ScrollMenu_Quad",
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
                normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward }
            };
        }

        private void OnDestroy()
        {
            CloseScroll();

            foreach (var bar in _bars)
            {
                if (bar.Material != null) Destroy(bar.Material);
                if (bar.GameObject != null) Destroy(bar.GameObject);
            }
            _bars.Clear();
        }

        private class CategoryBarEntry
        {
            public readonly NodeCategory Category;
            public readonly GameObject GameObject;
            public readonly BoxCollider? Collider;
            public readonly Material? Material;
            public readonly Color BaseColor;
            public readonly Color HighlightColor;
            public readonly float AngleDeg;

            public CategoryBarEntry(NodeCategory category, GameObject go,
                BoxCollider? collider, Material? material,
                Color baseColor, Color highlightColor, float angleDeg)
            {
                Category = category;
                GameObject = go;
                Collider = collider;
                Material = material;
                BaseColor = baseColor;
                HighlightColor = highlightColor;
                AngleDeg = angleDeg;
            }
        }

        /// <summary>カテゴリバーの定義。Inspectorで編集可能。</summary>
        [Serializable]
        public struct CategoryDefinition
        {
            public NodeCategory category;
            public string label;
            public Color accent;

            public CategoryDefinition(NodeCategory category, string label, Color accent)
            {
                this.category = category;
                this.label = label;
                this.accent = accent;
            }
        }

        private class ScrollPanelEntry
        {
            public readonly GameObject GameObject;
            public readonly WorldPanelHost PanelHost;
            public readonly WorldPanelRayBridge RayBridge;
            public readonly BoxCollider? Collider;
            public readonly Vector3 BaseLocalPosition;

            public ScrollPanelEntry(GameObject go, WorldPanelHost panelHost,
                WorldPanelRayBridge rayBridge, BoxCollider? collider, Vector3 baseLocalPos)
            {
                GameObject = go;
                PanelHost = panelHost;
                RayBridge = rayBridge;
                Collider = collider;
                BaseLocalPosition = baseLocalPos;
            }
        }
    }
}
