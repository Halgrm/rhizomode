#nullable enable

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// ノード生成メニューのUIコントローラー。カテゴリ選択→ノードタイプ選択の
    /// 2段階メニューを管理し、選択されたノードタイプを通知する。
    /// </summary>
    [RequireComponent(typeof(WorldPanelHost))]
    public class NodeCreationMenuController : MonoBehaviour
    {
        private const float MenuSpawnDistance = 0.6f;
        private const float MenuWorldWidth = 0.25f;
        private const float MenuWorldHeight = 0.35f;
        private const int MenuTextureWidth = 400;
        private const int MenuTextureHeight = 560;
        private const float ScrollStep = 50f;

        [SerializeField] private VisualTreeAsset? menuUxml;
        [SerializeField] private StyleSheet? menuStyleSheet;

        private WorldPanelHost? _panelHost;
        private NodeTypeRegistry? _typeRegistry;

        private VisualElement? _categoryViewport;
        private VisualElement? _categoryList;
        private VisualElement? _nodeViewport;
        private VisualElement? _nodeList;
        private Button? _scrollUpBtn;
        private Button? _scrollDownBtn;

        /// <summary>現在アクティブなビューポートとリスト。</summary>
        private VisualElement? _activeViewport;
        private VisualElement? _activeList;
        private float _scrollOffset;

        /// <summary>ノードタイプが選択された時に発火する。引数はタイプ名。</summary>
        public event Action<string>? OnNodeTypeSelected;

        /// <summary>メニューが表示中かどうか。</summary>
        public bool IsVisible { get; private set; }

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
            SetVisualActive(false);
        }

        /// <summary>
        /// NodeTypeRegistryを設定する。
        /// </summary>
        public void Initialize(NodeTypeRegistry typeRegistry)
        {
            _typeRegistry = typeRegistry;
        }

        /// <summary>
        /// メニューを表示する。頭の正面位置にワールド固定でスポーンする。
        /// </summary>
        public void Show(Vector3 headPosition, Vector3 headForward, Quaternion headRotation)
        {
            if (menuUxml == null || _panelHost == null) return;

            if (!_panelHost.IsInitialized)
            {
                _panelHost.Initialize(menuUxml, menuStyleSheet, MenuTextureWidth, MenuTextureHeight);
                _panelHost.Resize(MenuWorldWidth, MenuWorldHeight);
                CacheElements();
            }

            var spawnPos = headPosition + headForward * MenuSpawnDistance;
            transform.position = spawnPos;
            transform.rotation = Quaternion.LookRotation(transform.position - headPosition);

            SetVisualActive(true);
            ShowCategories();
            IsVisible = true;
        }

        /// <summary>メニューを非表示にする。</summary>
        public void Hide()
        {
            if (!IsVisible) return;

            IsVisible = false;
            SetVisualActive(false);
        }

        private void SetVisualActive(bool active)
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.enabled = active;

            var collider = GetComponent<MeshCollider>();
            if (collider != null)
                collider.enabled = active;
        }

        private void CacheElements()
        {
            var root = _panelHost?.Root;
            if (root == null) return;

            _categoryViewport = root.Q("category-viewport");
            _categoryList = root.Q("category-list");
            _nodeViewport = root.Q("node-viewport");
            _nodeList = root.Q("node-list");

            _scrollUpBtn = root.Q<Button>("scroll-up-btn");
            _scrollDownBtn = root.Q<Button>("scroll-down-btn");

            _scrollUpBtn?.RegisterCallback<ClickEvent>(_ => Scroll(-ScrollStep));
            _scrollDownBtn?.RegisterCallback<ClickEvent>(_ => Scroll(ScrollStep));
        }

        private void ShowCategories()
        {
            if (_categoryList == null || _nodeList == null || _typeRegistry == null) return;

            _categoryViewport!.style.display = DisplayStyle.Flex;
            _nodeViewport!.style.display = DisplayStyle.None;
            _categoryList.Clear();

            var categories = new[]
            {
                (NodeCategory.Input, "Input"),
                (NodeCategory.Math, "Math / Signal"),
                (NodeCategory.VFX, "VFX"),
                (NodeCategory.Shader, "Shader"),
                (NodeCategory.Time, "Time"),
                (NodeCategory.Utility, "Utility"),
                (NodeCategory.Scene, "Scene")
            };

            foreach (var (category, label) in categories)
            {
                if (!_typeRegistry.GetByCategory(category).Any())
                    continue;

                var button = new Button(() => ShowNodesForCategory(category))
                {
                    text = label
                };
                button.AddToClassList("category-button");

                var colorClass = category switch
                {
                    NodeCategory.Input => "category-button--input",
                    NodeCategory.Math => "category-button--math",
                    NodeCategory.VFX => "category-button--vfx",
                    NodeCategory.Shader => "category-button--shader",
                    NodeCategory.Time => "category-button--time",
                    NodeCategory.Utility => "category-button--utility",
                    NodeCategory.Scene => "category-button--scene",
                    _ => "category-button--utility"
                };
                button.AddToClassList(colorClass);

                _categoryList.Add(button);
            }

            SetActiveScrollTarget(_categoryViewport!, _categoryList);
        }

        private void ShowNodesForCategory(NodeCategory category)
        {
            if (_categoryList == null || _nodeList == null || _typeRegistry == null) return;

            _categoryViewport!.style.display = DisplayStyle.None;
            _nodeViewport!.style.display = DisplayStyle.Flex;
            _nodeList.Clear();

            var backButton = new Button(ShowCategories)
            {
                text = "← Back"
            };
            backButton.AddToClassList("back-button");
            _nodeList.Add(backButton);

            foreach (var info in _typeRegistry.GetByCategory(category))
            {
                var typeName = info.TypeName;
                var button = new Button(() => OnNodeSelected(typeName))
                {
                    text = info.DisplayName
                };
                button.AddToClassList("node-button");
                _nodeList.Add(button);
            }

            SetActiveScrollTarget(_nodeViewport!, _nodeList);
        }

        private void OnNodeSelected(string nodeType)
        {
            OnNodeTypeSelected?.Invoke(nodeType);
            Hide();
        }

        private void SetActiveScrollTarget(VisualElement viewport, VisualElement list)
        {
            _activeViewport = viewport;
            _activeList = list;
            _scrollOffset = 0;
            list.style.top = 0;
        }

        private void Scroll(float delta)
        {
            if (_activeViewport == null || _activeList == null) return;

            var viewportHeight = _activeViewport.resolvedStyle.height;
            var listHeight = _activeList.resolvedStyle.height;
            var maxScroll = Mathf.Max(0, listHeight - viewportHeight);

            _scrollOffset = Mathf.Clamp(_scrollOffset + delta, 0, maxScroll);
            _activeList.style.top = -_scrollOffset;
        }
    }
}
