#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.Model;

namespace Rhizomode.Graph.Tests
{
    public class EdgeIndexTests
    {
        private static Edge MakeEdge(string id, string from, string to) =>
            new(id, from, "Out", to, "In");

        [Test]
        public void Add_StoresEdgeAndIndexes()
        {
            var index = new EdgeIndex();
            var edge = MakeEdge("e1", "n1", "n2");

            Assert.IsTrue(index.Add(edge));

            Assert.AreEqual(1, index.Count);
            Assert.AreSame(edge, index.GetById("e1"));
            Assert.IsTrue(index.ContainsEndpoint("n1", "Out", "n2", "In"));
            CollectionAssert.Contains(index.OutgoingEdgeIds("n1"), "e1");
            CollectionAssert.Contains(index.IncomingEdgeIds("n2"), "e1");
        }

        [Test]
        public void Add_DuplicateEndpoint_Fails()
        {
            var index = new EdgeIndex();
            index.Add(MakeEdge("e1", "n1", "n2"));

            Assert.IsFalse(index.Add(MakeEdge("e2", "n1", "n2")));
            Assert.AreEqual(1, index.Count);
        }

        [Test]
        public void RemoveById_RemovesEdgeAndDeindexes()
        {
            var index = new EdgeIndex();
            index.Add(MakeEdge("e1", "n1", "n2"));

            Assert.IsTrue(index.RemoveById("e1"));
            Assert.AreEqual(0, index.Count);
            Assert.IsNull(index.GetById("e1"));
            CollectionAssert.DoesNotContain(index.OutgoingEdgeIds("n1"), "e1");
        }

        [Test]
        public void Clear_EmptiesAllIndices()
        {
            var index = new EdgeIndex();
            index.Add(MakeEdge("e1", "n1", "n2"));
            index.Add(MakeEdge("e2", "n2", "n3"));

            index.Clear();

            Assert.AreEqual(0, index.Count);
            Assert.IsNull(index.GetById("e1"));
        }
    }
}
