#nullable enable

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

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

        [SerializeField] private VisualTreeAsset? menuUxml;
        [SerializeField] private StyleSheet? menuStyleSheet;

        private WorldPanelHost? _panelHost;
        private NodeTypeRegistry? _typeRegistry;
        private VisualElement? _categoryList;
        private VisualElement? _nodeList;

        /// <summary>ノードタイプが選択された時に発火する。引数はタイプ名。</summary>
        public event Action<string>? OnNodeTypeSelected;

        /// <summary>メニューが表示中かどうか。</summary>
        public bool IsVisible { get; private set; }

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
            gameObject.SetActive(false);
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

            // 頭の前方にメニューを配置
            var spawnPos = headPosition + headForward * MenuSpawnDistance;
            transform.position = spawnPos;
            // メニューをプレイヤーに向ける
            transform.rotation = Quaternion.LookRotation(transform.position - headPosition);

            if (!IsVisible)
            {
                gameObject.SetActive(true);
                _panelHost.Resize(MenuWorldWidth, MenuWorldHeight);

                if (_panelHost.Root == null)
                {
                    _panelHost.Initialize(menuUxml, menuStyleSheet, MenuTextureWidth, MenuTextureHeight);
                }

                CacheElements();
            }

            ShowCategories();
            IsVisible = true;
        }

        /// <summary>メニューを非表示にする。</summary>
        public void Hide()
        {
            if (!IsVisible) return;

            IsVisible = false;
            gameObject.SetActive(false);
        }

        private void CacheElements()
        {
            var root = _panelHost?.Root;
            if (root == null) return;

            _categoryList = root.Q("category-list");
            _nodeList = root.Q("node-list");
        }

        private void ShowCategories()
        {
            if (_categoryList == null || _nodeList == null || _typeRegistry == null) return;

            _categoryList.style.display = DisplayStyle.Flex;
            _nodeList.style.display = DisplayStyle.None;
            _categoryList.Clear();

            var categories = new[]
            {
                (NodeCategory.Input, "Input"),
                (NodeCategory.Math, "Math / Signal"),
                (NodeCategory.Module, "Modules"),
                (NodeCategory.Time, "Time"),
                (NodeCategory.Utility, "Utility")
            };

            foreach (var (category, label) in categories)
            {
                // カテゴリにノードが登録されていない場合はスキップ
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
                    NodeCategory.Module => "category-button--module",
                    NodeCategory.Time => "category-button--time",
                    NodeCategory.Utility => "category-button--utility",
                    _ => "category-button--utility"
                };
                button.AddToClassList(colorClass);

                _categoryList.Add(button);
            }
        }

        private void ShowNodesForCategory(NodeCategory category)
        {
            if (_categoryList == null || _nodeList == null || _typeRegistry == null) return;

            _categoryList.style.display = DisplayStyle.None;
            _nodeList.style.display = DisplayStyle.Flex;
            _nodeList.Clear();

            // 戻るボタン
            var backButton = new Button(ShowCategories)
            {
                text = "← Back"
            };
            backButton.AddToClassList("back-button");
            _nodeList.Add(backButton);

            // ノードタイプボタン
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
        }

        private void OnNodeSelected(string nodeType)
        {
            OnNodeTypeSelected?.Invoke(nodeType);
            Hide();
        }
    }
}
