#nullable enable

using System.Collections.Generic;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine.UIElements;

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="NodeVisualController"/> の partial: タイトル / カテゴリ / ポート UI の構築。
    /// Phase 9 Round A で本体から分離。
    /// </summary>
    public partial class NodeVisualController
    {
        private static void SetTitle(VisualElement root, string title)
        {
            var label = root.Q<Label>("node-title");
            if (label != null)
                label.text = title;
        }

        private static void ApplyCategoryStyle(VisualElement root, NodeCategory category)
        {
            var header = root.Q("header");
            if (header == null) return;

            var className = category switch
            {
                NodeCategory.Input => "node-header--input",
                NodeCategory.Math => "node-header--math",
                NodeCategory.VFX => "node-header--vfx",
                NodeCategory.Shader => "node-header--shader",
                NodeCategory.Time => "node-header--time",
                NodeCategory.Utility => "node-header--utility",
                _ => "node-header--utility"
            };

            header.AddToClassList(className);
        }

        /// <summary>
        /// Rector風スロットバーを上部（入力）・下部（出力）に配置する。
        /// </summary>
        private static void BuildSlotBars(VisualElement root, IReadOnlyList<PortDefinition> ports)
        {
            var topSlots = root.Q("slot-list-top");
            var bottomSlots = root.Q("slot-list-bottom");
            if (topSlots == null || bottomSlots == null) return;

            topSlots.Clear();
            bottomSlots.Clear();

            foreach (var port in ports)
            {
                var bar = new VisualElement();
                bar.AddToClassList("port-dot");
                var typeClass = port.type switch
                {
                    ParamType.Float => "port-dot--float",
                    ParamType.Color => "port-dot--color",
                    ParamType.Bool => "port-dot--bool",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(typeClass))
                    bar.AddToClassList(typeClass);

                if (port.direction == PortDirection.Input)
                    topSlots.Add(bar);
                else
                    bottomSlots.Add(bar);
            }
        }

        private void BuildPortUI(VisualElement root, IReadOnlyList<PortDefinition> ports)
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
    }
}
