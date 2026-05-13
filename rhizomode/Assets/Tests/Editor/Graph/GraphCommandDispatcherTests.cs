#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using R3;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Tests
{
    public class GraphCommandDispatcherTests
    {
        private sealed class StubNode : NodeBase
        {
            public StubNode(string id) : base(id, "Stub") { }
            public override void Setup(GraphState context) { /* no-op */ }
        }

        private sealed class StubFactory : INodeFactory
        {
            public bool CanCreate(string typeName) => typeName == "Stub";
            public NodeBase? Create(string typeName, string nodeId) =>
                typeName == "Stub" ? new StubNode(nodeId) : null;
        }

        private static (GraphState state, GraphEventBus bus, GraphCommandDispatcher dispatcher)
            CreateSystem()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var applier = new GraphMutationApplier(state, new StubFactory(), bus);
            return (state, bus, new GraphCommandDispatcher(applier));
        }

        [Test]
        public void Execute_AddNode_RegistersInState_EmitsEvent()
        {
            var (state, bus, dispatcher) = CreateSystem();
            string? emitted = null;
            using var sub = bus.OnNodeAdded.Subscribe(id => emitted = id);

            dispatcher.Execute(new AddNodeCommand(
                CommandOrigin.Test, "n1", "Stub", new RzVector3(1, 2, 3)));

            Assert.IsTrue(state.Nodes.ContainsKey("n1"));
            Assert.AreEqual("n1", emitted);
            Assert.AreEqual(1, dispatcher.UndoStackCount);
        }

        [Test]
        public void Execute_UnknownTypeName_NoOp()
        {
            var (state, _, dispatcher) = CreateSystem();
            dispatcher.Execute(new AddNodeCommand(
                CommandOrigin.Test, "n1", "Bogus", RzVector3.Zero));

            Assert.AreEqual(0, state.Nodes.Count);
        }

        [Test]
        public void Undo_AfterAddNode_RemovesIt()
        {
            var (state, _, dispatcher) = CreateSystem();
            dispatcher.Execute(new AddNodeCommand(
                CommandOrigin.Test, "n1", "Stub", RzVector3.Zero));
            Assert.AreEqual(1, state.Nodes.Count);

            var didUndo = dispatcher.TryUndo();

            Assert.IsTrue(didUndo);
            Assert.AreEqual(0, state.Nodes.Count);
            Assert.AreEqual(1, dispatcher.RedoStackCount);
        }

        [Test]
        public void Redo_AfterUndo_RestoresAddedNode()
        {
            var (state, _, dispatcher) = CreateSystem();
            dispatcher.Execute(new AddNodeCommand(
                CommandOrigin.Test, "n1", "Stub", RzVector3.Zero));
            dispatcher.TryUndo();

            var didRedo = dispatcher.TryRedo();

            Assert.IsTrue(didRedo);
            Assert.AreEqual(1, state.Nodes.Count);
            Assert.IsTrue(state.Nodes.ContainsKey("n1"));
        }

        [Test]
        public void Execute_AfterUndo_ClearsRedoStack()
        {
            var (state, _, dispatcher) = CreateSystem();
            dispatcher.Execute(new AddNodeCommand(
                CommandOrigin.Test, "n1", "Stub", RzVector3.Zero));
            dispatcher.TryUndo();
            Assert.AreEqual(1, dispatcher.RedoStackCount);

            dispatcher.Execute(new AddNodeCommand(
                CommandOrigin.Test, "n2", "Stub", RzVector3.Zero));

            Assert.AreEqual(0, dispatcher.RedoStackCount);
        }

        [Test]
        public void TryUndo_EmptyHistory_ReturnsFalse()
        {
            var (_, _, dispatcher) = CreateSystem();
            Assert.IsFalse(dispatcher.TryUndo());
        }

        [Test]
        public void HistorySize_Limit_TrimsOldest()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var applier = new GraphMutationApplier(state, new StubFactory(), bus);
            var dispatcher = new GraphCommandDispatcher(applier, maxHistorySize: 2);

            dispatcher.Execute(new AddNodeCommand(CommandOrigin.Test, "n1", "Stub", RzVector3.Zero));
            dispatcher.Execute(new AddNodeCommand(CommandOrigin.Test, "n2", "Stub", RzVector3.Zero));
            dispatcher.Execute(new AddNodeCommand(CommandOrigin.Test, "n3", "Stub", RzVector3.Zero));

            Assert.AreEqual(2, dispatcher.UndoStackCount);
        }

        [Test]
        public void MainThreadQueue_Tick_DispatchesPendingCommands()
        {
            var (state, _, dispatcher) = CreateSystem();
            var queue = new MainThreadGraphCommandQueue(dispatcher);

            queue.Enqueue(new AddNodeCommand(CommandOrigin.Test, "n1", "Stub", RzVector3.Zero));
            queue.Enqueue(new AddNodeCommand(CommandOrigin.Test, "n2", "Stub", RzVector3.Zero));
            Assert.AreEqual(2, queue.PendingCount);

            queue.Tick();

            Assert.AreEqual(0, queue.PendingCount);
            Assert.AreEqual(2, state.Nodes.Count);
        }
    }
}
