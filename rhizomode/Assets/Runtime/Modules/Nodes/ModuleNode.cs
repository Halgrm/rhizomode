#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.Modules;

namespace Rhizomode.Nodes.Modules
{
    /// <summary>
    /// 汎用 module node。<see cref="ModuleDefinition"/> から param/event ポートを自動生成し、
    /// prefab 側の <see cref="IPerformanceModule"/> へ値を転送する。
    /// </summary>
    /// <remarks>
    /// 新しい module 種別 (GPU instancing、raymarch、compute particle 等) を追加する場合、
    /// 専用の <see cref="ModuleNodeBase"/> 派生クラスを書く必要はない — 本クラスをそのまま使う。
    /// 例外: <see cref="VFXModuleNode"/> のように追加ポート ("Active" 等) が必要なら派生クラスを作る。
    ///
    /// typeName は factory 側から渡される (legacy "VFX_X" / "Shader_X" / "InstancedCubes_X" alias と
    /// 新 "Module_X" 表記の両方が同じクラスで動く設計)。
    /// </remarks>
    public sealed class ModuleNode : ModuleNodeBase
    {
        /// <param name="id">ノードID</param>
        /// <param name="typeName">登録 typeName (factory key と一致させる)</param>
        /// <param name="definition">パラメータ定義 SO</param>
        public ModuleNode(string id, string typeName, ModuleDefinition definition)
            : base(id, typeName, definition)
        {
        }
    }
}
