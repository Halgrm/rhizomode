#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// Bool入力値をリアルタイム表示するモニターノード。
    /// </summary>
    public class BoolMonitorNode : NodeBase, IInlineMonitor
    {
        private bool _value;

        public BoolMonitorNode(string id) : base(id, "BoolMonitor")
        {
            RegisterInput<bool>("Value", ParamType.Bool);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<bool>(this, "Value")
                    .Subscribe(v => { _value = v; }));
        }

        ParamType IInlineMonitor.MonitorType => ParamType.Bool;
        string IInlineMonitor.MonitorDisplayValue => _value ? "TRUE" : "FALSE";
        Color IInlineMonitor.MonitorColor => Color.white;
    }
}
