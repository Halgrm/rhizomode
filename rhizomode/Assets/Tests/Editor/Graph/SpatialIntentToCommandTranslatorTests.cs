#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
using Rhizomode.Interaction.Contracts;
using Rhizomode.Interaction.GraphAdapter;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Tests
{
    public class SpatialIntentToCommandTranslatorTests
    {
        private sealed class StubNode : NodeBase
        {
            public StubNode(string id) : base(id, "Stub") { }
            public override void Setup(GraphState context) { }
        }

        private sealed class StubFactory : INodeFactory
        {
            public bool CanCreate(string typeName) => typeName == "Stub";
            public NodeBase? Create(string typeName, string nodeId) =>
                typeName == "Stub" ? new StubNode(nodeId) : null;
        }

        private static (GraphState state, GraphEventBus bus, GraphCommandDispatcher dispatcher,
                        SpatialIntentToCommandTranslator translator)
            CreateSystem(System.Func<string>? nodeIdProvider = null,
                         System.Func<string>? edgeIdProvider = null)
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var applier = new GraphMutationApplier(state, new StubFactory(), bus);
            var dispatcher = new GraphCommandDispatcher(applier);
            var translator = new SpatialIntentToCommandTranslator(
                dispatcher, nodeIdProvider, edgeIdProvider);
            return (state, bus, dispatcher, translator);
        }

        [Test]
        public void Translate_SpawnNodeIntent_ExecutesAddNodeWithInteractionOrigin()
        {
            var (state, _, dispatcher, translator) = CreateSystem(
                nodeIdProvider: () => "n-spawned");

            var ok = translator.Translate(new SpawnNodeIntent("Stub", new RzVector3(1, 2, 3)));

            Assert.IsTrue(ok);
            Assert.IsTrue(state.Nodes.ContainsKey("n-spawned"));
            Assert.AreEqual(1, dispatcher.AuditLog.CountByOrigin[CommandOrigin.Interaction]);
        }

        [Test]
        public void Translate_DeleteNodeIntent_ExecutesRemoveNode()
        {
            var (state, _, dispatcher, translator) = CreateSystem();
            translator.Translate(new SpawnNodeIntent("Stub", RzVector3.Zero));
            var spawnedId = state.Nodes.Keys.GetEnumerator();
            spawnedId.MoveNext();
            var id = spawnedId.Current;

            var ok = translator.Translate(new DeleteNodeIntent(id));

            Assert.IsTrue(ok);
            Assert.AreEqual(0, state.Nodes.Count);
            Assert.AreEqual(2, dispatcher.AuditLog.CountByOrigin[CommandOrigin.Interaction]);
        }

        [Test]
        public void Translate_MoveNodeIntent_ExecutesMoveNode()
        {
            var (state, _, _, translator) = CreateSystem(nodeIdProvider: () => "n1");
            translator.Translate(new SpawnNodeIntent("Stub", RzVector3.Zero));

            var ok = translator.Translate(new MoveNodeIntent("n1", new RzVector3(5, 6, 7)));

            Assert.IsTrue(ok);
            Assert.AreEqual(5f, state.Nodes["n1"].Position.x);
            Assert.AreEqual(6f, state.Nodes["n1"].Position.y);
            Assert.AreEqual(7f, state.Nodes["n1"].Position.z);
        }

        [Test]
        public void Translate_GrabIntent_DoesNotExecuteCommand()
        {
            var (_, _, dispatcher, translator) = CreateSystem();

            var ok = translator.Translate(new GrabIntent("n1"));

            Assert.IsFalse(ok);
            Assert.AreEqual(0, dispatcher.UndoStackCount);
        }

        [Test]
        public void Translate_ConnectPortsIntent_ExecutesConnect()
        {
            var (_, _, dispatcher, translator) = CreateSystem(edgeIdProvider: () => "e1");

            // ConnectPorts は実体 graph に node が無いと失敗するが、Dispatcher.Execute は呼ぶ。
            // 本テストでは AuditLog の Origin 計上を確認する。
            var ok = translator.Translate(new ConnectPortsIntent(
                "n1", "Out", "n2", "In"));

            Assert.IsTrue(ok);
            Assert.AreEqual(1, dispatcher.AuditLog.CountByOrigin[CommandOrigin.Interaction]);
        }
    }
}
