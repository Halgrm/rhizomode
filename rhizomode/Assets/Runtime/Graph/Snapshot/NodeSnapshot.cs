#nullable enable

using System.Collections.Generic;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Snapshot
{
    /// <summary>
    /// ノードの不変な像 (canonical immutable projection)。
    /// </summary>
    /// <remarks>
    /// Phase 2 で導入。二重責務 (Plan v5.0 明文化):
    /// (1) Undo runtime DTO ([Graph.Mutation] が Pre/Post state を Snapshot で保持)
    /// (2) Serialization 中間表現 ([Graph.Serialization] が GraphState ↔ Snapshot ↔ GraphData 変換)
    /// シリアライズ可能性は問わない (pure C# record)。JSON 形式とは独立。
    /// </remarks>
    public sealed record NodeSnapshot(
        string NodeId,
        string TypeName,
        RzVector3 Position,
        IReadOnlyDictionary<string, ParamValue> ParamValues);
}
