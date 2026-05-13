#nullable enable

namespace Rhizomode.Graph.Snapshot
{
    /// <summary>
    /// エッジの不変な像 (canonical immutable projection)。
    /// </summary>
    /// <remarks>
    /// Phase 2 で導入。Snapshot 二重責務 (Undo + Serialization 中間表現)。
    /// </remarks>
    public sealed record EdgeSnapshot(
        string EdgeId,
        string FromNodeId,
        string FromPortName,
        string ToNodeId,
        string ToPortName);
}
