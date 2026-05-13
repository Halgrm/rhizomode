#nullable enable

using System.Collections.Generic;

namespace Rhizomode.Graph.CatalogBridge
{
    /// <summary>
    /// 利用可能なノードタイプの列挙 contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: NodeCatalog.Runtime の <c>NodeTypeRegistry</c> が実装。
    /// UI の Scroll Menu が本 interface 経由で typeName 一覧を取得する。
    /// </remarks>
    public interface INodeTypeProvider
    {
        /// <summary>登録済みの全ノードタイプ名を列挙する。</summary>
        IEnumerable<string> AllTypeNames { get; }

        /// <summary>指定された typeName が登録されているか。</summary>
        bool IsRegistered(string typeName);
    }
}
