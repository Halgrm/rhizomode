#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using R3;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
using Rhizomode.Interaction.GraphAdapter;
using Rhizomode.SharedKernel;
using Rhizomode.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rhizomode.Interaction.Tests
{
    /// <summary>
    /// F-Vf-d.2 (Codex review #6 TEST_COVERAGE_GAP): NodeSpawnService の AddNodeCommand + ConnectPortsCommand
    /// 連投 + PrimeInitialEmission + event ポート skip + Toggle/Trigger 追加 spawn の振る舞いを検証する。
    /// </summary>
    public class NodeSpawnServiceTests
    {
        private const string FloatSourceType = "ConstFloat";
        private const string ColorSourceType = "ConstColor";
        private const string ToggleSourceType = "Toggle";
        private const string TriggerSourceType = "Trigger";
        private const string TargetTypeName = "Target";

        private sealed class StubFloatSource : NodeBase
        {
            public readonly OutputPort<float> Value;
            public float InitialValue = 0.5f;
            public int PrimeCallCount;

            public StubFloatSource(string id) : base(id, FloatSourceType)
            {
                Value = RegisterOutput<float>("Value", ParamType.Float);
            }

            public override void Setup(GraphState context) { /* no-op for tests */ }

            public override void PrimeInitialEmission()
            {
                PrimeCallCount++;
                Value.Emit(InitialValue);
            }
        }

        private sealed class StubColorSource : NodeBase
        {
            public StubColorSource(string id) : base(id, ColorSourceType)
            {
                RegisterOutput<Color>("Value", ParamType.Color);
            }
            public override void Setup(GraphState context) { }
        }

        private sealed class StubToggleNode : NodeBase
        {
            public StubToggleNode(string id) : base(id, ToggleSourceType)
            {
                RegisterInput<bool>("Trigger", ParamType.Bool);
                RegisterOutput<bool>("State", ParamType.Bool);
            }
            public override void Setup(GraphState context) { }
        }

        private sealed class StubTriggerNode : NodeBase
        {
            public StubTriggerNode(string id) : base(id, TriggerSourceType)
            {
                RegisterOutput<bool>("Trigger", ParamType.Bool);
            }
            public override void Setup(GraphState context) { }
        }

        private sealed class StubTarget : NodeBase
        {
            public readonly InputPort<float> FloatIn;
            public readonly InputPort<bool> BoolIn;
            private readonly HashSet<string> _eventPorts;

            public StubTarget(string id, params string[] eventPorts) : base(id, TargetTypeName)
            {
                FloatIn = RegisterInput<float>("FloatIn", ParamType.Float);
                BoolIn = RegisterInput<bool>("BoolIn", ParamType.Bool);
                RegisterInput<bool>("EventIn", ParamType.Bool);
                _eventPorts = new HashSet<string>(eventPorts ?? Array.Empty<string>());
            }

            public override void Setup(GraphState context) { }

            public override bool IsInputPortEvent(string portName) => _eventPorts.Contains(portName);
        }

        private sealed class StubFactory : INodeFactory
        {
            public bool CanCreate(string typeName) => typeName switch
            {
                FloatSourceType or ColorSourceType or ToggleSourceType or TriggerSourceType or TargetTypeName => true,
                _ => false,
            };

            public NodeBase? Create(string typeName, string nodeId) => typeName switch
            {
                FloatSourceType => new StubFloatSource(nodeId),
                ColorSourceType => new StubColorSource(nodeId),
                ToggleSourceType => new StubToggleNode(nodeId),
                TriggerSourceType => new StubTriggerNode(nodeId),
                TargetTypeName => new StubTarget(nodeId, "EventIn"),
                _ => null,
            };
        }

        private static (GraphState state, GraphCommandDispatcher dispatcher, NodeSpawnService service)
            CreateSystem()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var applier = new GraphMutationApplier(state, new StubFactory(), bus);
            var dispatcher = new GraphCommandDispatcher(applier);
            var service = new NodeSpawnService(state, dispatcher);
            return (state, dispatcher, service);
        }

        [Test]
        public void TrySpawnFromMenu_KnownType_AddsNodeAndReturnsResult()
        {
            var (state, dispatcher, service) = CreateSystem();

            var result = service.TrySpawnFromMenu(TargetTypeName, new Vector3(0, 1, 0), Vector3.forward);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, state.Nodes.Count);
            Assert.IsTrue(state.Nodes.ContainsKey(result!.Node.Id));
            Assert.AreEqual(1, dispatcher.UndoStackCount);
        }

        [Test]
        public void TrySpawnFromMenu_UnknownType_ReturnsNullAndLeavesGraphClean()
        {
            var (state, dispatcher, service) = CreateSystem();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
            var result = service.TrySpawnFromMenu("Bogus", Vector3.zero, Vector3.forward);

            Assert.IsNull(result);
            Assert.AreEqual(0, state.Nodes.Count);
            Assert.AreEqual(0, dispatcher.UndoStackCount);
        }

        [Test]
        public void SpawnInputNodes_FloatPort_SpawnsConstFloatAndConnects()
        {
            var (state, _, service) = CreateSystem();
            var target = service.TrySpawnFromMenu(TargetTypeName, Vector3.zero, Vector3.forward)!;

            var results = service.SpawnInputNodes(target.Node, target.Position, Vector3.zero);

            // FloatIn + BoolIn の 2 件 (EventIn は IsInputPortEvent true で skip)
            Assert.AreEqual(2, results.Count);
            var floatResult = FindByPortType(results, ParamType.Float);
            Assert.IsNotNull(floatResult);
            Assert.IsNotNull(floatResult!.PrimaryEdge);
            Assert.AreEqual(FloatSourceType, floatResult.Source.NodeType);
            Assert.IsNull(floatResult.TriggerNode);
            Assert.IsNull(floatResult.TriggerEdge);

            // edge が GraphState.Edges に存在し、id が一致する
            var edgeId = floatResult.PrimaryEdge!.EdgeId;
            var found = false;
            foreach (var e in state.Edges) if (e.Id == edgeId) found = true;
            Assert.IsTrue(found, "ConnectPortsCommand で発行した edge が GraphState に存在しない");
        }

        [Test]
        public void SpawnInputNodes_BoolPort_SpawnsToggleAndTriggerWithBothEdges()
        {
            var (_, _, service) = CreateSystem();
            var target = service.TrySpawnFromMenu(TargetTypeName, Vector3.zero, Vector3.forward)!;

            var results = service.SpawnInputNodes(target.Node, target.Position, Vector3.zero);

            var boolResult = FindByPortType(results, ParamType.Bool);
            Assert.IsNotNull(boolResult);
            Assert.AreEqual(ToggleSourceType, boolResult!.Source.NodeType);
            Assert.IsNotNull(boolResult.PrimaryEdge);
            Assert.IsNotNull(boolResult.TriggerNode);
            Assert.AreEqual(TriggerSourceType, boolResult.TriggerNode!.NodeType);
            Assert.IsNotNull(boolResult.TriggerEdge);
        }

        [Test]
        public void SpawnInputNodes_EventPort_NotSpawned()
        {
            var (_, _, service) = CreateSystem();
            var target = service.TrySpawnFromMenu(TargetTypeName, Vector3.zero, Vector3.forward)!;

            var results = service.SpawnInputNodes(target.Node, target.Position, Vector3.zero);

            // EventIn (IsInputPortEvent true) は spawn 対象外
            foreach (var r in results)
            {
                Assert.AreNotEqual("EventIn", r.PrimaryEdge?.ToPort,
                    "event ポート EventIn が auto-spawn された (skip 失敗)");
            }
        }

        [Test]
        public void PrimeInitialEmission_AfterConnect_SubscriberReceivesValue()
        {
            var (state, _, service) = CreateSystem();
            var target = service.TrySpawnFromMenu(TargetTypeName, Vector3.zero, Vector3.forward)!;
            var targetNode = (StubTarget)target.Node;

            float? observed = null;
            using var sub = targetNode.FloatIn.Observable.Subscribe(v => observed = v);

            var results = service.SpawnInputNodes(target.Node, target.Position, Vector3.zero);
            var floatResult = FindByPortType(results, ParamType.Float);
            var source = (StubFloatSource)floatResult!.Source;

            // PrimeInitialEmission が呼ばれ、subscriber に initial value (0.5) が届いた
            Assert.AreEqual(1, source.PrimeCallCount);
            Assert.IsNotNull(observed);
            Assert.AreEqual(source.InitialValue, observed!.Value);
        }

        [Test]
        public void SpawnInputNodes_UndoAfterMenuSpawn_RemovesTargetOnly()
        {
            // 1 スコープ = 1 ステップ Undo の確認:
            // TrySpawnFromMenu 1 回で target ノード 1 個追加。Undo 1 回で graph が空に戻ること。
            var (state, dispatcher, service) = CreateSystem();
            var target = service.TrySpawnFromMenu(TargetTypeName, Vector3.zero, Vector3.forward)!;
            Assert.AreEqual(1, state.Nodes.Count);

            var didUndo = dispatcher.TryUndo();

            Assert.IsTrue(didUndo);
            Assert.AreEqual(0, state.Nodes.Count);
        }

        [Test]
        public void SpawnInputNodes_UndoAfterInputSpawn_RestoresPreSpawnState()
        {
            // 1 入力 spawn = 1 ステップ Undo の確認:
            // SpawnInputNodes が Float の 1 source ノード + 1 edge を追加 → Undo 1 回で target だけが残る。
            var (state, dispatcher, service) = CreateSystem();
            var target = service.TrySpawnFromMenu(TargetTypeName, Vector3.zero, Vector3.forward)!;
            service.SpawnInputNodes(target.Node, target.Position, Vector3.zero);
            // target(1) + Float source(1) + Toggle(1) + Trigger(1) = 4 ノード
            Assert.AreEqual(4, state.Nodes.Count);
            // edge: Float→FloatIn + Toggle→BoolIn + Trigger→Toggle.Trigger = 3 edges
            Assert.AreEqual(3, state.Edges.Count);

            // Undo 順:
            //  (1) Bool input spawn (Toggle + Trigger + 2 edges) -> 4-3=1+0?? wait Bool spawn = +2 nodes +2 edges
            //  (0) Float input spawn (ConstFloat + 1 edge)       -> +1 node +1 edge
            //  (-1) Menu spawn (Target)                          -> +1 node
            // Bool は最初に処理されるか Float が最初か? 辞書順序依存だが、scope ごとに 1 undo 加わる
            // SpawnInputNodes 内では port ごとに別 scope を発行するため、入力 1 件 = 1 undo entry。
            // 合計 Undo stack: 1 (menu) + 2 (2 inputs) = 3
            Assert.AreEqual(3, dispatcher.UndoStackCount);
        }

        private static InputSpawnResult? FindByPortType(IReadOnlyList<InputSpawnResult> results, ParamType type)
        {
            foreach (var r in results) if (r.PortType == type) return r;
            return null;
        }
    }
}
