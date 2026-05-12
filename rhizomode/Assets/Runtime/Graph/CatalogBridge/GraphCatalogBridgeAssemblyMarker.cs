#nullable enable

namespace Rhizomode.Graph.CatalogBridge
{
    /// <summary>
    /// Phase 1A placeholder for Rhizomode.Graph.CatalogBridge asmdef (Plan v5.3).
    /// Phase 2 で INodeFactory / INodeTypeProvider /
    /// INodeTypeAliasResolver / IPortAliasResolver を配置。
    /// alias resolver は本層に置き、Serialization は depend するのみ (rename migration の余地)。
    /// </summary>
    internal static class GraphCatalogBridgeAssemblyMarker
    {
    }
}
