#nullable enable

using NUnit.Framework;
using R3;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Utility;

namespace Rhizomode.Nodes.Tests
{
    public sealed class CountNodeTests
    {
        [Test]
        public void OneHotAdvance()
        {
            using var fixture = new CountFixture();

            for (var i = 1; i <= 4; i++)
            {
                fixture.Rise();
                Assert.AreEqual(i == 1, fixture.One);
                Assert.AreEqual(i == 2, fixture.Two);
                Assert.AreEqual(i == 3, fixture.Three);
                Assert.AreEqual(i == 4, fixture.Four);
                Assert.AreEqual(i, fixture.Index);
                fixture.Fall();
            }
        }

        [Test]
        public void Wrap()
        {
            using var fixture = new CountFixture();

            for (var i = 0; i < 4; i++)
            {
                fixture.Rise();
                fixture.Fall();
            }

            fixture.Rise();
            Assert.AreEqual(1f, fixture.Index);
            Assert.IsTrue(fixture.One);
            Assert.IsFalse(fixture.Two);
            Assert.IsFalse(fixture.Three);
            Assert.IsFalse(fixture.Four);
        }

        [Test]
        public void NoRisingEdgeOnHold()
        {
            using var fixture = new CountFixture();

            fixture.Rise();
            fixture.Rise();

            Assert.AreEqual(1, fixture.IndexEmissionCount);
            Assert.AreEqual(1f, fixture.Index);
        }

        private sealed class CountFixture : System.IDisposable
        {
            private readonly GraphState _graphState = new();
            private readonly CountNode _node = new("count");

            public bool One { get; private set; }
            public bool Two { get; private set; }
            public bool Three { get; private set; }
            public bool Four { get; private set; }
            public float Index { get; private set; }
            public int IndexEmissionCount { get; private set; }

            public CountFixture()
            {
                _node.Setup(_graphState);
                SubscribeOutputs();
            }

            public void Rise() => _node.GetInputPort("Trigger")!.OnNext(true);

            public void Fall() => _node.GetInputPort("Trigger")!.OnNext(false);

            public void Dispose()
            {
                _node.Dispose();
                _graphState.Dispose();
            }

            private void SubscribeOutputs()
            {
                ((OutputPort<bool>)_node.GetOutputPort("1")!).Observable.Subscribe(v => One = v);
                ((OutputPort<bool>)_node.GetOutputPort("2")!).Observable.Subscribe(v => Two = v);
                ((OutputPort<bool>)_node.GetOutputPort("3")!).Observable.Subscribe(v => Three = v);
                ((OutputPort<bool>)_node.GetOutputPort("4")!).Observable.Subscribe(v => Four = v);
                ((OutputPort<float>)_node.GetOutputPort("Index")!).Observable.Subscribe(OnIndex);
            }

            private void OnIndex(float value)
            {
                Index = value;
                IndexEmissionCount++;
            }
        }
    }
}
