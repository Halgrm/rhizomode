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
    /// F-Vf-d.2 (Codex review #3 NON_ATOMIC_MULTI_DISPATCH): <see cref="GraphCommandScope"/> の atomic 適用 +
    /// 失敗時 rollback + Commit 時の単一 Undo entry 追加を検証する。
    /// </summary>
    public class GraphCommandScopeTests
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

        private static (GraphState state, GraphCommandDispatcher dispatcher) CreateSystem()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var applier = new GraphMutationApplier(state, new StubFactory(), bus);
            return (state, new GraphCommandDispatcher(applier));
        }

        [Test]
        public void Scope_AllExecuteSucceed_Commit_PushesSingleUndoEntry()
        {
            var (state, dispatcher) = CreateSystem();

            using (var scope = dispatcher.BeginScope())
            {
                Assert.IsTrue(scope.TryExecute(new AddNodeCommand(
                    CommandOrigin.Test, "n1", "Stub", RzVector3.Zero)));
                Assert.IsTrue(scope.TryExecute(new AddNodeCommand(
                    CommandOrigin.Test, "n2", "Stub", RzVector3.Zero)));
                scope.Commit();
            }

            Assert.AreEqual(2, state.Nodes.Count);
            Assert.AreEqual(1, dispatcher.UndoStackCount, "scope 全体で 1 Undo entry のみ");
        }

        [Test]
        public void Scope_OneFailure_RollsBackAllPriorSubCommands()
        {
            var (state, dispatcher) = CreateSystem();

            using var scope = dispatcher.BeginScope();
            Assert.IsTrue(scope.TryExecute(new AddNodeCommand(
                CommandOrigin.Test, "n1", "Stub", RzVector3.Zero)));
            Assert.AreEqual(1, state.Nodes.Count, "中間状態: n1 は登録された");

            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*Unknown typeName.*"));
            var ok = scope.TryExecute(new AddNodeCommand(
                CommandOrigin.Test, "n2", "Bogus", RzVector3.Zero));

            Assert.IsFalse(ok);
            Assert.IsTrue(scope.HasFailed);
            Assert.AreEqual(0, state.Nodes.Count, "失敗時に n1 を含む全変更が rollback されること");
            Assert.AreEqual(0, dispatcher.UndoStackCount, "rollback された scope は Undo に積まれない");
        }

        [Test]
        public void Scope_DisposedWithoutCommit_RollsBackChanges()
        {
            var (state, dispatcher) = CreateSystem();

            using (var scope = dispatcher.BeginScope())
            {
                Assert.IsTrue(scope.TryExecute(new AddNodeCommand(
                    CommandOrigin.Test, "n1", "Stub", RzVector3.Zero)));
                // Commit 呼ばずに Dispose
            }

            Assert.AreEqual(0, state.Nodes.Count, "Commit せず Dispose で entry snapshot に rollback");
            Assert.AreEqual(0, dispatcher.UndoStackCount);
        }

        [Test]
        public void Scope_CommittedThenUndo_RestoresEntrySnapshot()
        {
            var (state, dispatcher) = CreateSystem();

            using (var scope = dispatcher.BeginScope())
            {
                Assert.IsTrue(scope.TryExecute(new AddNodeCommand(
                    CommandOrigin.Test, "n1", "Stub", RzVector3.Zero)));
                Assert.IsTrue(scope.TryExecute(new AddNodeCommand(
                    CommandOrigin.Test, "n2", "Stub", RzVector3.Zero)));
                scope.Commit();
            }

            Assert.AreEqual(2, state.Nodes.Count);
            Assert.IsTrue(dispatcher.TryUndo(), "scope の Undo");
            Assert.AreEqual(0, state.Nodes.Count, "scope 内 sub-command 全件が Undo で消える");
        }

        [Test]
        public void Scope_AfterFailure_FurtherTryExecuteReturnsFalse()
        {
            var (_, dispatcher) = CreateSystem();

            using var scope = dispatcher.BeginScope();
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*Unknown typeName.*"));
            scope.TryExecute(new AddNodeCommand(CommandOrigin.Test, "n1", "Bogus", RzVector3.Zero));

            // 失敗後は後続の TryExecute も拒否される
            var ok = scope.TryExecute(new AddNodeCommand(CommandOrigin.Test, "n2", "Stub", RzVector3.Zero));

            Assert.IsFalse(ok);
            Assert.AreEqual(0, scope.CommittedCount);
        }
    }
}
