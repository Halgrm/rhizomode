#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
using Rhizomode.SharedKernel;

namespace Rhizomode.Performance.Tests
{
    /// <summary>
    /// N6 (2026-05-16): 60 nodes / 200 edges + cycle detection の throughput と
    /// allocation を CI で観測する。EditMode で sanity check し、本格負荷は別途 Profile ビルドで実測する。
    /// </summary>
    /// <remarks>
    /// 目的は「劣化を CI で検出する」こと。具体閾値は launch 後の実測で校正する想定で
    /// 緩めに設定 (FAIL 閾値はあくまで bug detection の最低ライン)。
    /// </remarks>
    public class GraphScaleTests
    {
        private const int TargetNodeCount = 60;
        private const int TargetEdgeCount = 200;

        // CI 環境差を吸収するため緩めに設定 (実測値の 5-10 倍を上限とする)
        private const long RegisterBudgetMs = 500;
        private const long ConnectBudgetMs = 1500;
        private const long SnapshotBudgetMs = 200;

        private sealed class StubNode : NodeBase
        {
            public StubNode(string id) : base(id, "Stub")
            {
                // 各ノードに float Out + Float In × 4 を持たせる (60 nodes × 200 edges 想定)
                RegisterOutput<float>("Out", ParamType.Float);
                RegisterInput<float>("In0", ParamType.Float);
                RegisterInput<float>("In1", ParamType.Float);
                RegisterInput<float>("In2", ParamType.Float);
                RegisterInput<float>("In3", ParamType.Float);
            }
            public override void Setup(GraphState context) { }
        }

        private sealed class StubFactory : INodeFactory
        {
            public bool CanCreate(string typeName) => typeName == "Stub";
            public NodeBase? Create(string typeName, string nodeId) =>
                typeName == "Stub" ? new StubNode(nodeId) : null;
        }

        private static (GraphState state, GraphCommandDispatcher dispatcher, GraphMutationApplier applier)
            CreateSystem()
        {
            var state = new GraphState();
            var bus = new GraphEventBus();
            var applier = new GraphMutationApplier(state, new StubFactory(), bus);
            return (state, new GraphCommandDispatcher(applier, maxHistorySize: 4), applier);
        }

        [Test]
        public void Register60Nodes_WithinBudget()
        {
            var (state, dispatcher, _) = CreateSystem();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < TargetNodeCount; i++)
            {
                dispatcher.Execute(new AddNodeCommand(
                    CommandOrigin.Test, $"n{i}", "Stub", RzVector3.Zero));
            }
            sw.Stop();

            Assert.AreEqual(TargetNodeCount, state.Nodes.Count);
            Assert.Less(sw.ElapsedMilliseconds, RegisterBudgetMs,
                $"60 ノード登録に {sw.ElapsedMilliseconds}ms — budget {RegisterBudgetMs}ms 超過");
        }

        [Test]
        public void Connect200Edges_NoCycle_WithinBudget()
        {
            var (state, dispatcher, _) = CreateSystem();

            // チェイン: n0→n1→...→n59 で線形 DAG を作る (59 edges, no cycle)
            for (int i = 0; i < TargetNodeCount; i++)
            {
                dispatcher.Execute(new AddNodeCommand(
                    CommandOrigin.Test, $"n{i}", "Stub", RzVector3.Zero));
            }

            var sw = Stopwatch.StartNew();
            int connected = 0;
            int edgeId = 0;
            // チェイン 59 edges
            for (int i = 0; i < TargetNodeCount - 1 && connected < TargetEdgeCount; i++)
            {
                dispatcher.Execute(new ConnectPortsCommand(
                    CommandOrigin.Test, $"e{edgeId++}", $"n{i}", "Out", $"n{i + 1}", "In0"));
                connected++;
            }
            // 追加 fan-out: n0 → n2..n59 / n1 → n3..n59 (DAG を保つように下流のみ)
            for (int src = 0; src < TargetNodeCount && connected < TargetEdgeCount; src++)
            {
                for (int dst = src + 2; dst < TargetNodeCount && connected < TargetEdgeCount; dst++)
                {
                    // ポートを 4 通りで分散させる
                    var inPort = $"In{1 + (connected % 3)}";
                    dispatcher.Execute(new ConnectPortsCommand(
                        CommandOrigin.Test, $"e{edgeId++}", $"n{src}", "Out", $"n{dst}", inPort));
                    connected++;
                }
            }
            sw.Stop();

            Assert.GreaterOrEqual(state.Edges.Count, TargetEdgeCount,
                $"想定の {TargetEdgeCount} エッジに届かなかった: {state.Edges.Count}");
            Assert.Less(sw.ElapsedMilliseconds, ConnectBudgetMs,
                $"{state.Edges.Count} edges 接続 (cycle check 込み) に {sw.ElapsedMilliseconds}ms — budget {ConnectBudgetMs}ms 超過");
        }

        [Test]
        public void CaptureSnapshot_With60Nodes_WithinBudget()
        {
            var (state, dispatcher, applier) = CreateSystem();
            for (int i = 0; i < TargetNodeCount; i++)
            {
                dispatcher.Execute(new AddNodeCommand(
                    CommandOrigin.Test, $"n{i}", "Stub", new RzVector3(i, 0, 0)));
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                var _ = applier.CaptureSnapshot();
            }
            sw.Stop();

            Assert.Less(sw.ElapsedMilliseconds, SnapshotBudgetMs,
                $"60 nodes × 10 回 Snapshot 取得に {sw.ElapsedMilliseconds}ms — budget {SnapshotBudgetMs}ms 超過");
        }

        [Test]
        public void CycleDetection_OnLinearChain_DoesNotExplode()
        {
            // 線形チェイン 60 nodes + n59→n0 cycle 拒否の DFS が指数爆発しないことを確認
            var (state, dispatcher, _) = CreateSystem();
            for (int i = 0; i < TargetNodeCount; i++)
            {
                dispatcher.Execute(new AddNodeCommand(
                    CommandOrigin.Test, $"n{i}", "Stub", RzVector3.Zero));
            }
            for (int i = 0; i < TargetNodeCount - 1; i++)
            {
                dispatcher.Execute(new ConnectPortsCommand(
                    CommandOrigin.Test, $"e{i}", $"n{i}", "Out", $"n{i + 1}", "In0"));
            }

            var sw = Stopwatch.StartNew();
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*Cycle would be created.*"));
            dispatcher.Execute(new ConnectPortsCommand(
                CommandOrigin.Test, "cycle", "n59", "Out", "n0", "In0"));
            sw.Stop();

            Assert.AreEqual(TargetNodeCount - 1, state.Edges.Count, "cycle は拒否されるべき");
            Assert.Less(sw.ElapsedMilliseconds, 50,
                $"60-node 線形チェインの cycle DFS に {sw.ElapsedMilliseconds}ms — 50ms 超過");
        }
    }
}
