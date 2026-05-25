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
    /// <see cref="HzToNoteNode"/> / <see cref="NoteToHzNode"/> の代表値変換と
    /// NaN / Inf / 非正入力のフォールバック挙動を検証する。
    /// </summary>
    public class HzNoteConversionNodeTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void HzToNote_A4_Returns69()
        {
            var (node, state) = BuildHzToNote();
            using var disposables = new CompositeDisposable();
            var notes = SubscribeOutput<float>(node, "Note", disposables);

            FeedFloat(state, node, "Hz", 440f);

            Assert.AreEqual(1, notes.Count);
            Assert.AreEqual(69f, notes[0], Tolerance);
        }

        [Test]
        public void HzToNote_OctaveUp_AddsTwelve()
        {
            var (node, state) = BuildHzToNote();
            using var disposables = new CompositeDisposable();
            var notes = SubscribeOutput<float>(node, "Note", disposables);

            FeedFloat(state, node, "Hz", 880f);

            Assert.AreEqual(81f, notes[^1], Tolerance);
        }

        [Test]
        public void HzToNote_NonPositive_Returns0()
        {
            var (node, state) = BuildHzToNote();
            using var disposables = new CompositeDisposable();
            var notes = SubscribeOutput<float>(node, "Note", disposables);

            FeedFloat(state, node, "Hz", 0f);
            FeedFloat(state, node, "Hz", -10f);
            FeedFloat(state, node, "Hz", float.NaN);

            Assert.AreEqual(3, notes.Count);
            foreach (var n in notes) Assert.AreEqual(0f, n, Tolerance);
        }

        [Test]
        public void NoteToHz_69_Returns440()
        {
            var (node, state) = BuildNoteToHz();
            using var disposables = new CompositeDisposable();
            var hz = SubscribeOutput<float>(node, "Hz", disposables);

            FeedFloat(state, node, "Note", 69f);

            Assert.AreEqual(440f, hz[^1], Tolerance);
        }

        [Test]
        public void NoteToHz_OctaveUp_DoublesFrequency()
        {
            var (node, state) = BuildNoteToHz();
            using var disposables = new CompositeDisposable();
            var hz = SubscribeOutput<float>(node, "Hz", disposables);

            FeedFloat(state, node, "Note", 81f);

            Assert.AreEqual(880f, hz[^1], Tolerance);
        }

        [Test]
        public void NoteToHz_NaN_Returns0()
        {
            var (node, state) = BuildNoteToHz();
            using var disposables = new CompositeDisposable();
            var hz = SubscribeOutput<float>(node, "Hz", disposables);

            FeedFloat(state, node, "Note", float.NaN);

            Assert.AreEqual(0f, hz[^1], Tolerance);
        }

        [Test]
        public void HzToNote_ThenNoteToHz_RoundTrip()
        {
            var hzInput = 261.6256f; // C4
            var (h2n, state1) = BuildHzToNote();
            using var d1 = new CompositeDisposable();
            var notes = SubscribeOutput<float>(h2n, "Note", d1);
            FeedFloat(state1, h2n, "Hz", hzInput);

            var (n2h, state2) = BuildNoteToHz();
            using var d2 = new CompositeDisposable();
            var hzOut = SubscribeOutput<float>(n2h, "Hz", d2);
            FeedFloat(state2, n2h, "Note", notes[^1]);

            Assert.AreEqual(hzInput, hzOut[^1], 0.01f);
        }

        private static (HzToNoteNode, GraphState) BuildHzToNote()
        {
            var node = new HzToNoteNode("n");
            var state = new GraphState();
            state.RegisterNode(node); // RegisterNode が内部で node.Setup(this) を呼ぶ
            return (node, state);
        }

        private static (NoteToHzNode, GraphState) BuildNoteToHz()
        {
            var node = new NoteToHzNode("n");
            var state = new GraphState();
            state.RegisterNode(node);
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
