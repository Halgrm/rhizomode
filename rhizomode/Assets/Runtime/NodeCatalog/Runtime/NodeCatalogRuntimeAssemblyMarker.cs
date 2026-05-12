#nullable enable

namespace Rhizomode.NodeCatalog.Runtime
{
    /// <summary>
    /// Phase 1B placeholder for Rhizomode.NodeCatalog.Runtime asmdef (Plan v5.3).
    ///
    /// Phase 1B 後の配置:
    ///   NodeTypeRegistry (UI から移送)
    ///
    /// Phase 4 で以下を追加:
    ///   NodeTypeRegistration (display + factory + runtimeType)
    ///   NodeTypeAttributeScanner (6 Nodes asmdef 横断)
    ///   NodeTypeDisplayOverridesAsset (SO)
    ///   NodeCategoryDefaults (SO)
    ///   AttributeScannerNodeFactory : INodeFactory
    ///   NodeTypeAliasRegistry : INodeTypeAliasResolver
    ///   PortAliasRegistry : IPortAliasResolver
    ///   NodeCatalogCache (SO)
    /// </summary>
    internal static class NodeCatalogRuntimeAssemblyMarker
    {
    }
}
