#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using R3;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Math;
using Rhizomode.SharedKernel;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// <see cref="LinearToDbNode"/> / <see cref="DbToLinearNode"/> の代表値と
    /// 非正/NaN/Inf 入力でのフォールバック挙動。
    /// </summary>
    public class LinearDbConversionNodeTests
    {
        private const float Tolerance = 1e-3f;
        private const float SilentFloorDb = -120f;

        [Test]
        public void LinearToDb_Unity_Returns0Db()
        {
            var (node, state) = Build<LinearToDbNode>();
            using var d = new CompositeDisposable();
            var values = SubscribeOutput<float>(node, "dB", d);

            FeedFloat(state, node, "Linear", 1f);

            Assert.AreEqual(0f, values[^1], Tolerance);
        }

        [Test]
        public void LinearToDb_Half_ReturnsMinus6Db()
        {
            var (node, state) = Build<LinearToDbNode>();
            using var d = new CompositeDisposable();
            var values = SubscribeOutput<float>(node, "dB", d);

            FeedFloat(state, node, "Linear", 0.5f);

            // -6.0206 dB
            Assert.AreEqual(-6.02f, values[^1], 0.01f);
        }

        [Test]
        public void LinearToDb_Silent_ReturnsSilentFloor()
        {
            var (node, state) = Build<LinearToDbNode>();
            using var d = new CompositeDisposable();
            var values = SubscribeOutput<float>(node, "dB", d);

            FeedFloat(state, node, "Linear", 0f);
            FeedFloat(state, node, "Linear", -0.1f);
            FeedFloat(state, node, "Linear", float.NaN);

            Assert.AreEqual(3, values.Count);
            foreach (var v in values) Assert.AreEqual(SilentFloorDb, v, Tolerance);
        }

        [Test]
        public void DbToLinear_0Db_Returns1()
        {
            var (node, state) = Build<DbToLinearNode>();
            using var d = new CompositeDisposable();
            var values = SubscribeOutput<float>(node, "Linear", d);

            FeedFloat(state, node, "dB", 0f);

            Assert.AreEqual(1f, values[^1], Tolerance);
        }

        [Test]
        public void DbToLinear_Minus6Db_ReturnsHalf()
        {
            var (node, state) = Build<DbToLinearNode>();
            using var d = new CompositeDisposable();
            var values = SubscribeOutput<float>(node, "Linear", d);

            FeedFloat(state, node, "dB", -6.02f);

            Assert.AreEqual(0.5f, values[^1], 0.01f);
        }

        [Test]
        public void DbToLinear_NaNOrInf_Returns0()
        {
            var (node, state) = Build<DbToLinearNode>();
            using var d = new CompositeDisposable();
            var values = SubscribeOutput<float>(node, "Linear", d);

            FeedFloat(state, node, "dB", float.NaN);
            FeedFloat(state, node, "dB", float.PositiveInfinity);
            FeedFloat(state, node, "dB", float.NegativeInfinity);

            Assert.AreEqual(3, values.Count);
            foreach (var v in values) Assert.AreEqual(0f, v, Tolerance);
        }

        private static (T, GraphState) Build<T>() where T : NodeBase
        {
            var node = (T)System.Activator.CreateInstance(typeof(T), "n")!;
            var state = new GraphState();
            state.RegisterNode(node); // RegisterNode が内部で node.Setup(this) を呼ぶ
            return (node, state);
        }

        private static void FeedFloat(GraphState state, NodeBase node, string portName, float value)
        {
            var port = node.GetInputPort(portName);
            Assert.NotNull(port);
            port!.OnNext(value);
        }

        private static List<T> SubscribeOutput<T>(NodeBase node, string portName, CompositeDisposable disposables)
        {
            var port = (OutputPort<T>)node.GetOutputPort(portName)!;
            var values = new List<T>();
            disposables.Add(port.Observable.Subscribe(v => values.Add(v)));
            return values;
        }
    }
}
