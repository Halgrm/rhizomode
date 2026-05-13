#nullable enable

using NUnit.Framework;
using R3;
using Rhizomode.Graph.Events;

namespace Rhizomode.Graph.Tests
{
    public class GraphMutationScopeTests
    {
        [Test]
        public void Dispose_EmitsChangeSetOnce()
        {
            var bus = new GraphEventBus();
            GraphChangeSet? captured = null;
            using var sub = bus.OnGraphChanged.Subscribe(cs => captured = cs);

            using (var scope = new GraphMutationScope(bus))
            {
                scope.RecordNodeAdded("n1");
                scope.RecordEdgeAdded("e1");
            }

            Assert.IsNotNull(captured);
            CollectionAssert.AreEqual(new[] { "n1" }, captured!.AddedNodeIds);
            CollectionAssert.AreEqual(new[] { "e1" }, captured.AddedEdgeIds);
        }

        [Test]
        public void Dispose_EmptyScope_DoesNotEmit()
        {
            var bus = new GraphEventBus();
            var fired = false;
            using var sub = bus.OnGraphChanged.Subscribe(_ => fired = true);

            using (var _ = new GraphMutationScope(bus))
            {
                // 何も record しない
            }

            Assert.IsFalse(fired);
        }
    }
}
