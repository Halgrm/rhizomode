#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Modules;

namespace Rhizomode.Modules
{
    /// <summary>
    /// <see cref="ModuleDefinition"/> SO のリストから VFX_/Shader_ プレフィックスの動的 typeName を
    /// <see cref="INodeFactory"/> として公開する。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 4 follow-up: GameBootstrap の RegisterVFXModules / RegisterShaderModules
    /// (動的 typeName 登録) を本 factory に置換する。
    ///
    /// typeName 規約 (NodeBase 派生クラスの ctor で固定):
    ///   "VFX_{moduleName}"     → VFXModuleNode(id, definition)
    ///   "Shader_{moduleName}"  → ShaderModuleNode(id, definition)
    ///
    /// 同一 moduleName が VFX/Shader 両方で使い回せる (異なる typeName)。
    /// </remarks>
    public sealed class ModuleDefinitionNodeFactory : INodeFactory
    {
        private const string VfxPrefix = "VFX_";
        private const string ShaderPrefix = "Shader_";

        private readonly Dictionary<string, ModuleDefinition> _byModuleName;

        public ModuleDefinitionNodeFactory(IEnumerable<ModuleDefinition> definitions)
        {
            _byModuleName = new Dictionary<string, ModuleDefinition>();
            foreach (var def in definitions)
            {
                if (def == null) continue;
                if (string.IsNullOrEmpty(def.moduleName)) continue;
                _byModuleName[def.moduleName] = def;
            }
        }

        public bool CanCreate(string typeName)
        {
            if (TryParse(typeName, out _, out var moduleName))
            {
                return _byModuleName.ContainsKey(moduleName);
            }
            return false;
        }

        public NodeBase? Create(string typeName, string nodeId)
        {
            if (!TryParse(typeName, out var kind, out var moduleName)) return null;
            if (!_byModuleName.TryGetValue(moduleName, out var def)) return null;

            return kind switch
            {
                ModuleKind.Vfx => new VFXModuleNode(nodeId, def),
                ModuleKind.Shader => new ShaderModuleNode(nodeId, def),
                _ => null
            };
        }

        public IEnumerable<string> AllTypeNames
        {
            get
            {
                foreach (var moduleName in _byModuleName.Keys)
                {
                    yield return VfxPrefix + moduleName;
                    yield return ShaderPrefix + moduleName;
                }
            }
        }

        private static bool TryParse(string typeName, out ModuleKind kind, out string moduleName)
        {
            if (typeName.StartsWith(VfxPrefix, System.StringComparison.Ordinal))
            {
                kind = ModuleKind.Vfx;
                moduleName = typeName.Substring(VfxPrefix.Length);
                return true;
            }
            if (typeName.StartsWith(ShaderPrefix, System.StringComparison.Ordinal))
            {
                kind = ModuleKind.Shader;
                moduleName = typeName.Substring(ShaderPrefix.Length);
                return true;
            }
            kind = ModuleKind.Vfx;
            moduleName = string.Empty;
            return false;
        }

        private enum ModuleKind { Vfx, Shader }
    }
}
