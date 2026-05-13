#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// Float入力値をリアルタイム表示するモニターノード。
    /// </summary>
    [NodeType("FloatMonitor", "Float Monitor", NodeCategory.Utility)]
    public class FloatMonitorNode : NodeBase, IInlineMonitor
    {
        private float _value;

        public FloatMonitorNode(string id) : base(id, "FloatMonitor")
        {
            RegisterInput<float>("Value", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "Value")
                    .Subscribe(v => { _value = v; }));
        }

        ParamType IInlineMonitor.MonitorType => ParamType.Float;
        string IInlineMonitor.MonitorDisplayValue => _value.ToString("F3");
        Color IInlineMonitor.MonitorColor => Color.white;
    }
}
