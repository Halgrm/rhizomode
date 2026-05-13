#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Rhizomode.Graph.Snapshot;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Tests
{
    public class GraphSnapshotRoundTripTests
    {
        [Test]
        public void NodeSnapshot_RecordEquality_ByFieldValue()
        {
            var paramsA = new Dictionary<string, ParamValue>
            {
                { "Value", ParamValue.Float(1.0f) }
            };
            var a = new NodeSnapshot("n1", "ConstFloat", new RzVector3(0, 0, 0), paramsA);
            var b = new NodeSnapshot("n1", "ConstFloat", new RzVector3(0, 0, 0), paramsA);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void GraphSnapshot_Empty_HasEmptyLists()
        {
            Assert.AreEqual(0, GraphSnapshot.Empty.Nodes.Count);
            Assert.AreEqual(0, GraphSnapshot.Empty.Edges.Count);
        }

        [Test]
        public void EdgeSnapshot_RecordEquality_ByFieldValue()
        {
            var a = new EdgeSnapshot("e1", "n1", "Out", "n2", "In");
            var b = new EdgeSnapshot("e1", "n1", "Out", "n2", "In");
            Assert.AreEqual(a, b);
        }
    }
}
