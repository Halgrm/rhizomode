#nullable enable

using System.Collections.Generic;
using Rhizomode.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// 1つのノードのWorldSpace表示を制御する。NodeBaseの状態を
    /// UIToolkitパネルに反映し、ポートの視覚表現を管理する。
    /// </summary>
    [RequireComponent(typeof(WorldPanelHost))]
    public class NodeVisualController : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset? portUxml;

        private WorldPanelHost? _panelHost;
        private NodeBase? _node;
        private NodeTypeInfo? _typeInfo;
        private readonly Dictionary<string, VisualElement> _portElements = new();

        /// <summary>バインドされたノード。</summary>
        public NodeBase? Node => _node;

        /// <summary>ノードタイプ情報。</summary>
        public NodeTypeInfo? TypeInfo => _typeInfo;

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
        }

        /// <summary>
        /// ノードデータをバインドし、UIを構築する。
        /// </summary>
        public void Bind(NodeBase node, NodeTypeInfo typeInfo)
        {
            _node = node;
            _typeInfo = typeInfo;

            var root = _panelHost?.Root;
            if (root == null) return;

            SetTitle(root, typeInfo.DisplayName);
            ApplyCategoryStyle(root, typeInfo.Category);
            BuildPortUI(root, node.GetPortDefinitions());

            // 初期位置を反映
            transform.position = node.Position;
        }

        /// <summary>
        /// 指定ポート名のワールド座標を返す。エッジ描画用。
        /// </summary>
        public Vector3 GetPortWorldPosition(string portName)
        {
            if (!_portElements.TryGetValue(portName, out var element))
                return transform.position;

            // ポート要素のパネル内位置からワールド座標を概算
            var rect = element.worldBound;
            var panelCenter = new Vector2(
                rect.x + rect.width * 0.5f,
                rect.y + rect.height * 0.5f
            );

            return PanelToWorldPosition(panelCenter);
        }

        private void LateUpdate()
        {
            // ノード位置をTransformと同期（グラブ移動対応）
            if (_node != null)
            {
                _node.Position = transform.position;
            }
        }

        private void SetTitle(VisualElement root, string title)
        {
            var label = root.Q<Label>("node-title");
            if (label != null)
                label.text = title;
        }

        private void ApplyCategoryStyle(VisualElement root, NodeCategory category)
        {
            var header = root.Q("header");
            if (header == null) return;

            var className = category switch
            {
                NodeCategory.Input => "node-header--input",
                NodeCategory.Math => "node-header--math",
                NodeCategory.Module => "node-header--module",
                NodeCategory.Time => "node-header--time",
                NodeCategory.Utility => "node-header--utility",
                _ => "node-header--utility"
            };

            header.AddToClassList(className);
        }

        private void BuildPortUI(VisualElement root, List<PortDefinition> ports)
        {
            var inputContainer = root.Q("input-ports");
            var outputContainer = root.Q("output-ports");
            if (inputContainer == null || outputContainer == null) return;

            inputContainer.Clear();
            outputContainer.Clear();
            _portElements.Clear();

            foreach (var port in ports)
            {
                var container = port.direction == PortDirection.Input
                    ? inputContainer
                    : outputContainer;

                var portElement = CreatePortElement(port);
                container.Add(portElement);
                _portElements[port.name] = portElement;
            }
        }

        private VisualElement CreatePortElement(PortDefinition port)
        {
            VisualElement element;

            if (portUxml != null)
            {
                element = portUxml.Instantiate();
            }
            else
            {
                element = CreatePortElementFallback();
            }

            // ポート名を設定
            var label = element.Q<Label>("port-label");
            if (label != null)
                label.text = port.name;

            // ポートの型に応じた色クラスを追加
            var dot = element.Q("port-dot");
            if (dot != null)
            {
                var typeClass = port.type switch
                {
                    ParamType.Float => "port-dot--float",
                    ParamType.Color => "port-dot--color",
                    ParamType.Bool => "port-dot--bool",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(typeClass))
                    dot.AddToClassList(typeClass);
            }

            return element;
        }

        private static VisualElement CreatePortElementFallback()
        {
            var row = new VisualElement();
            row.AddToClassList("port-row");

            var dot = new VisualElement();
            dot.name = "port-dot";
            dot.AddToClassList("port-dot");
            row.Add(dot);

            var label = new Label();
            label.name = "port-label";
            label.AddToClassList("port-label");
            row.Add(label);

            return row;
        }

        private Vector3 PanelToWorldPosition(Vector2 panelPos)
        {
            if (_panelHost == null) return transform.position;

            // パネル座標（ピクセル）を-0.5〜0.5の正規化座標に変換
            float nx = (panelPos.x / _panelHost.TextureWidth) - 0.5f;
            float ny = 0.5f - (panelPos.y / _panelHost.TextureHeight);

            // ローカル座標に変換（Quadは-0.5〜0.5のスケール）
            var localPos = new Vector3(
                nx * _panelHost.WorldWidth,
                ny * _panelHost.WorldHeight,
                0f
            );

            return transform.TransformPoint(localPos);
        }
    }
}
