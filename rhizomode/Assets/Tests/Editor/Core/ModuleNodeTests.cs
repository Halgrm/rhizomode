#nullable enable

using System.Linq;
using NUnit.Framework;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Modules;
using UnityEngine;

namespace Rhizomode.Core.Tests
{
    public class ModuleNodeTests
    {
        [Test]
        public void VFXModuleNode_RegistersPortsFromDefinition()
        {
            var def = ScriptableObject.CreateInstance<ModuleDefinition>();
            def.moduleName = "TestVFX";
            def.parameters = new()
            {
                new ParamDefinition { name = "Intensity", type = ParamType.Float, minFloat = 0f, maxFloat = 10f },
                new ParamDefinition { name = "BaseColor", type = ParamType.Color },
                new ParamDefinition { name = "Active", type = ParamType.Bool },
            };
            def.events = new() { "Spawn", "Burst" };

            var node = new VFXModuleNode("test-1", def);
            var ports = node.GetPortDefinitions();

            // パラメータ3つ + イベント2つ = 5入力ポート
            Assert.AreEqual(5, ports.Count);
            Assert.IsTrue(ports.Any(p => p.name == "Intensity" && p.type == ParamType.Float));
            Assert.IsTrue(ports.Any(p => p.name == "BaseColor" && p.type == ParamType.Color));
            Assert.IsTrue(ports.Any(p => p.name == "Active" && p.type == ParamType.Bool));
            Assert.IsTrue(ports.Any(p => p.name == "Spawn" && p.type == ParamType.Bool));
            Assert.IsTrue(ports.Any(p => p.name == "Burst" && p.type == ParamType.Bool));
        }

        [Test]
        public void ModuleNode_MinMaxPreserved()
        {
            var def = ScriptableObject.CreateInstance<ModuleDefinition>();
            def.moduleName = "TestShader";
            def.parameters = new()
            {
                new ParamDefinition { name = "Speed", type = ParamType.Float, minFloat = -5f, maxFloat = 5f, defaultFloat = 1f },
            };
            def.events = new();

            var node = new ShaderModuleNode("test-2", def);

            Assert.AreEqual("Shader_TestShader", node.NodeType);
            Assert.AreEqual(-5f, node.Definition.parameters[0].minFloat);
            Assert.AreEqual(5f, node.Definition.parameters[0].maxFloat);
            Assert.AreEqual(1f, node.Definition.parameters[0].defaultFloat);
        }

        [Test]
        public void ModuleDefinition_IsEvent_ReturnsCorrectly()
        {
            var def = ScriptableObject.CreateInstance<ModuleDefinition>();
            def.events = new() { "Spawn", "Burst" };

            Assert.IsTrue(def.IsEvent("Spawn"));
            Assert.IsTrue(def.IsEvent("Burst"));
            Assert.IsFalse(def.IsEvent("Intensity"));
        }
    }
}
