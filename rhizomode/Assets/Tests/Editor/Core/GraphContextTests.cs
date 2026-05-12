#nullable enable

using System;
using NUnit.Framework;
using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// テスト用の最小ノード実装。Float入力をFloat出力にパススルーする。
    /// </summary>
    internal class PassthroughNode : NodeBase
    {
        public PassthroughNode(string id) : base(id, "Passthrough")
        {
            RegisterInput<float>("Input", ParamType.Float);
            RegisterOutput<float>("Output", ParamType.Float);
        }

        public override void Setup(GraphContext context)
        {
            var input = context.GetInputObservable<float>(this, "Input");
            AddSubscription(input.Subscribe(v => context.SetOutput(this, "Output", v)));
        }
    }

    /// <summary>
    /// テスト用のソースノード。出力ポートのみ。
    /// </summary>
    internal class SourceNode : NodeBase
    {
        public SourceNode(string id) : base(id, "Source")
        {
            RegisterOutput<float>("Value", ParamType.Float);
        }

        public override void Setup(GraphContext context) { }
    }

    /// <summary>
    /// テスト用のシンクノード。入力ポートのみ。
    /// </summary>
    internal class SinkNode : NodeBase
    {
        public SinkNode(string id) : base(id, "Sink")
        {
            RegisterInput<float>("Value", ParamType.Float);
        }

        public override void Setup(GraphContext context) { }
    }

    /// <summary>
    /// テスト用のColorノード。Color型ポートでの型不一致テスト用。
    /// </summary>
    internal class ColorSourceNode : NodeBase
    {
        public ColorSourceNode(string id) : base(id, "ColorSource")
        {
            RegisterOutput<Color>("Value", ParamType.Color);
        }

        public override void Setup(GraphContext context) { }
    }

    public class GraphContextTests
    {
        [Test]
        public void RegisterNode_AddsToContext()
        {
            using var context = new GraphContext();
            var node = new SourceNode("n1");
            context.RegisterNode(node);

            Assert.IsTrue(context.Nodes.ContainsKey("n1"));
        }

        [Test]
        public void RemoveNode_RemovesFromContext()
        {
            using var context = new GraphContext();
            var node = new SourceNode("n1");
            context.RegisterNode(node);
            context.RemoveNode("n1");

            Assert.IsFalse(context.Nodes.ContainsKey("n1"));
        }

        [Test]
        public void TryConnect_SameType_ReturnsTrue()
        {
            using var context = new GraphContext();
            context.RegisterNode(new SourceNode("n1"));
            context.RegisterNode(new SinkNode("n2"));

            bool result = context.TryConnect("n1", "Value", "n2", "Value");

            Assert.IsTrue(result);
            Assert.AreEqual(1, context.Edges.Count);
        }

        [Test]
        public void TryConnect_TypeMismatch_ReturnsFalse()
        {
            using var context = new GraphContext();
            context.RegisterNode(new ColorSourceNode("n1"));
            context.RegisterNode(new SinkNode("n2"));

            bool result = context.TryConnect("n1", "Value", "n2", "Value");

            Assert.IsFalse(result);
            Assert.AreEqual(0, context.Edges.Count);
        }

        [Test]
        public void TryConnect_InvalidPort_ReturnsFalse()
        {
            using var context = new GraphContext();
            context.RegisterNode(new SourceNode("n1"));
            context.RegisterNode(new SinkNode("n2"));

            bool result = context.TryConnect("n1", "NonExistent", "n2", "Value");

            Assert.IsFalse(result);
        }

        [Test]
        public void TryConnect_InvalidNode_ReturnsFalse()
        {
            using var context = new GraphContext();
            context.RegisterNode(new SourceNode("n1"));

            bool result = context.TryConnect("n1", "Value", "n99", "Value");

            Assert.IsFalse(result);
        }

        [Test]
        public void SignalFlow_EndToEnd()
        {
            using var context = new GraphContext();
            var source = new SourceNode("n1");
            var sink = new SinkNode("n2");

            context.RegisterNode(source);
            context.RegisterNode(sink);
            context.TryConnect("n1", "Value", "n2", "Value");

            float received = 0f;
            var inputPort = sink.GetInputPort("Value") as InputPort<float>;
            Assert.IsNotNull(inputPort);
            inputPort!.Observable.Subscribe(v => received = v);

            context.SetOutput(source, "Value", 0.42f);

            Assert.AreEqual(0.42f, received, 0.0001f);
        }

        [Test]
        public void SignalFlow_ThroughPassthroughNode()
        {
            using var context = new GraphContext();
            var source = new SourceNode("n1");
            var passthrough = new PassthroughNode("n2");
            var sink = new SinkNode("n3");

            context.RegisterNode(source);
            context.RegisterNode(passthrough);
            context.RegisterNode(sink);

            context.TryConnect("n1", "Value", "n2", "Input");
            context.TryConnect("n2", "Output", "n3", "Value");

            float received = 0f;
            var inputPort = sink.GetInputPort("Value") as InputPort<float>;
            inputPort!.Observable.Subscribe(v => received = v);

            context.SetOutput(source, "Value", 0.99f);

            Assert.AreEqual(0.99f, received, 0.0001f);
        }

        [Test]
        public void Disconnect_StopsPropagation()
        {
            using var context = new GraphContext();
            var source = new SourceNode("n1");
            var sink = new SinkNode("n2");

            context.RegisterNode(source);
            context.RegisterNode(sink);
            context.TryConnect("n1", "Value", "n2", "Value");

            float received = 0f;
            var inputPort = sink.GetInputPort("Value") as InputPort<float>;
            inputPort!.Observable.Subscribe(v => received = v);

            context.SetOutput(source, "Value", 1.0f);
            Assert.AreEqual(1.0f, received, 0.0001f);

            context.Disconnect("n1", "Value", "n2", "Value");
            context.SetOutput(source, "Value", 2.0f);

            Assert.AreEqual(1.0f, received, 0.0001f);
        }

        [Test]
        public void RemoveNode_DisconnectsAllEdges()
        {
            using var context = new GraphContext();
            var source = new SourceNode("n1");
            var sink = new SinkNode("n2");

            context.RegisterNode(source);
            context.RegisterNode(sink);
            context.TryConnect("n1", "Value", "n2", "Value");

            Assert.AreEqual(1, context.Edges.Count);

            context.RemoveNode("n1");

            Assert.AreEqual(0, context.Edges.Count);
        }

        [Test]
        public void Clear_RemovesEverything()
        {
            using var context = new GraphContext();
            context.RegisterNode(new SourceNode("n1"));
            context.RegisterNode(new SinkNode("n2"));
            context.TryConnect("n1", "Value", "n2", "Value");

            context.Clear();

            Assert.AreEqual(0, context.Nodes.Count);
            Assert.AreEqual(0, context.Edges.Count);
        }
    }
}
