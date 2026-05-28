#nullable enable

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// 16-step Rector-style counter: Trigger rising edges cycle 1..16 across sixteen one-hot
    /// bool outputs (plus an Index float).
    /// </summary>
    [NodeType("Count16", "Count 16", NodeCategory.Utility)]
    public class Count16Node : StepCountNodeBase
    {
        private const int Steps = 16;

        public Count16Node(string id) : base(id, "Count16", Steps)
        {
        }
    }
}
