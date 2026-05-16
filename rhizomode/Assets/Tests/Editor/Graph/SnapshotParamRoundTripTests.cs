#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
using Rhizomode.Graph.Serialization;
using Rhizomode.Graph.Snapshot;
using Rhizomode.SharedKernel;
using UnityEngine;

namespace Rhizomode.Graph.Tests
{
    /// <summary>
    /// P2 fix (2026-05-16): GraphMutationApplier.CaptureSnapshot が node の paramsJson を保存し、
    /// RestoreFromSnapshot が RestoreParamsFromJson 経由で完全 round-trip することを検証する。
    /// 旧コードは CaptureNodeParams が空辞書を返し、Undo/Redo で ConstFloat の値が落ちていた。
    /// </summary>
    public class SnapshotParamRoundTripTests
    {
        /// <summary>
        /// Stub node: ToNodeData / RestoreParamsFromJson を実装し、Value field を JSON で round-trip する。
        /// 実 ConstFloatNode を持ち込むと UI 系の依存が広がるため最小再現で検証。
        /// </summary>
        private sealed class StubParamNode : NodeBase
        {
            public float Value;

            public StubParamNode(string id) : base(id, "StubParam") { }

            public override void Setup(GraphState context) { }

            public override NodeData ToNodeData()
            {
                var data = base.ToNodeData();
                data.paramsJson = JsonUtility.ToJson(new Payload { value = Value });
                return data;
            }

            public override void RestoreParamsFromJson(string paramsJson)
            {
                if (string.IsNullOrEmpty(paramsJson)) return;
                var p = JsonUtility.FromJson<Payload>(paramsJson);
                Value = p.value;
            }

            [System.Serializable]
            private struct Payload { public float value; }
        }

        private sealed class StubFactory : INodeFactory
        {
            public bool CanCreate(string typeName) => typeName == "StubParam";
            public NodeBase? Create(string typeName, string nodeId) =>
                typeName == "StubParam" ? new StubParamNode(nodeId) : null;
        }

        private static (GraphState state, GraphMutationApplier applier, GraphCommandDispatcher dispatcher)
            CreateSystem()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var applier = new GraphMutationApplier(state, new StubFactory(), bus);
            return (state, applier, new GraphCommandDispatcher(applier));
        }

        [Test]
        public void CaptureAndRestoreSnapshot_PreservesParamsJson()
        {
            var (state, applier, dispatcher) = CreateSystem();
            dispatcher.Execute(new AddNodeCommand(CommandOrigin.Test, "n1", "StubParam", RzVector3.Zero));

            // ノードの値を設定 (実環境では SetNodeParamCommand 経由だがここは直接アクセス)
            var node = (StubParamNode)state.Nodes["n1"];
            node.Value = 0.42f;

            var snapshot = applier.CaptureSnapshot();

            // 別の state を用意して Snapshot から復元
            var (otherState, otherApplier, _) = CreateSystem();
            otherApplier.RestoreFromSnapshot(snapshot);

            Assert.IsTrue(otherState.Nodes.ContainsKey("n1"));
            var restored = (StubParamNode)otherState.Nodes["n1"];
            Assert.AreEqual(0.42f, restored.Value,
                "paramsJson 経由で Value が round-trip するべき (旧 CaptureNodeParams 空辞書 bug 修正)");
        }

        [Test]
        public void Undo_AfterParamChange_DoesNotDropValue()
        {
            var (state, applier, dispatcher) = CreateSystem();
            dispatcher.Execute(new AddNodeCommand(CommandOrigin.Test, "n1", "StubParam", RzVector3.Zero));

            var node = (StubParamNode)state.Nodes["n1"];
            node.Value = 1.0f;

            // dispatcher.Execute で別 mutation を起こす (param 変更を Undo entry に積む代用として
            // MoveNode を発行 → 直後 Undo すると pre-state の Value=1.0 に戻るはず)
            dispatcher.Execute(new MoveNodeCommand(CommandOrigin.Test, "n1", new RzVector3(5, 0, 0)));
            node.Value = 9.9f; // 後から値変更を加えても Snapshot 取得 → Undo で 1.0 に戻ること

            Assert.IsTrue(dispatcher.TryUndo());
            var afterUndo = (StubParamNode)state.Nodes["n1"];
            Assert.AreEqual(1.0f, afterUndo.Value,
                "Undo 後は MoveNode 直前の Value=1.0 に戻るべき (paramsJson Snapshot が機能していること)");
        }

        [Test]
        public void NodeSnapshot_DefaultParamsJson_IsEmpty()
        {
            // backward compat: 既存テストの (id, type, pos, paramValues) 4-arg 形式は ParamsJson="" にフォールバック
            var snap = new NodeSnapshot("n1", "X", new RzVector3(0, 0, 0),
                new System.Collections.Generic.Dictionary<string, ParamValue>());
            Assert.AreEqual(string.Empty, snap.ParamsJson);
        }
    }
}
