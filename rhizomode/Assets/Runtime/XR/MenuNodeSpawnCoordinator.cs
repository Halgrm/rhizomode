#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.SharedKernel;
using Rhizomode.UI;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// ScrollMenu 選択経由でノード spawn が成立した後の visual 創出を担う coordinator。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 9 Round F2 (F-8.2 系 prereq 残)。旧 GameBootstrap.OnScrollMenuNodeSelected /
    /// SpawnInputVisuals の visual 構築 (NodeVisualManager.CreateNodeVisual + EdgeVisualManager.CreateEdgeVisual)
    /// 部分をここに集約。GameBootstrap 側は graphContext 妥当性チェック + scrollMenuInteraction.CloseMenu +
    /// Object3D Proxy bind (Module 層の関心事) のみを残す。
    ///
    /// graph mutation は <see cref="NodeSpawnService"/> が担当。本 coordinator は visual side-effects のみ。
    /// </remarks>
    public sealed class MenuNodeSpawnCoordinator
    {
        private readonly NodeVisualManager _visualManager;
        private readonly EdgeVisualManager _edgeVisualManager;
        private readonly NodeSpawnService _nodeSpawnService;

        public MenuNodeSpawnCoordinator(
            NodeVisualManager visualManager,
            EdgeVisualManager edgeVisualManager,
            NodeSpawnService nodeSpawnService)
        {
            _visualManager = visualManager;
            _edgeVisualManager = edgeVisualManager;
            _nodeSpawnService = nodeSpawnService;
        }

        /// <summary>
        /// 主ノードの visual を生成し、プレイヤー方向に rotate する。
        /// </summary>
        /// <returns>生成された visual。nodeUxml 未設定など失敗時は null。</returns>
        public NodeVisualController? CreatePrimaryVisual(NodeBase node, Vector3 position, Vector3 headPos)
        {
            var visual = _visualManager.CreateNodeVisual(new NodeViewAdapter(node), position);
            if (visual != null)
                visual.transform.rotation = Quaternion.LookRotation(position - headPos);
            return visual;
        }

        /// <summary>
        /// 主ノードの入力ポートに対する自動 spawn (Const/Toggle/Trigger) の visual 群を生成する。
        /// </summary>
        public void SpawnInputVisuals(NodeBase targetNode, Vector3 nodePos, Vector3 headPos)
        {
            var results = _nodeSpawnService.SpawnInputNodes(targetNode, nodePos, headPos);
            foreach (var r in results)
            {
                // Source ノード (Const/Toggle) の visual
                var sourceVisual = _visualManager.CreateNodeVisual(new NodeViewAdapter(r.Source), r.SourcePosition);
                if (sourceVisual != null)
                    sourceVisual.transform.rotation = Quaternion.LookRotation(r.SourcePosition - headPos);

                // Source → target 間の edge visual (接続成功時のみ)
                if (r.PrimaryEdge != null)
                {
                    var pe = r.PrimaryEdge;
                    _edgeVisualManager.CreateEdgeVisual(
                        new EdgeViewModel(pe.Id, pe.FromNodeId, pe.FromPort, pe.ToNodeId, pe.ToPort),
                        r.PortType);
                }

                // Trigger ノードがあれば visual + edge visual
                if (r.TriggerNode != null)
                {
                    var triggerVisual = _visualManager.CreateNodeVisual(new NodeViewAdapter(r.TriggerNode), r.TriggerPosition);
                    if (triggerVisual != null)
                        triggerVisual.transform.rotation = Quaternion.LookRotation(r.TriggerPosition - headPos);

                    if (r.TriggerEdge != null)
                    {
                        var te = r.TriggerEdge;
                        _edgeVisualManager.CreateEdgeVisual(
                            new EdgeViewModel(te.Id, te.FromNodeId, te.FromPort, te.ToNodeId, te.ToPort),
                            ParamType.Bool);
                    }
                }
            }
        }
    }
}
