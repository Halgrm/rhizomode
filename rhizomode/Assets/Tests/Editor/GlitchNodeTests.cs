#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Scene;

namespace Rhizomode.Nodes.Scene.Tests
{
    public sealed class GlitchNodeTests
    {
        [Test]
        public void Inactive_ReturnsZero()
        {
            using var graphState = new GraphState();
            var node = new GlitchNode("glitch");
            node.Setup(graphState);

            node.GetInputPort("Active")!.OnNext(false);

            Assert.AreEqual(0f, node.CurrentAmount);
            node.Dispose();
        }

        [Test]
        public void Active_UsesAmount()
        {
            using var graphState = new GraphState();
            var node = new GlitchNode("glitch");
            node.Setup(graphState);

            node.GetInputPort("Active")!.OnNext(true);
            node.GetInputPort("Amount")!.OnNext(0.5f);

            Assert.AreEqual(0.5f, node.CurrentAmount);
            node.Dispose();
        }

        [Test]
        public void Active_ClampsAmount()
        {
            using var graphState = new GraphState();
            var node = new GlitchNode("glitch");
            node.Setup(graphState);

            node.GetInputPort("Active")!.OnNext(true);
            node.GetInputPort("Amount")!.OnNext(1.5f);

            Assert.AreEqual(1f, node.CurrentAmount);
            node.Dispose();
        }
    }
}
