#nullable enable

namespace Rhizomode.Graph.CatalogBridge
{
    /// <summary>
    /// 旧ポート名から現行 portName への alias 解決 contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3: JSON ロード時に古いポート名 (例: "Active" → "Enabled") を解決する。
    /// Phase 4 で NodeCatalog.Runtime の <c>PortAliasRegistry</c> (SO) が実装。
    /// </remarks>
    public interface IPortAliasResolver
    {
        /// <summary>typeName 配下の old portName を current portName に解決する。</summary>
        string ResolvePortName(string typeName, string oldPortName);
    }
}
