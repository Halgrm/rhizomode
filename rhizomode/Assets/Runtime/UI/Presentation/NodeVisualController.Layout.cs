#nullable enable

using Rhizomode.SharedKernel;
using Rhizomode.UI.Contracts;
using UnityEngine.UIElements;

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="NodeVisualController"/> の partial: タイトル / カテゴリ / ポート UI の構築。
    /// Phase 9 Round A で本体から分離。
    /// Round E (E3+E4) で <see cref="INodeView"/> を直接受け取る形に変更し、
    /// Graph.Model.PortDefinition / PortDirection 依存を撤廃。
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
        private static void BuildSlotBars(VisualElement root, INodeView node)
        {
            var topSlots = root.Q("slot-list-top");
            var bottomSlots = root.Q("slot-list-bottom");
            if (topSlots == null || bottomSlots == null) return;

            topSlots.Clear();
            bottomSlots.Clear();

            foreach (var port in node.InputPorts)
                topSlots.Add(CreateSlotBar(port.PortType));
            foreach (var port in node.OutputPorts)
                bottomSlots.Add(CreateSlotBar(port.PortType));
        }

        private static VisualElement CreateSlotBar(ParamType portType)
        {
            var bar = new VisualElement();
            bar.AddToClassList("port-dot");
            var typeClass = portType switch
            {
                ParamType.Float => "port-dot--float",
                ParamType.Color => "port-dot--color",
                ParamType.Bool => "port-dot--bool",
                _ => ""
            };
            if (!string.IsNullOrEmpty(typeClass))
                bar.AddToClassList(typeClass);
            return bar;
        }

        private void BuildPortUI(VisualElement root, INodeView node)
        {
            var inputContainer = root.Q("input-ports");
            var outputContainer = root.Q("output-ports");
            if (inputContainer == null || outputContainer == null) return;

            inputContainer.Clear();
            outputContainer.Clear();
            _portElements.Clear();

            foreach (var port in node.InputPorts)
            {
                var portElement = CreatePortElement(port);
                inputContainer.Add(portElement);
                _portElements[port.PortName] = portElement;
            }
            foreach (var port in node.OutputPorts)
            {
                var portElement = CreatePortElement(port);
                outputContainer.Add(portElement);
                _portElements[port.PortName] = portElement;
            }
        }

        private VisualElement CreatePortElement(PortViewModel port)
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

            // ポート名 + 単位ラベル (例: "Hz", "BPM") を設定。Unit == None なら名前のみ。
            var label = element.Q<Label>("port-label");
            if (label != null)
                label.text = FormatPortLabel(port);

            // ポートの型に応じた色クラスを追加
            var dot = element.Q("port-dot");
            if (dot != null)
            {
                var typeClass = port.PortType switch
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

        private static string FormatPortLabel(PortViewModel port)
        {
            var symbol = UnitSymbol(port.Unit);
            return string.IsNullOrEmpty(symbol) ? port.PortName : $"{port.PortName} ({symbol})";
        }

        private static string UnitSymbol(PortUnit unit) => unit switch
        {
            PortUnit.Hz => "Hz",
            PortUnit.Bpm => "BPM",
            PortUnit.Seconds => "s",
            PortUnit.Milliseconds => "ms",
            PortUnit.Decibels => "dB",
            PortUnit.Normalized => "0-1",
            PortUnit.Phase => "φ",
            PortUnit.Note => "note",
            PortUnit.Degrees => "°",
            _ => "",
        };

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
