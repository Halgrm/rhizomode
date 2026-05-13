#nullable enable

using NUnit.Framework;
using Rhizomode.Modules;
using Rhizomode.Nodes.Modules;
using UnityEngine;

namespace Rhizomode.Catalog.Tests
{
    /// <summary>
    /// Phase 4F Round C: ModuleDefinitionNodeFactory / Object3DNodeFactory の動的 typeName 登録検証。
    /// </summary>
    public class ModuleDefinitionNodeFactoryTests
    {
        private static ModuleDefinition MakeDef(string moduleName)
        {
            var so = ScriptableObject.CreateInstance<ModuleDefinition>();
            so.moduleName = moduleName;
            return so;
        }

        [Test]
        public void ModuleFactory_VfxPrefix_CreatesVFXModuleNode()
        {
            var factory = new ModuleDefinitionNodeFactory(new[]
            {
                MakeDef("Drums"), MakeDef("Bass")
            });

            Assert.IsTrue(factory.CanCreate("VFX_Drums"));
            var node = factory.Create("VFX_Drums", "n1");
            Assert.IsInstanceOf<VFXModuleNode>(node);
            Assert.AreEqual("VFX_Drums", node!.NodeType);
        }

        [Test]
        public void ModuleFactory_ShaderPrefix_CreatesShaderModuleNode()
        {
            var factory = new ModuleDefinitionNodeFactory(new[] { MakeDef("Blob") });

            Assert.IsTrue(factory.CanCreate("Shader_Blob"));
            var node = factory.Create("Shader_Blob", "n1");
            Assert.IsInstanceOf<ShaderModuleNode>(node);
            Assert.AreEqual("Shader_Blob", node!.NodeType);
        }

        [Test]
        public void ModuleFactory_UnknownModuleName_ReturnsNull()
        {
            var factory = new ModuleDefinitionNodeFactory(new[] { MakeDef("Drums") });

            Assert.IsFalse(factory.CanCreate("VFX_NotRegistered"));
            Assert.IsNull(factory.Create("VFX_NotRegistered", "n1"));
        }

        [Test]
        public void ModuleFactory_NoPrefixOrWrongPrefix_ReturnsNull()
        {
            var factory = new ModuleDefinitionNodeFactory(new[] { MakeDef("Drums") });

            Assert.IsFalse(factory.CanCreate("Drums"));
            Assert.IsFalse(factory.CanCreate("ConstFloat"));
            Assert.IsNull(factory.Create("Drums", "n1"));
        }

        [Test]
        public void ModuleFactory_AllTypeNames_IncludesBothPrefixes()
        {
            var factory = new ModuleDefinitionNodeFactory(new[] { MakeDef("Foo") });
            var names = System.Linq.Enumerable.ToList(factory.AllTypeNames);
            CollectionAssert.Contains(names, "VFX_Foo");
            CollectionAssert.Contains(names, "Shader_Foo");
            Assert.AreEqual(2, names.Count);
        }

        [Test]
        public void Object3DFactory_RegisteredPrefab_Creates()
        {
            var prefabList = ScriptableObject.CreateInstance<Object3DPrefabList>();
            var go = new GameObject("MyCube");
            try
            {
                var field = typeof(Object3DPrefabList).GetField(
                    "prefabs",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(field);
                field!.SetValue(prefabList, new[] { go });

                var factory = new Object3DNodeFactory(prefabList);

                Assert.IsTrue(factory.CanCreate("Object3D_MyCube"));
                var node = factory.Create("Object3D_MyCube", "n1");
                Assert.IsInstanceOf<Object3DNode>(node);
                Assert.AreEqual("MyCube", ((Object3DNode)node!).PrefabName);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Object3DFactory_WrongPrefix_Rejects()
        {
            var prefabList = ScriptableObject.CreateInstance<Object3DPrefabList>();
            var factory = new Object3DNodeFactory(prefabList);

            Assert.IsFalse(factory.CanCreate("VFX_Cube"));
            Assert.IsFalse(factory.CanCreate("Cube"));
            Assert.IsNull(factory.Create("Cube", "n1"));
        }
    }
}
