#nullable enable

namespace Rhizomode.Graph.Mutation
{
    /// <summary>
    /// Phase 1A placeholder for Rhizomode.Graph.Mutation asmdef (Plan v5.3).
    /// Phase 2 で以下を配置:
    ///   CommandOrigin enum (v5.1 で SharedKernel から移動)
    ///   IGraphCommand record 7 種 (CommandOrigin Origin + GraphSnapshot Undo)
    ///   GraphCommandDispatcher
    ///   MainThreadGraphCommandQueue (public Tick())
    ///   HistoryConfig (SO)
    ///   CommandAuditLog (origin 別の発行統計 + dispatch trace)
    ///
    /// 制約: Graph.Serialization 参照禁止 (Undo は Snapshot 経由、JSON 形式から独立)
    /// </summary>
    internal static class GraphMutationAssemblyMarker
    {
    }
}
