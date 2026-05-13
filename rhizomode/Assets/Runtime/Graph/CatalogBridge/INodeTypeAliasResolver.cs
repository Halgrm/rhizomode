#nullable enable

namespace Rhizomode.Graph.CatalogBridge
{
    /// <summary>
    /// 旧ノードタイプ名から現行 typeName への alias 解決 contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 (v4.9 で移送): JSON ロード時に古い保存形式の typeName を現行に解決する。
    /// Phase 4 で NodeCatalog.Runtime の <c>NodeTypeAliasRegistry</c> (SO) が実装。
    ///
    /// 当初 Graph.Serialization に置いていたが、「カタログ名解決」は JSON 形式に閉じない概念のため
    /// Graph.CatalogBridge に移送 (Plan v4.9-1)。
    /// </remarks>
    public interface INodeTypeAliasResolver
    {
        /// <summary>old typeName を current typeName に解決する。alias が無ければそのまま返す。</summary>
        string ResolveTypeName(string oldTypeName);
    }
}
