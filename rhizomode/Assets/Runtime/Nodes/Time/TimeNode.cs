#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Time
{
    /// <summary>
    /// 毎フレームTime.timeを出力する時間ソースノード。
    /// </summary>
    [NodeType("Time", "Time", NodeCategory.Time)]
    public class TimeNode : NodeBase
    {
        private readonly OutputPort<float> _timeOut;

        public TimeNode(string id) : base(id, "Time")
        {
            _timeOut = RegisterOutput<float>("Time", ParamType.Float, PortUnit.Seconds);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                Observable.EveryUpdate()
                    .Subscribe(_ => _timeOut.Emit(UnityEngine.Time.time)));
        }
    }
}
