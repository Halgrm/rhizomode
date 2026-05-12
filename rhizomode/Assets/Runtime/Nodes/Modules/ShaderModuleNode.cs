#nullable enable

using Rhizomode.Core;

namespace Rhizomode.Nodes.Modules
{
    /// <summary>
    /// Shaderモジュールをラップするノード。ModuleDefinitionから入力ポートを動的生成する。
    /// </summary>
    public class ShaderModuleNode : ModuleNodeBase
    {
        /// <param name="id">ノードID</param>
        /// <param name="definition">Shaderモジュールのパラメータ定義</param>
        public ShaderModuleNode(string id, ModuleDefinition definition)
            : base(id, $"Shader_{definition.moduleName}", definition)
        {
        }
    }
}
