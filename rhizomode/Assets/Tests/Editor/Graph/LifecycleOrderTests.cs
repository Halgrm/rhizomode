#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Tests
{
    public class LifecycleOrderTests
    {
        private sealed class StubNode : NodeBase
        {
            public bool SetupCalled { get; private set; }

            public StubNode(string id) : base(id, "Stub") { }

            public override void Setup(GraphState context)
            {
                SetupCalled = true;
            }
        }

        /// <summary>呼び出し順序を記録する fake processor。</summary>
        private sealed class RecordingProcessor : INodeLifecycleProcessor
        {
            public List<string> Calls { get; } = new();
            public string Label { get; }

            public RecordingProcessor(string label) { Label = label; }

            public void BeforeSetup(NodeBase node, NodeInitMode mode)
                => Calls.Add($"{Label}.BeforeSetup({node.Id},{mode})");

            public void AfterSetup(NodeBase node, NodeInitMode mode)
                => Calls.Add($"{Label}.AfterSetup({node.Id},{mode})");

            public void AfterDeserialize(GraphState state)
                => Calls.Add($"{Label}.AfterDeserialize");
        }

        [Test]
        public void RegisterNode_Order_BeforeSetup_Setup_AfterSetup()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var p = new RecordingProcessor("P1");
            var runtime = new NodeRuntime(state, bus, new[] { (INodeLifecycleProcessor)p });

            var node = new StubNode("n1");
            runtime.RegisterNode(node, NodeInitMode.FreshSpawn);

            // BeforeSetup → Setup → AfterSetup の順、Setup は GraphState.RegisterNode 内で呼ばれる
            Assert.IsTrue(node.SetupCalled);
            CollectionAssert.AreEqual(new[]
            {
                "P1.BeforeSetup(n1,FreshSpawn)",
                "P1.AfterSetup(n1,FreshSpawn)"
            }, p.Calls);
        }

        [Test]
        public void RegisterNode_MultipleProcessors_AllInvokedInOrder()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var p1 = new RecordingProcessor("P1");
            var p2 = new RecordingProcessor("P2");
            var runtime = new NodeRuntime(state, bus,
                new INodeLifecycleProcessor[] { p1, p2 });

            runtime.RegisterNode(new StubNode("n1"), NodeInitMode.Deserialize);

            // P1.BeforeSetup → P2.BeforeSetup → (Setup) → P1.AfterSetup → P2.AfterSetup
            CollectionAssert.AreEqual(new[] { "P1.BeforeSetup(n1,Deserialize)" }, p1.Calls.GetRange(0, 1));
            CollectionAssert.AreEqual(new[] { "P2.BeforeSetup(n1,Deserialize)" }, p2.Calls.GetRange(0, 1));
            Assert.AreEqual("P1.AfterSetup(n1,Deserialize)", p1.Calls[1]);
            Assert.AreEqual("P2.AfterSetup(n1,Deserialize)", p2.Calls[1]);
        }

        [Test]
        public void NotifyAfterDeserialize_InvokesAllProcessorsOnce()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var p1 = new RecordingProcessor("P1");
            var p2 = new RecordingProcessor("P2");
            var runtime = new NodeRuntime(state, bus,
                new INodeLifecycleProcessor[] { p1, p2 });

            runtime.NotifyAfterDeserialize();

            CollectionAssert.AreEqual(new[] { "P1.AfterDeserialize" }, p1.Calls);
            CollectionAssert.AreEqual(new[] { "P2.AfterDeserialize" }, p2.Calls);
        }

        [Test]
        public void RegisterNode_ProcessorThrows_OtherProcessorsStillInvoked()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var throwing = new ThrowingProcessor();
            var recording = new RecordingProcessor("P2");
            var runtime = new NodeRuntime(state, bus,
                new INodeLifecycleProcessor[] { throwing, recording });

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex(".*"));

            runtime.RegisterNode(new StubNode("n1"), NodeInitMode.FreshSpawn);

            // 後続 processor が走り、node も登録される (defensive runtime 原則)
            Assert.IsTrue(state.Nodes.ContainsKey("n1"));
            Assert.AreEqual(2, recording.Calls.Count);
        }

        private sealed class ThrowingProcessor : INodeLifecycleProcessor
        {
            public void BeforeSetup(NodeBase node, NodeInitMode mode)
                => throw new System.InvalidOperationException("test");
            public void AfterSetup(NodeBase node, NodeInitMode mode) { }
            public void AfterDeserialize(GraphState state) { }
        }
    }
}
