#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.UI.Contracts;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// Color入力値をリアルタイム表示するモニターノード。色スウォッチ付き。
    /// </summary>
    [NodeType("ColorMonitor", "Color Monitor", NodeCategory.Utility)]
    public class ColorMonitorNode : NodeBase, IInlineMonitor
    {
        private Color _value = Color.black;

        public ColorMonitorNode(string id) : base(id, "ColorMonitor")
        {
            RegisterInput<Color>("Value", ParamType.Color);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<Color>(this, "Value")
                    .Subscribe(v => { _value = v; }));
        }

        ParamType IInlineMonitor.MonitorType => ParamType.Color;

        string IInlineMonitor.MonitorDisplayValue =>
            $"({_value.r:F2}, {_value.g:F2}, {_value.b:F2})";

        Color IInlineMonitor.MonitorColor => _value;
    }
}
