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
    /// <see cref="BpmToSecNode"/> / <see cref="SecToBpmNode"/> の代表値とゼロ除算ガード。
    /// </summary>
    public class BpmSecConversionNodeTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void BpmToSec_120_Returns0_5()
        {
            var (node, state) = Build<BpmToSecNode>();
            using var d = new CompositeDisposable();
            var values = SubscribeOutput<float>(node, "Seconds", d);

            FeedFloat(state, node, "BPM", 120f);

            Assert.AreEqual(0.5f, values[^1], Tolerance);
        }

        [Test]
        public void BpmToSec_60_Returns1()
        {
            var (node, state) = Build<BpmToSecNode>();
            using var d = new CompositeDisposable();
            var values = SubscribeOutput<float>(node, "Seconds", d);

            FeedFloat(state, node, "BPM", 60f);

            Assert.AreEqual(1f, values[^1], Tolerance);
        }

        [Test]
        public void BpmToSec_Zero_Returns0()
        {
            var (node, state) = Build<BpmToSecNode>();
            using var d = new CompositeDisposable();
            var values = SubscribeOutput<float>(node, "Seconds", d);

            FeedFloat(state, node, "BPM", 0f);
            FeedFloat(state, node, "BPM", -10f);
            FeedFloat(state, node, "BPM", float.NaN);

            Assert.AreEqual(3, values.Count);
            foreach (var v in values) Assert.AreEqual(0f, v, Tolerance);
        }

        [Test]
        public void SecToBpm_0_5_Returns120()
        {
            var (node, state) = Build<SecToBpmNode>();
            using var d = new CompositeDisposable();
            var values = SubscribeOutput<float>(node, "BPM", d);

            FeedFloat(state, node, "Seconds", 0.5f);

            Assert.AreEqual(120f, values[^1], Tolerance);
        }

        [Test]
        public void SecToBpm_Zero_Returns0()
        {
            var (node, state) = Build<SecToBpmNode>();
            using var d = new CompositeDisposable();
            var values = SubscribeOutput<float>(node, "BPM", d);

            FeedFloat(state, node, "Seconds", 0f);

            Assert.AreEqual(0f, values[^1], Tolerance);
        }

        [Test]
        public void RoundTrip_BpmToSecAndBack_Preserves()
        {
            var (b2s, state1) = Build<BpmToSecNode>();
            using var d1 = new CompositeDisposable();
            var secs = SubscribeOutput<float>(b2s, "Seconds", d1);
            FeedFloat(state1, b2s, "BPM", 128f);

            var (s2b, state2) = Build<SecToBpmNode>();
            using var d2 = new CompositeDisposable();
            var bpms = SubscribeOutput<float>(s2b, "BPM", d2);
            FeedFloat(state2, s2b, "Seconds", secs[^1]);

            Assert.AreEqual(128f, bpms[^1], Tolerance);
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
