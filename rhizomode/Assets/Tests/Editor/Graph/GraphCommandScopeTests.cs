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

        [Test]
        public void Scope_AddSucceedsButConnectFails_RollsBackAddedNode()
        {
            // Codex re-review WARN #6 補強: AddNode が成功した後に ConnectPorts が失敗するケースで、
            // 既に登録された node が rollback されることを検証。
            // ConnectPorts は target node が存在しないと TryConnect が false を返して失敗する。
            var (state, dispatcher) = CreateSystem();

            using var scope = dispatcher.BeginScope();
            Assert.IsTrue(scope.TryExecute(new AddNodeCommand(
                CommandOrigin.Test, "src", "Stub", RzVector3.Zero)));
            Assert.AreEqual(1, state.Nodes.Count, "AddNode 直後に src が登録されている");

            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*Target node not found.*"));
            var ok = scope.TryExecute(new ConnectPortsCommand(
                CommandOrigin.Test, "e1",
                "src", "OutPort",
                "missing_target", "InPort"));

            Assert.IsFalse(ok, "ConnectPorts は missing target で失敗する");
            Assert.IsTrue(scope.HasFailed);
            Assert.AreEqual(0, state.Nodes.Count, "src が rollback で削除されている");
            Assert.AreEqual(0, state.Edges.Count);
            Assert.AreEqual(0, dispatcher.UndoStackCount, "rollback された scope は Undo に積まれない");
        }

        [Test]
        public void Scope_Nested_ThrowsInvalidOperationException()
        {
            // F-Vf-d.2 re-review fix (New WARN: Nested scope unguarded): outer scope が active 中の
            // BeginScope() 呼び出しは InvalidOperationException を投げる。
            var (_, dispatcher) = CreateSystem();

            using var outer = dispatcher.BeginScope();
            Assert.Throws<System.InvalidOperationException>(() => dispatcher.BeginScope());
        }

        [Test]
        public void Scope_AfterFirstFinalized_BeginScopeSucceeds()
        {
            // nested 検出は IsFinalized で行うため、Commit / Dispose 後の再 BeginScope は許可される。
            var (_, dispatcher) = CreateSystem();

            using (var first = dispatcher.BeginScope())
            {
                Assert.IsTrue(first.TryExecute(new AddNodeCommand(
                    CommandOrigin.Test, "n1", "Stub", RzVector3.Zero)));
                first.Commit();
            }

            // 第 2 scope は OK
            Assert.DoesNotThrow(() =>
            {
                using var second = dispatcher.BeginScope();
                second.Commit();
            });
        }

        [Test]
        public void Scope_FailedRollback_AuditAlsoRolledBack()
        {
            // F-Vf-d.3: scope 失敗時の audit transactional rollback を検証。
            var (_, dispatcher) = CreateSystem();
            var auditCountBefore = dispatcher.AuditLog.Trace.Count;

            using var scope = dispatcher.BeginScope();
            Assert.IsTrue(scope.TryExecute(new AddNodeCommand(
                CommandOrigin.Test, "n1", "Stub", RzVector3.Zero)));
            Assert.AreEqual(auditCountBefore + 1, dispatcher.AuditLog.Trace.Count,
                "成功時は audit に entry が追加される");

            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*Unknown typeName.*"));
            scope.TryExecute(new AddNodeCommand(CommandOrigin.Test, "n2", "Bogus", RzVector3.Zero));

            Assert.IsTrue(scope.HasFailed);
            Assert.AreEqual(auditCountBefore, dispatcher.AuditLog.Trace.Count,
                "失敗 scope は audit からも巻き戻される");
        }

        [Test]
        public void Scope_Committed_AuditContainsCompositeCommandMarker()
        {
            // F-Vf-d.3: 成功 scope の Commit で CompositeCommand marker が audit に記録される。
            var (_, dispatcher) = CreateSystem();
            var auditCountBefore = dispatcher.AuditLog.Trace.Count;

            using (var scope = dispatcher.BeginScope())
            {
                Assert.IsTrue(scope.TryExecute(new AddNodeCommand(
                    CommandOrigin.Test, "n1", "Stub", RzVector3.Zero)));
                Assert.IsTrue(scope.TryExecute(new AddNodeCommand(
                    CommandOrigin.Test, "n2", "Stub", RzVector3.Zero)));
                scope.Commit();
            }

            // sub-command 2 件 + CompositeCommand 1 件 = 3 件
            Assert.AreEqual(auditCountBefore + 3, dispatcher.AuditLog.Trace.Count);
            var lastEntry = dispatcher.AuditLog.Trace[dispatcher.AuditLog.Trace.Count - 1];
            Assert.AreEqual("CompositeCommand", lastEntry.Kind,
                "scope commit の最後に CompositeCommand entry が追加されている");
            Assert.AreEqual(CommandOrigin.Test, lastEntry.Origin);
        }

        [Test]
        public void Scope_DisposeWithoutCommit_AuditRolledBack()
        {
            // F-Vf-d.3: 暗黙 rollback (Dispose without Commit) も audit を巻き戻す。
            var (_, dispatcher) = CreateSystem();
            var auditCountBefore = dispatcher.AuditLog.Trace.Count;

            using (var scope = dispatcher.BeginScope())
            {
                Assert.IsTrue(scope.TryExecute(new AddNodeCommand(
                    CommandOrigin.Test, "n1", "Stub", RzVector3.Zero)));
                // Commit せず Dispose
            }

            Assert.AreEqual(auditCountBefore, dispatcher.AuditLog.Trace.Count,
                "Dispose without Commit でも audit が巻き戻される");
        }
    }
}
