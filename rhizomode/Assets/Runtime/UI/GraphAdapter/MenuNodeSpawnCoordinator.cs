#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.SharedKernel;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// ScrollMenu 選択経由でノード spawn が成立した後の visual 創出を担う coordinator。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 F-Vf-a.1 Phase A: 旧 Rhizomode.Bootstrap.MenuNodeSpawnCoordinator を UI.GraphAdapter
    /// asmdef へ移送。同時に NodeSpawnService への直接依存を解消し、graph mutation 結果
    /// (<see cref="InputSpawnResult"/> リスト) を caller から受け取る形に変更
    /// — 本 coordinator は純粋な visual side-effects のみを担当する。
    ///
    /// graph mutation は <c>Rhizomode.Interaction.NodeSpawnService</c> (F-Vf-a.1 Phase D 移送済) が担当。
    /// </remarks>
    public sealed class MenuNodeSpawnCoordinator
    {
        private readonly NodeVisualManager _visualManager;
        private readonly EdgeVisualManager _edgeVisualManager;

        public MenuNodeSpawnCoordinator(
            NodeVisualManager visualManager,
            EdgeVisualManager edgeVisualManager)
        {
            _visualManager = visualManager;
            _edgeVisualManager = edgeVisualManager;
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
        /// <param name="results">caller が NodeSpawnService.SpawnInputNodes で得た mutation 結果。</param>
        /// <param name="headPos">プレイヤー head 位置 (visual rotation 計算用)。</param>
        public void SpawnInputVisuals(IReadOnlyList<InputSpawnResult> results, Vector3 headPos)
        {
            foreach (var r in results)
            {
                var sourceVisual = _visualManager.CreateNodeVisual(new NodeViewAdapter(r.Source), r.SourcePosition);
                if (sourceVisual != null)
                    sourceVisual.transform.rotation = Quaternion.LookRotation(r.SourcePosition - headPos);

                if (r.PrimaryEdge != null)
                {
                    var pe = r.PrimaryEdge;
                    _edgeVisualManager.CreateEdgeVisual(
                        new EdgeViewModel(pe.EdgeId, pe.FromNodeId, pe.FromPort, pe.ToNodeId, pe.ToPort),
                        r.PortType);
                }

                if (r.TriggerNode != null)
                {
                    var triggerVisual = _visualManager.CreateNodeVisual(new NodeViewAdapter(r.TriggerNode), r.TriggerPosition);
                    if (triggerVisual != null)
                        triggerVisual.transform.rotation = Quaternion.LookRotation(r.TriggerPosition - headPos);

                    if (r.TriggerEdge != null)
                    {
                        var te = r.TriggerEdge;
                        _edgeVisualManager.CreateEdgeVisual(
                            new EdgeViewModel(te.EdgeId, te.FromNodeId, te.FromPort, te.ToNodeId, te.ToPort),
                            ParamType.Bool);
                    }
                }
            }
        }
    }
}
