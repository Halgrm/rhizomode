#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using R3;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Graph.Serialization;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Tests
{
    public class HydrationPlanExecutorTests
    {
        private sealed class StubNode : NodeBase
        {
            public StubNode(string id) : base(id, "Stub")
            {
                RegisterInput<float>("In", ParamType.Float);
                RegisterOutput<float>("Out", ParamType.Float);
            }

            public override void Setup(GraphState context) { }
        }

        private sealed class StubFactory : INodeFactory
        {
            public bool CanCreate(string typeName) => typeName == "Stub";
            public NodeBase? Create(string typeName, string nodeId) =>
                typeName == "Stub" ? new StubNode(nodeId) : null;
        }

        private static (GraphState state, GraphEventBus bus, NodeRuntime runtime, HydrationPlanExecutor exec)
            CreateSystem()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var runtime = new NodeRuntime(state, bus);
            var exec = new HydrationPlanExecutor(runtime);
            return (state, bus, runtime, exec);
        }

        [Test]
        public void Execute_EmptyPlan_NoChange()
        {
            var (state, bus, _, exec) = CreateSystem();
            var fired = false;
            using var sub = bus.OnGraphChanged.Subscribe(_ => fired = true);

            exec.Execute(new HydrationPlan(
                new List<NodeHydrationEntry>(),
                new List<EdgeHydrationEntry>()),
                new StubFactory());

            Assert.AreEqual(0, state.Nodes.Count);
            Assert.IsFalse(fired, "Empty scope should not emit OnGraphChanged");
        }

        [Test]
        public void Execute_SingleNode_RegistersAndEmitsEvents()
        {
            var (state, bus, _, exec) = CreateSystem();
            var nodeAddedIds = new List<string>();
            GraphChangeSet? captured = null;
            using var sub1 = bus.OnNodeAdded.Subscribe(id => nodeAddedIds.Add(id));
            using var sub2 = bus.OnGraphChanged.Subscribe(cs => captured = cs);

            var plan = new HydrationPlan(
                new List<NodeHydrationEntry>
                {
                    new("n1", "Stub", new RzVector3(1, 2, 3),
                        new Dictionary<string, ParamValue>())
                },
                new List<EdgeHydrationEntry>());

            exec.Execute(plan, new StubFactory());

            Assert.AreEqual(1, state.Nodes.Count);
            Assert.IsTrue(state.Nodes.ContainsKey("n1"));
            Assert.AreEqual(new UnityEngine.Vector3(1, 2, 3), state.Nodes["n1"].Position);
            CollectionAssert.AreEqual(new[] { "n1" }, nodeAddedIds);
            Assert.IsNotNull(captured);
            CollectionAssert.AreEqual(new[] { "n1" }, captured!.AddedNodeIds);
        }

        [Test]
        public void Execute_TwoNodesPlusEdge_ConnectsThemAndEmitsAll()
        {
            var (state, bus, _, exec) = CreateSystem();
            var edgeAddedIds = new List<string>();
            GraphChangeSet? captured = null;
            using var sub1 = bus.OnEdgeAdded.Subscribe(id => edgeAddedIds.Add(id));
            using var sub2 = bus.OnGraphChanged.Subscribe(cs => captured = cs);

            var plan = new HydrationPlan(
                new List<NodeHydrationEntry>
                {
                    new("a", "Stub", RzVector3.Zero, new Dictionary<string, ParamValue>()),
                    new("b", "Stub", RzVector3.Zero, new Dictionary<string, ParamValue>())
                },
                new List<EdgeHydrationEntry>
                {
                    new("e1", "a", "Out", "b", "In")
                });

            exec.Execute(plan, new StubFactory());

            Assert.AreEqual(2, state.Nodes.Count);
            Assert.AreEqual(1, state.Edges.Count);
            CollectionAssert.AreEqual(new[] { "e1" }, edgeAddedIds);
            Assert.IsNotNull(captured);
            CollectionAssert.AreEqual(new[] { "a", "b" }, captured!.AddedNodeIds);
            CollectionAssert.AreEqual(new[] { "e1" }, captured.AddedEdgeIds);
        }

        [Test]
        public void Execute_UnknownTypeName_SkipsNode()
        {
            var (state, _, _, exec) = CreateSystem();

            var plan = new HydrationPlan(
                new List<NodeHydrationEntry>
                {
                    new("n1", "Bogus", RzVector3.Zero, new Dictionary<string, ParamValue>())
                },
                new List<EdgeHydrationEntry>());

            exec.Execute(plan, new StubFactory());

            Assert.AreEqual(0, state.Nodes.Count);
        }

        [Test]
        public void Execute_EdgeWithMissingEndpoint_LogsWarningButContinues()
        {
            var (state, _, _, exec) = CreateSystem();

            var plan = new HydrationPlan(
                new List<NodeHydrationEntry>
                {
                    new("a", "Stub", RzVector3.Zero, new Dictionary<string, ParamValue>())
                },
                new List<EdgeHydrationEntry>
                {
                    new("e1", "a", "Out", "missing", "In")
                });

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*"));

            exec.Execute(plan, new StubFactory());

            Assert.AreEqual(1, state.Nodes.Count);
            Assert.AreEqual(0, state.Edges.Count);
        }
    }
}
