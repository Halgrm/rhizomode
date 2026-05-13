#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;

namespace Rhizomode.UI
{
    /// <summary>
    /// 巻物メニューの視覚コンポーネント。カテゴリバー（常時表示・軽量Quad）と
    /// スクロールパネル（展開時のみ・WorldPanelHost）を管理する。
    /// バーはUIToolkit不使用（メモリ削減）。スクロールのみWorldPanelHost。
    /// </summary>
    /// <remarks>
    /// Phase 9 Round B で partial class に分割:
    /// - <c>ScrollMenuVisualController.cs</c> (本ファイル): public API / フィールド / 内部 DTO / Initialize / OnDestroy
    /// - <c>ScrollMenuVisualController.Bars.cs</c>: カテゴリバー (Quad + TextMesh) の構築
    /// - <c>ScrollMenuVisualController.Scroll.cs</c>: スクロールパネル (WorldPanelHost) の構築 + 遅延 populate
    /// </remarks>
    public partial class ScrollMenuVisualController : MonoBehaviour
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
