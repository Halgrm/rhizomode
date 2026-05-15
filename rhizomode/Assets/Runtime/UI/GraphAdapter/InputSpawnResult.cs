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
    /// (各 position) を一緒に運ぶ DTO。<c>Rhizomode.Bootstrap.NodeSpawnService</c> が生成し、
    /// <see cref="MenuNodeSpawnCoordinator"/> が visual を creates。
    ///
    /// Plan v5.4 §15 F-Vf-a.1 Phase A: 旧 NodeSpawnService.cs 内 record を UI.GraphAdapter へ
    /// 持ち上げ、MenuNodeSpawnCoordinator (UI.GraphAdapter) と NodeSpawnService (Bootstrap) を
    /// 同一 DTO 経由で結ぶことで MenuNodeSpawnCoordinator の NodeSpawnService 直接依存を解消する。
    /// </remarks>
    public sealed record InputSpawnResult(
        NodeBase Source,
        Vector3 SourcePosition,
        ParamType PortType,
        Edge? PrimaryEdge,
        NodeBase? TriggerNode,
        Vector3 TriggerPosition,
        Edge? TriggerEdge);
}
