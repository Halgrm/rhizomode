#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.Model;

namespace Rhizomode.Graph.Tests
{
    public class CycleDetectorTests
    {
        private static Edge MakeEdge(string id, string from, string to) =>
            new(id, from, "Out", to, "In");

        [Test]
        public void WouldCreateCycle_SelfLoop_ReturnsTrue()
        {
            var index = new EdgeIndex();
            Assert.IsTrue(CycleDetector.WouldCreateCycle(index, "n1", "n1"));
        }

        [Test]
        public void WouldCreateCycle_ReverseExistingPath_ReturnsTrue()
        {
            var index = new EdgeIndex();
            index.Add(MakeEdge("e1", "n1", "n2"));
            index.Add(MakeEdge("e2", "n2", "n3"));

            // n3 → n1 を追加すると n1 → n2 → n3 → n1 のサイクル
            Assert.IsTrue(CycleDetector.WouldCreateCycle(index, "n3", "n1"));
        }

        [Test]
        public void WouldCreateCycle_NewBranch_ReturnsFalse()
        {
            var index = new EdgeIndex();
            index.Add(MakeEdge("e1", "n1", "n2"));

            // n1 → n3 は新しい枝、サイクルにならない
            Assert.IsFalse(CycleDetector.WouldCreateCycle(index, "n1", "n3"));
        }

        [Test]
        public void HasCycle_AcyclicGraph_ReturnsFalse()
        {
            var index = new EdgeIndex();
            index.Add(MakeEdge("e1", "n1", "n2"));
            index.Add(MakeEdge("e2", "n2", "n3"));

            Assert.IsFalse(CycleDetector.HasCycle(index));
        }
    }
}
