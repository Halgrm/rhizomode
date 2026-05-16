#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Tests
{
    /// <summary>
    /// GraphState.TryConnect の循環検出 (P1 fix, 2026-05-16)。
    /// R3 push 型では cycle が再入 + スタック増大 + 映像停止につながるため、必ず接続段階で弾く。
    /// </summary>
    public class GraphStateCycleDetectionTests
    {
        private sealed class StubNode : NodeBase
        {
            public StubNode(string id) : base(id, "Stub")
            {
                RegisterOutput<float>("Out", ParamType.Float);
                RegisterInput<float>("In", ParamType.Float);
            }
            public override void Setup(GraphState context) { }
        }

        private sealed class StubFactory : INodeFactory
        {
            public bool CanCreate(string typeName) => typeName == "Stub";
            public NodeBase? Create(string typeName, string nodeId) =>
                typeName == "Stub" ? new StubNode(nodeId) : null;
        }

        private static (GraphState state, GraphCommandDispatcher dispatcher) CreateSystem()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var applier = new GraphMutationApplier(state, new StubFactory(), bus);
            return (state, new GraphCommandDispatcher(applier));
        }

        private static void AddNode(GraphCommandDispatcher dispatcher, string id)
        {
            dispatcher.Execute(new AddNodeCommand(CommandOrigin.Test, id, "Stub", RzVector3.Zero));
        }

        [Test]
        public void TryConnect_DirectCycle_TwoNodes_Refused()
        {
            var (state, dispatcher) = CreateSystem();
            AddNode(dispatcher, "n1");
            AddNode(dispatcher, "n2");

            // n1 → n2 は接続可
            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "e1", "n1", "Out", "n2", "In"));
            Assert.AreEqual(1, state.Edges.Count);

            // n2 → n1 は cycle (n1→n2→n1) を作るため拒否
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*Cycle would be created.*"));
            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "e2", "n2", "Out", "n1", "In"));
            Assert.AreEqual(1, state.Edges.Count, "cycle を作る edge は追加されない");
        }

        [Test]
        public void TryConnect_TriangleCycle_ThreeNodes_Refused()
        {
            var (state, dispatcher) = CreateSystem();
            AddNode(dispatcher, "a");
            AddNode(dispatcher, "b");
            AddNode(dispatcher, "c");

            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "e1", "a", "Out", "b", "In"));
            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "e2", "b", "Out", "c", "In"));
            Assert.AreEqual(2, state.Edges.Count);

            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*Cycle would be created.*"));
            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "e3", "c", "Out", "a", "In"));
            Assert.AreEqual(2, state.Edges.Count, "三角 cycle は拒否される");
        }

        [Test]
        public void TryConnect_DAG_Tree_NoCycle_Allowed()
        {
            var (state, dispatcher) = CreateSystem();
            AddNode(dispatcher, "root");
            AddNode(dispatcher, "l");
            AddNode(dispatcher, "r");

            // root → l, root → r は DAG なので両方接続できる
            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "e1", "root", "Out", "l", "In"));
            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "e2", "root", "Out", "r", "In"));
            Assert.AreEqual(2, state.Edges.Count);
        }

        [Test]
        public void TryConnect_DiamondMerge_NotACycle_Allowed()
        {
            var (state, dispatcher) = CreateSystem();
            AddNode(dispatcher, "a");
            AddNode(dispatcher, "b");
            AddNode(dispatcher, "c");
            AddNode(dispatcher, "d");

            // a→b, a→c, b→d, c→d (diamond) は cycle ではない
            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "e1", "a", "Out", "b", "In"));
            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "e2", "a", "Out", "c", "In"));
            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "e3", "b", "Out", "d", "In"));
            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "e4", "c", "Out", "d", "In"));
            Assert.AreEqual(4, state.Edges.Count, "diamond は cycle ではないので全 edge OK");
        }
    }
}
