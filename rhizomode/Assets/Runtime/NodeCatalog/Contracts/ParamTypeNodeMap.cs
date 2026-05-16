#nullable enable

using Rhizomode.SharedKernel;

namespace Rhizomode.NodeCatalog.Contracts
{
    /// <summary>
    /// ParamType → "Const/Toggle ノード typeName" の static mapping。
    /// </summary>
    /// <remarks>
    /// F-Vf-d.1: NodeSpawnService が auto-spawn する source ノードの typeName を解決する。
    /// Float → ConstFloat / Color → ConstColor / Bool → Toggle。
    /// Interaction asmdef は Nodes.Standard の具体型を knowing せず、typeName 文字列で AddNodeCommand を
    /// 発行する設計のため (Plan v5.4 §13)、この mapping は NodeCatalog.Contracts (最下層 contracts) に置く。
    /// </remarks>
    public static class ParamTypeNodeMap
    {
        /// <summary>
        /// 指定 <see cref="ParamType"/> の入力ポートに繋ぐ source ノード typeName を返す。
        /// </summary>
        /// <returns>typeName 文字列。サポート外の型は null。</returns>
        public static string? GetSourceTypeName(ParamType type) => type switch
        {
            ParamType.Float => "ConstFloat",
            ParamType.Color => "ConstColor",
            ParamType.Bool => "Toggle",
            _ => null
        };
    }
}
