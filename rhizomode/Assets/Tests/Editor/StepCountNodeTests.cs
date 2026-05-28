#nullable enable

using NUnit.Framework;
using R3;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Utility;

namespace Rhizomode.Nodes.Tests
{
    /// <summary>
    /// Verifies the 8- and 16-step Count variants advance one-hot and wrap at their step count.
    /// </summary>
    public sealed class StepCountNodeTests
    {
        [Test]
        public void Count8_WrapsAtEight()
        {
            using var fixture = new IndexFixture(new Count8Node("count8"));
            AssertWrap(fixture, 8);
        }

        [Test]
        public void Count16_WrapsAtSixteen()
        {
            using var fixture = new IndexFixture(new Count16Node("count16"));
            AssertWrap(fixture, 16);
        }

        [Test]
        public void Count8_HasEightStepOutputs()
        {
            using var node = new Count8Node("count8");
            for (var i = 1; i <= 8; i++)
                Assert.IsNotNull(node.GetOutputPort(i.ToString()), $"missing output {i}");
            Assert.IsNull(node.GetOutputPort("9"), "should not have a 9th step output");
        }

        // Step through one full cycle plus one to confirm the index lands back on 1.
        private static void AssertWrap(IndexFixture fixture, int steps)
        {
            for (var i = 1; i <= steps; i++)
            {
                fixture.Rise();
                Assert.AreEqual(i, fixture.Index, $"step {i}");
                fixture.Fall();
            }

            fixture.Rise();
            Assert.AreEqual(1f, fixture.Index, "should wrap back to 1");
        }

        private sealed class IndexFixture : System.IDisposable
        {
            private readonly GraphState _graphState = new();
            private readonly StepCountNodeBase _node;

            public float Index { get; private set; }

            public IndexFixture(StepCountNodeBase node)
            {
                _node = node;
                _node.Setup(_graphState);
                ((OutputPort<float>)_node.GetOutputPort("Index")!).Observable.Subscribe(v => Index = v);
            }

            public void Rise() => _node.GetInputPort("Trigger")!.OnNext(true);

            public void Fall() => _node.GetInputPort("Trigger")!.OnNext(false);

            public void Dispose()
            {
                _node.Dispose();
                _graphState.Dispose();
            }
        }
    }
}
