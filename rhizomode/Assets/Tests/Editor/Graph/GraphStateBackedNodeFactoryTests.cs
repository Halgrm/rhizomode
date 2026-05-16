#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Model;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Tests
{
    /// <summary>
    /// Codex BUG #5 regression (2026-05-16): GraphState dict 経由で登録された動的 factory が
    /// INodeFactory contract から呼べることを検証する。
    /// 旧コードは CompositeNodeFactory が AttributeScannerNodeFactory のみで、SceneTrigger / Module /
    /// Object3D 系がメニュー spawn / Snapshot restore / hydration で生成不可だった。
    /// </summary>
    public class GraphStateBackedNodeFactoryTests
    {
        private sealed class FakeNode : NodeBase
        {
            public string CtorArg { get; }
            public FakeNode(string id, string ctorArg) : base(id, "Fake")
            {
                CtorArg = ctorArg;
            }
            public override void Setup(GraphState context) { }
        }

        [Test]
        public void Adapter_QueriesGraphStateDict_AfterRegisterNodeFactory()
        {
            var state = new GraphState();
            INodeFactory factory = new GraphStateBackedNodeFactory(state);

            // 構築時点では登録ゼロ
            Assert.IsFalse(factory.CanCreate("FakeA"));

            // adapter 構築後に dict 登録
            state.RegisterNodeFactory("FakeA", id => new FakeNode(id, "first"));
            state.RegisterNodeFactory("FakeB", id => new FakeNode(id, "second"));

            Assert.IsTrue(factory.CanCreate("FakeA"));
            Assert.IsTrue(factory.CanCreate("FakeB"));

            var nodeA = factory.Create("FakeA", "id-a");
            Assert.IsInstanceOf<FakeNode>(nodeA);
            Assert.AreEqual("id-a", nodeA!.Id);
            Assert.AreEqual("first", ((FakeNode)nodeA).CtorArg);
        }

        [Test]
        public void Adapter_DelegatesToParamsFactoryWhenRegistered()
        {
            var state = new GraphState();
            INodeFactory factory = new GraphStateBackedNodeFactory(state);

            state.RegisterNodeFactory("FakeP", (System.Func<string, string, NodeBase>)((id, json) =>
                new FakeNode(id, json.Length == 0 ? "empty" : json)));

            Assert.IsTrue(factory.CanCreate("FakeP"));
            var node = factory.Create("FakeP", "id-p");
            Assert.IsInstanceOf<FakeNode>(node);
            // INodeFactory.Create は paramsJson を渡せないため adapter は "" で呼ぶ仕様
            Assert.AreEqual("empty", ((FakeNode)node!).CtorArg);
        }

        [Test]
        public void Create_WithParamsJson_PassedToParamsAwareFactory()
        {
            // Codex re-review #5 fix: SceneObjectNode 系の constructor 依存ノードを paramsJson 経由で
            // 復元できることを検証する (INodeFactory 3 引数 overload → paramsFactory)。
            var state = new GraphState();
            INodeFactory factory = new GraphStateBackedNodeFactory(state);

            state.RegisterNodeFactory("FakeC", (System.Func<string, string, NodeBase>)((id, json) =>
                new FakeNode(id, json)));

            var node = factory.Create("FakeC", "id-c", "{\"objectName\":\"Saved\"}");
            Assert.IsInstanceOf<FakeNode>(node);
            Assert.AreEqual("{\"objectName\":\"Saved\"}", ((FakeNode)node!).CtorArg);
        }

        [Test]
        public void AllTypeNames_UnionOfBothDicts_NoDuplicates()
        {
            var state = new GraphState();
            var adapter = new GraphStateBackedNodeFactory(state);

            state.RegisterNodeFactory("X", id => new FakeNode(id, "x"));
            state.RegisterNodeFactory("Y", (System.Func<string, string, NodeBase>)((id, _) => new FakeNode(id, "y")));

            var names = new System.Collections.Generic.HashSet<string>(adapter.AllTypeNames);
            Assert.IsTrue(names.Contains("X"));
            Assert.IsTrue(names.Contains("Y"));
            Assert.AreEqual(2, names.Count);
        }
    }
}
