#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.Serialization;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Tests
{
    public class GraphHydratorTests
    {
        [Test]
        public void Build_EmptyGraphData_ReturnsEmptyPlan()
        {
            var data = new GraphData();
            var plan = new GraphHydrator().Build(data);

            Assert.AreEqual(0, plan.Nodes.Count);
            Assert.AreEqual(0, plan.Edges.Count);
        }

        [Test]
        public void Build_NodeWithPositionAndParams_PreservesAll()
        {
            var data = new GraphData
            {
                nodes =
                {
                    new NodeData
                    {
                        id = "n1",
                        type = "Stub",
                        position = new[] { 1f, 2f, 3f },
                        paramsJson = "{\"value\":42}"
                    }
                }
            };

            var plan = new GraphHydrator().Build(data);

            Assert.AreEqual(1, plan.Nodes.Count);
            var entry = plan.Nodes[0];
            Assert.AreEqual("n1", entry.NodeId);
            Assert.AreEqual("Stub", entry.TypeName);
            Assert.AreEqual(new RzVector3(1, 2, 3), entry.Position);
            Assert.AreEqual("{\"value\":42}", entry.ParamsJson);
            Assert.AreEqual(0, entry.ParamValues.Count, "ParamValues empty until INodeParamReader implemented");
        }

        [Test]
        public void Build_EdgeData_PreservesEndpoints()
        {
            var data = new GraphData
            {
                edges =
                {
                    new EdgeData
                    {
                        id = "e1",
                        from = "a",
                        fromPort = "Out",
                        to = "b",
                        toPort = "In"
                    }
                }
            };

            var plan = new GraphHydrator().Build(data);

            Assert.AreEqual(1, plan.Edges.Count);
            var edge = plan.Edges[0];
            Assert.AreEqual("e1", edge.EdgeId);
            Assert.AreEqual("a", edge.FromNodeId);
            Assert.AreEqual("Out", edge.FromPortName);
            Assert.AreEqual("b", edge.ToNodeId);
            Assert.AreEqual("In", edge.ToPortName);
        }

        [Test]
        public void Build_NullParamsJson_BecomesEmptyString()
        {
            var data = new GraphData
            {
                nodes =
                {
                    new NodeData { id = "n1", type = "Stub", paramsJson = null! }
                }
            };

            var plan = new GraphHydrator().Build(data);

            Assert.AreEqual("", plan.Nodes[0].ParamsJson);
        }

        [Test]
        public void Build_PositionWithShortArray_PadsWithZeros()
        {
            var data = new GraphData
            {
                nodes =
                {
                    new NodeData { id = "n1", type = "Stub", position = new[] { 5f } }
                }
            };

            var plan = new GraphHydrator().Build(data);

            Assert.AreEqual(new RzVector3(5, 0, 0), plan.Nodes[0].Position);
        }
    }
}
