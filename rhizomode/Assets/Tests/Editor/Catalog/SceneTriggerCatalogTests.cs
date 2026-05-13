#nullable enable

using NUnit.Framework;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Nodes.Scene;
using UnityEngine;
using SceneTriggerEntry = Rhizomode.Nodes.Scene.SceneTriggerCatalog.Entry;

namespace Rhizomode.Catalog.Tests
{
    /// <summary>
    /// Phase 4F Round B: SceneTriggerCatalog SO 経由の動的 typeName 登録検証。
    /// </summary>
    public class SceneTriggerCatalogTests
    {
        private static SceneTriggerCatalog CreateCatalog(params (string typeName, string label, int sceneIndex)[] entries)
        {
            var so = ScriptableObject.CreateInstance<SceneTriggerCatalog>();
            // SerializedField 経由でしか入らないので、テスト用に reflection で entries フィールドを差し替える
            var field = typeof(SceneTriggerCatalog).GetField(
                "entries",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "entries field reflection failed");
            var list = new System.Collections.Generic.List<SceneTriggerEntry>();
            foreach (var (t, l, i) in entries)
            {
                list.Add(new SceneTriggerEntry(t, l, i));
            }
            field!.SetValue(so, list);
            return so;
        }

        [Test]
        public void Catalog_FindByTypeName_ReturnsEntry()
        {
            var catalog = CreateCatalog(
                ("SceneDark", "Dark", 0),
                ("SceneWhite", "White", 1),
                ("SceneNature", "Nature", 2));

            var entry = catalog.FindByTypeName("SceneWhite");
            Assert.IsNotNull(entry);
            Assert.AreEqual(1, entry!.SceneIndex);
        }

        [Test]
        public void Catalog_FindByTypeName_Unknown_ReturnsNull()
        {
            var catalog = CreateCatalog(("SceneDark", "Dark", 0));
            Assert.IsNull(catalog.FindByTypeName("Bogus"));
        }

        [Test]
        public void SceneTriggerFactory_CanCreateAllRegistered()
        {
            var catalog = CreateCatalog(
                ("SceneDark", "Dark", 0),
                ("SceneWhite", "White", 1),
                ("SceneNature", "Nature", 2));
            var factory = new SceneTriggerNodeFactory(catalog);

            Assert.IsTrue(factory.CanCreate("SceneDark"));
            Assert.IsTrue(factory.CanCreate("SceneWhite"));
            Assert.IsTrue(factory.CanCreate("SceneNature"));
            Assert.IsFalse(factory.CanCreate("Bogus"));
        }

        [Test]
        public void SceneTriggerFactory_Create_AppliesSceneIndex()
        {
            var catalog = CreateCatalog(("SceneWhite", "White", 5));
            var factory = new SceneTriggerNodeFactory(catalog);

            var node = factory.Create("SceneWhite", "n1");
            Assert.IsNotNull(node);
            Assert.IsInstanceOf<SceneTriggerNode>(node);
            var st = (SceneTriggerNode)node!;
            Assert.AreEqual(5, st.SceneIndex);
            Assert.AreEqual("SceneWhite", st.NodeType);
        }

        [Test]
        public void CompositeFactory_DelegatesToFirstMatch()
        {
            var catalog = CreateCatalog(("SceneDark", "Dark", 0));
            var sceneFactory = new SceneTriggerNodeFactory(catalog);

            // Scanner 由来の静的 factory (ConstFloat 等を含む) と合成
            var scanner = new NodeTypeAttributeScanner();
            var staticFactory = new AttributeScannerNodeFactory(scanner.Scan());

            var composite = new CompositeNodeFactory(new Rhizomode.Graph.CatalogBridge.INodeFactory[]
            {
                staticFactory, sceneFactory
            });

            // 静的: ConstFloat
            Assert.IsTrue(composite.CanCreate("ConstFloat"));
            Assert.IsNotNull(composite.Create("ConstFloat", "n1"));

            // 動的: SceneDark
            Assert.IsTrue(composite.CanCreate("SceneDark"));
            Assert.IsNotNull(composite.Create("SceneDark", "n2"));

            // 未知
            Assert.IsFalse(composite.CanCreate("Nope"));
            Assert.IsNull(composite.Create("Nope", "n3"));
        }
    }
}
