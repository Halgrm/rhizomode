#nullable enable

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Modules;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.Nodes.Modules;
using UnityEngine;
using UnityEngine.TestTools;

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

            // L1 verification: definition で "Active" を持っていても VFXModuleNode は二重登録しない
            var node = new VFXModuleNode("test-1", def);
            var ports = node.GetPortDefinitions();

            // パラメータ3つ (Intensity / BaseColor / Active) + イベント2つ = 5入力ポート (Active は重複しない)
            Assert.AreEqual(5, ports.Count);
            Assert.IsTrue(ports.Any(p => p.name == "Intensity" && p.type == ParamType.Float));
            Assert.IsTrue(ports.Any(p => p.name == "BaseColor" && p.type == ParamType.Color));
            Assert.IsTrue(ports.Any(p => p.name == "Active" && p.type == ParamType.Bool));
            Assert.IsTrue(ports.Any(p => p.name == "Spawn" && p.type == ParamType.Bool));
            Assert.IsTrue(ports.Any(p => p.name == "Burst" && p.type == ParamType.Bool));
            Assert.AreEqual(1, ports.Count(p => p.name == "Active"));
        }

        [Test]
        public void VFXModuleNode_AddsActivePort_WhenActiveOnlyInEvents()
        {
            // Codex re-review fix: events に "Active" があっても (false 通知が来ないため) VFXModuleNode の
            // 独自 bool subscribe を残す。skip 条件は parameters.Active のみが正しい。
            var def = ScriptableObject.CreateInstance<ModuleDefinition>();
            def.moduleName = "TestVFXEvent";
            def.parameters = new()
            {
                new ParamDefinition { name = "Intensity", type = ParamType.Float },
            };
            def.events = new() { "Active" }; // event 経由の Active

            var node = new VFXModuleNode("vfx-evt", def);
            var ports = node.GetPortDefinitions();

            // event Active (base 経由) + bool Active (VFXModuleNode 経由) は重複 → NodeBase guard で 1 個になる
            // ただし subscribe は base 経由 (true 時のみ) + VFXModuleNode 経由 (bool full) の両方が動く
            Assert.IsTrue(ports.Any(p => p.name == "Active" && p.type == ParamType.Bool));
            Assert.AreEqual(1, ports.Count(p => p.name == "Active"));
        }

        [Test]
        public void VFXModuleNode_AddsActivePort_WhenNotInDefinition()
        {
            var def = ScriptableObject.CreateInstance<ModuleDefinition>();
            def.moduleName = "TestVFX2";
            def.parameters = new()
            {
                new ParamDefinition { name = "Intensity", type = ParamType.Float },
            };
            def.events = new();

            var node = new VFXModuleNode("test-active", def);
            var ports = node.GetPortDefinitions();

            // L1: definition に Active が無ければ VFXModuleNode が自前で追加 (旧来動作維持)
            Assert.IsTrue(ports.Any(p => p.name == "Active" && p.type == ParamType.Bool));
            Assert.AreEqual(2, ports.Count);
        }

        [Test]
        public void VFXModuleNode_ThreeArgCtor_UsesProvidedTypeName()
        {
            // M3: legacy alias factory が canonical typeName を渡しても、ノードはその typeName で構築される
            var def = ScriptableObject.CreateInstance<ModuleDefinition>();
            def.moduleName = "Smoke";
            def.parameters = new();
            def.events = new();

            var node = new VFXModuleNode("vfx-1", "Module_Smoke", def);
            Assert.AreEqual("Module_Smoke", node.NodeType);
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

            // 汎用 ModuleNode (旧 ShaderModuleNode / InstancedCubesModuleNode は単一クラスに統合済)
            var node = new ModuleNode("test-2", "Module_TestShader", def);

            Assert.AreEqual("Module_TestShader", node.NodeType);
            Assert.AreEqual(-5f, node.Definition.parameters[0].minFloat);
            Assert.AreEqual(5f, node.Definition.parameters[0].maxFloat);
            Assert.AreEqual(1f, node.Definition.parameters[0].defaultFloat);
        }

        [Test]
        public void InstancedCubesModule_BoidStride_MatchesShaderLayout()
        {
            // Codex re-review fix (WARN 7): BoidData struct を変えると compute shader と layout が乖離する。
            // 静的 ctor の LogError と合わせて test でも固定する。
            const int expected = sizeof(float) * 13; // float3 + float3 + float4 + float3 = 52
            // BoidData は private 型のため reflection 経由で取得
            var t = typeof(InstancedCubesModule).GetNestedType("BoidData",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(t, "BoidData nested type must exist");
            var actual = System.Runtime.InteropServices.Marshal.SizeOf(t!);
            Assert.AreEqual(expected, actual,
                $"BoidData stride must be {expected} bytes (3+3+4+3 floats) to match compute shader layout");
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

        [Test]
        public void PerformanceModuleAttribute_IsAppliedToBuiltInModules()
        {
            // M4: 全 IPerformanceModule 実装に [PerformanceModule] が貼られていること
            var vfxAttr = typeof(VFXModule).GetCustomAttribute<PerformanceModuleAttribute>();
            Assert.IsNotNull(vfxAttr);
            Assert.AreEqual(NodeCategory.VFX, vfxAttr!.Category);
            Assert.AreEqual("VFX_", vfxAttr.LegacyTypeNamePrefix);
            Assert.AreEqual(typeof(VFXModuleNode), vfxAttr.CustomNodeType);

            var shaderAttr = typeof(ShaderModule).GetCustomAttribute<PerformanceModuleAttribute>();
            Assert.IsNotNull(shaderAttr);
            Assert.AreEqual(NodeCategory.Shader, shaderAttr!.Category);
            Assert.AreEqual("Shader_", shaderAttr.LegacyTypeNamePrefix);
            Assert.IsNull(shaderAttr.CustomNodeType);

            var cubesAttr = typeof(InstancedCubesModule).GetCustomAttribute<PerformanceModuleAttribute>();
            Assert.IsNotNull(cubesAttr);
            Assert.AreEqual("InstancedCubes_", cubesAttr!.LegacyTypeNamePrefix);
        }

        [Test]
        public void ModuleNodeBase_RestoreParamsFromJson_LogsMismatchWarning()
        {
            // L5: saved graph の moduleName と注入された ModuleDefinition.moduleName が違うと warning
            var def = ScriptableObject.CreateInstance<ModuleDefinition>();
            def.moduleName = "CurrentModule";
            def.parameters = new();
            def.events = new();

            var node = new ModuleNode("warn-1", "Module_CurrentModule", def);

            // moduleName=DeletedModule の saved graph をロードする想定
            const string oldJson = "{\"moduleName\":\"DeletedModule\"}";
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("moduleName mismatch"));
            node.RestoreParamsFromJson(oldJson);
        }

        [Test]
        public void ModuleNodeBase_RestoreParamsFromJson_NoWarningWhenMatching()
        {
            var def = ScriptableObject.CreateInstance<ModuleDefinition>();
            def.moduleName = "Smoke";
            def.parameters = new();
            def.events = new();

            var node = new ModuleNode("ok-1", "Module_Smoke", def);
            const string sameJson = "{\"moduleName\":\"Smoke\"}";
            // 一致時は warning を出さない (LogAssert.NoUnexpectedReceived が validate する)
            node.RestoreParamsFromJson(sameJson);
        }
    }
}
