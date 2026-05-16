#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.SharedKernel;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// ScrollMenu spawn 後の自動入力ノード (Const/Toggle/Trigger) 1 件分の結果データ。
    /// </summary>
    /// <remarks>
    /// graph mutation 結果 (Source / PrimaryEdge / TriggerNode / TriggerEdge) と visual 配置情報
    /// (各 position) を一緒に運ぶ DTO。<c>Rhizomode.Interaction.GraphAdapter.NodeSpawnService</c> が生成し、
    /// <see cref="MenuNodeSpawnCoordinator"/> が visual を creates。
    ///
    /// Plan v5.4 §15 F-Vf-a.1 Phase A: 旧 NodeSpawnService.cs 内 record を UI.GraphAdapter へ持ち上げ、
    /// MenuNodeSpawnCoordinator (UI.GraphAdapter) と NodeSpawnService (Interaction.GraphAdapter、Phase D
    /// で移送、F-Vf-d.2 で .GraphAdapter へ再移送) を同一 DTO 経由で結ぶ。
    ///
    /// F-Vf-d.2 (Codex review #1 EDGE_IDENTITY): 旧 <c>Edge?</c> 直接参照を <see cref="SpawnedEdgeInfo"/>
    /// (id + endpoints) に置換し、Snapshot 復元後の stale Edge reference リスクを排除した。
    /// </remarks>
    public sealed record InputSpawnResult(
        NodeBase Source,
        Vector3 SourcePosition,
        ParamType PortType,
        SpawnedEdgeInfo? PrimaryEdge,
        NodeBase? TriggerNode,
        Vector3 TriggerPosition,
        SpawnedEdgeInfo? TriggerEdge);
}
