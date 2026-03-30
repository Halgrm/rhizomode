#nullable enable

using NUnit.Framework;
using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.Core.Tests
{
    public class SerializationTests
    {
        private GraphContext CreateContextWithFactories()
        {
            var context = new GraphContext();
            context.RegisterNodeFactory("Source", id => new SourceNode(id));
            context.RegisterNodeFactory("Sink", id => new SinkNode(id));
            context.RegisterNodeFactory("Passthrough", id => new PassthroughNode(id));
            return context;
        }

        [Test]
        public void Serialize_EmptyGraph_ReturnsValidData()
        {
            using var context = new GraphContext();
            var data = context.Serialize();

            Assert.AreEqual("1.0", data.version);
            Assert.AreEqual(0, data.nodes.Count);
            Assert.AreEqual(0, data.edges.Count);
        }

        [Test]
        public void Serialize_PreservesNodes()
        {
            using var context = new GraphContext();
            context.RegisterNode(new SourceNode("n1") { Position = new Vector3(1, 2, 3) });
            context.RegisterNode(new SinkNode("n2") { Position = new Vector3(4, 5, 6) });

            var data = context.Serialize();

            Assert.AreEqual(2, data.nodes.Count);
        }

        [Test]
        public void Serialize_PreservesEdges()
        {
            using var context = new GraphContext();
            context.RegisterNode(new SourceNode("n1"));
            context.RegisterNode(new SinkNode("n2"));
            context.TryConnect("n1", "Value", "n2", "Value");

            var data = context.Serialize();

            Assert.AreEqual(1, data.edges.Count);
            Assert.AreEqual("n1", data.edges[0].from);
            Assert.AreEqual("Value", data.edges[0].fromPort);
            Assert.AreEqual("n2", data.edges[0].to);
            Assert.AreEqual("Value", data.edges[0].toPort);
        }

        [Test]
        public void RoundTrip_PreservesNodeCount()
        {
            using var original = CreateContextWithFactories();
            original.RegisterNode(new SourceNode("n1"));
            original.RegisterNode(new SinkNode("n2"));

            var data = original.Serialize();

            using var restored = CreateContextWithFactories();
            restored.Deserialize(data);

            Assert.AreEqual(2, restored.Nodes.Count);
            Assert.IsTrue(restored.Nodes.ContainsKey("n1"));
            Assert.IsTrue(restored.Nodes.ContainsKey("n2"));
        }

        [Test]
        public void RoundTrip_PreservesEdges()
        {
            using var original = CreateContextWithFactories();
            original.RegisterNode(new SourceNode("n1"));
            original.RegisterNode(new SinkNode("n2"));
            original.TryConnect("n1", "Value", "n2", "Value");

            var data = original.Serialize();

            using var restored = CreateContextWithFactories();
            restored.Deserialize(data);

            Assert.AreEqual(1, restored.Edges.Count);
        }

        [Test]
        public void RoundTrip_PreservesPositions()
        {
            var pos = new Vector3(1.5f, 2.5f, 3.5f);

            using var original = CreateContextWithFactories();
            original.RegisterNode(new SourceNode("n1") { Position = pos });

            var data = original.Serialize();

            using var restored = CreateContextWithFactories();
            restored.Deserialize(data);

            var restoredPos = restored.Nodes["n1"].Position;
            Assert.AreEqual(pos.x, restoredPos.x, 0.001f);
            Assert.AreEqual(pos.y, restoredPos.y, 0.001f);
            Assert.AreEqual(pos.z, restoredPos.z, 0.001f);
        }

        [Test]
        public void RoundTrip_SignalFlowWorks()
        {
            using var original = CreateContextWithFactories();
            original.RegisterNode(new SourceNode("n1"));
            original.RegisterNode(new SinkNode("n2"));
            original.TryConnect("n1", "Value", "n2", "Value");

            var data = original.Serialize();

            using var restored = CreateContextWithFactories();
            restored.Deserialize(data);

            float received = 0f;
            var inputPort = restored.Nodes["n2"].GetInputPort("Value") as InputPort<float>;
            inputPort!.Observable.Subscribe(v => received = v);

            var source = restored.Nodes["n1"];
            restored.SetOutput(source, "Value", 0.88f);

            Assert.AreEqual(0.88f, received, 0.0001f);
        }

        [Test]
        public void NodeData_PositionConversion()
        {
            var nodeData = new NodeData
            {
                position = new[] { 1.0f, 2.0f, 3.0f }
            };

            var vec = nodeData.ToVector3();
            Assert.AreEqual(1.0f, vec.x, 0.001f);
            Assert.AreEqual(2.0f, vec.y, 0.001f);
            Assert.AreEqual(3.0f, vec.z, 0.001f);

            var arr = NodeData.FromVector3(new Vector3(4, 5, 6));
            Assert.AreEqual(4.0f, arr[0], 0.001f);
            Assert.AreEqual(5.0f, arr[1], 0.001f);
            Assert.AreEqual(6.0f, arr[2], 0.001f);
        }

        [Test]
        public void JsonUtility_RoundTrip()
        {
            var data = new GraphData();
            data.nodes.Add(new NodeData
            {
                id = "n1",
                type = "Source",
                position = new[] { 1.0f, 2.0f, 3.0f },
                paramsJson = "{}",
                groupId = ""
            });
            data.edges.Add(new EdgeData
            {
                id = "e1",
                from = "n1",
                fromPort = "Value",
                to = "n2",
                toPort = "Value"
            });

            string json = JsonUtility.ToJson(data, true);
            var restored = JsonUtility.FromJson<GraphData>(json);

            Assert.IsNotNull(restored);
            Assert.AreEqual(1, restored!.nodes.Count);
            Assert.AreEqual("n1", restored.nodes[0].id);
            Assert.AreEqual("Source", restored.nodes[0].type);
            Assert.AreEqual(1, restored.edges.Count);
            Assert.AreEqual("e1", restored.edges[0].id);
        }

        [Test]
        public void Deserialize_UnknownNodeType_SkipsGracefully()
        {
            var data = new GraphData();
            data.nodes.Add(new NodeData { id = "n1", type = "UnknownType" });

            using var context = CreateContextWithFactories();
            context.Deserialize(data);

            Assert.AreEqual(0, context.Nodes.Count);
        }
    }
}
