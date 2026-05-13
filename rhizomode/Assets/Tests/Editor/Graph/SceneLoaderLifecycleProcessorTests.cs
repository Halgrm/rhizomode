#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Scene.Contracts;
using Rhizomode.Scene.GraphAdapter;

namespace Rhizomode.Graph.Tests
{
    public class SceneLoaderLifecycleProcessorTests
    {
        private sealed class FakeSceneLoader : ISceneLoader
        {
            public int SceneCount => 0;
            public void LoadScene(int index) { }
            public void UnloadCurrentScene() { }
            public string? GetSceneName(int index) => null;
        }

        /// <summary>非 ISceneLoaderConsumer ノード (注入対象外)。</summary>
        private sealed class PlainNode : NodeBase
        {
            public PlainNode(string id) : base(id, "Plain") { }
            public override void Setup(GraphState context) { }
        }

        /// <summary>ISceneLoaderConsumer 実装の test double。</summary>
        private sealed class ConsumerNode : NodeBase, ISceneLoaderConsumer
        {
            public ISceneLoader? Loader { get; set; }
            public ConsumerNode(string id) : base(id, "Consumer") { }
            public override void Setup(GraphState context) { }
        }

        [Test]
        public void BeforeSetup_Consumer_InjectsLoader()
        {
            var loader = new FakeSceneLoader();
            var proc = new SceneLoaderLifecycleProcessor(loader);
            var node = new ConsumerNode("n1");

            proc.BeforeSetup(node, NodeInitMode.FreshSpawn);

            Assert.AreSame(loader, node.Loader);
        }

        [Test]
        public void BeforeSetup_NonConsumer_NoOp()
        {
            var loader = new FakeSceneLoader();
            var proc = new SceneLoaderLifecycleProcessor(loader);
            var node = new PlainNode("n1");

            // 例外を投げずに何もしない
            Assert.DoesNotThrow(() => proc.BeforeSetup(node, NodeInitMode.FreshSpawn));
        }

        [Test]
        public void BeforeSetup_NullLoader_StillInjectsNullSafely()
        {
            var proc = new SceneLoaderLifecycleProcessor(null);
            var node = new ConsumerNode("n1") { Loader = new FakeSceneLoader() };

            proc.BeforeSetup(node, NodeInitMode.Deserialize);

            Assert.IsNull(node.Loader);
        }
    }
}
