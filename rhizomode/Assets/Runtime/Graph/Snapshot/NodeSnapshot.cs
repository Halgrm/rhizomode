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
    ///
    /// <see cref="ParamValues"/> は <see cref="INodeParamAccessor"/> 経由の typed param (Undo/Redo +
    /// SetNodeParamCommand から書き込まれる) を保持する。一方 <see cref="ParamsJson"/> は
    /// <see cref="NodeBase.ToNodeData"/> の paramsJson 文字列を保持し、INodeParamAccessor 非対応の
    /// internal state (LFO 波形 enum 等) も含む完全 round-trip を可能にする (P2 fix, 2026-05-16)。
    /// 復元時は ParamsJson → ParamValues の順で適用 (typed value が末尾上書き)。
    /// </remarks>
    public sealed record NodeSnapshot(
        string NodeId,
        string TypeName,
        RzVector3 Position,
        IReadOnlyDictionary<string, ParamValue> ParamValues,
        string ParamsJson = "");
}
