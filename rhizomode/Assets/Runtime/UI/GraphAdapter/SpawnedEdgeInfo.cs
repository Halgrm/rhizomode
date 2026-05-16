#nullable enable

namespace Rhizomode.UI
{
    /// <summary>
    /// NodeSpawnService が ConnectPortsCommand 経由で生成した edge の id + endpoints 情報を運ぶ DTO。
    /// </summary>
    /// <remarks>
    /// F-Vf-d.2: 旧 <c>InputSpawnResult.PrimaryEdge</c> / <c>TriggerEdge</c> は <c>Edge</c> instance を直接保持
    /// していたが、Undo/Redo (Snapshot 復元) 後は Edge インスタンスが入れ替わるため、stale reference のリスクが
    /// あった (Codex review #1 EDGE_IDENTITY 指摘)。本 record は id + endpoints のみ運び、視覚生成側が必要なら
    /// id で <see cref="Rhizomode.Graph.Model.GraphState"/> から再取得できる。
    /// </remarks>
    public sealed record SpawnedEdgeInfo(
        string EdgeId,
        string FromNodeId,
        string FromPort,
        string ToNodeId,
        string ToPort);
}
