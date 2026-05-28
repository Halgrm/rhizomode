#nullable enable

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// 4-step Rector-style counter: Trigger rising edges cycle 1→2→3→4→1 across four one-hot
    /// bool outputs (plus an Index float).
    /// </summary>
    [NodeType("Count", "Count 4", NodeCategory.Utility)]
    public class CountNode : StepCountNodeBase
    {
        private const int Steps = 4;

        public CountNode(string id) : base(id, "Count", Steps)
        {
        }
    }
}
