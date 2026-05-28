#nullable enable

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// 8-step Rector-style counter: Trigger rising edges cycle 1..8 across eight one-hot bool
    /// outputs (plus an Index float).
    /// </summary>
    [NodeType("Count8", "Count 8", NodeCategory.Utility)]
    public class Count8Node : StepCountNodeBase
    {
        private const int Steps = 8;

        public Count8Node(string id) : base(id, "Count8", Steps)
        {
        }
    }
}
