#nullable enable

namespace Rhizomode.Graph.Snapshot
{
    /// <summary>
    /// Phase 1A placeholder for Rhizomode.Graph.Snapshot asmdef (Plan v5.3).
    ///
    /// 二重責務 (asmdef README に明記、Phase 2 で実装):
    ///   (1) Undo runtime DTO: Command が Pre/Post state を保持
    ///   (2) Serialization 中間表現: GraphState ↔ Snapshot ↔ GraphData
    ///
    /// Phase 2 配置予定:
    ///   NodeSnapshot (record: NodeId, TypeName, RzVector3 Position, IReadOnlyDictionary&lt;string, ParamValue&gt;)
    ///   EdgeSnapshot (record: EdgeId, FromNode/Port, ToNode/Port)
    ///   GraphSnapshot (record: IReadOnlyList&lt;NodeSnapshot&gt;, IReadOnlyList&lt;EdgeSnapshot&gt;)
    ///
    /// シリアライズ可能性に依存しない pure C# record。
    /// </summary>
    internal static class GraphSnapshotAssemblyMarker
    {
    }
}
