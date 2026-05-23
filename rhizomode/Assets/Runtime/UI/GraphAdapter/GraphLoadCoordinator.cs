#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.Input.Contracts;
using Rhizomode.SharedKernel;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// グラフロード完了時のビジュアル再構築を担う coordinator。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 F-Vf-a.1 Phase A: 旧 Rhizomode.Bootstrap.GraphLoadCoordinator を UI.GraphAdapter asmdef へ
    /// 移送 (NodeVisualManager / EdgeVisualManager は Rhizomode.UI asmdef、NodeViewAdapter は UI.GraphAdapter
    /// asmdef — UI.GraphAdapter は両方を参照済のため adapter 層として自然な所属先)。
    ///
    /// cue 表裏 fix (2026-05-23): <see cref="Rebuild"/> に <c>savedRotations</c> 引数を追加。
    /// 保存された rotation がある node はそれを復元し、無い node (旧形式 cue) のみ
    /// LookRotation fallback に流す。
    /// </remarks>
    public sealed class GraphLoadCoordinator
    {
        private const float HeadSingularityEpsilonSqr = 1e-6f;

        private readonly NodeVisualManager _visualManager;
        private readonly EdgeVisualManager _edgeVisualManager;

        public GraphLoadCoordinator(NodeVisualManager visualManager, EdgeVisualManager edgeVisualManager)
        {
            _visualManager = visualManager;
            _edgeVisualManager = edgeVisualManager;
        }

        /// <summary>
        /// ロード後のグラフ状態から visual を全再構築する。
        /// </summary>
        /// <param name="state">ロード後の GraphState (caller が graphContext.Context 等で取得)。</param>
        /// <param name="controllerInput">プレイヤー視点。null ならノード回転をスキップ (LookRotation fallback のみ)。</param>
        /// <param name="savedRotations">
        /// id → quaternion の保存済み rotation map。null または node id 未登録なら LookRotation fallback。
        /// </param>
        public void Rebuild(
            GraphState state,
            IControllerInput? controllerInput,
            IReadOnlyDictionary<string, Quaternion>? savedRotations = null)
        {
            RebuildNodeVisuals(state);
            RebuildEdgeVisuals(state);
            ApplyNodeRotations(state, controllerInput, savedRotations);
        }

        private void RebuildNodeVisuals(GraphState state)
        {
            var views = new List<INodeView>(state.Nodes.Count);
            foreach (var node in state.Nodes.Values)
                views.Add(new NodeViewAdapter(node));
            _visualManager.RebuildAllVisuals(views);
        }

        private void RebuildEdgeVisuals(GraphState state)
        {
            var edgePairs = new List<(EdgeViewModel edge, ParamType portType)>(state.Edges.Count);
            foreach (var edge in state.Edges)
            {
                var fromNode = state.Nodes.TryGetValue(edge.FromNodeId, out var n) ? n : null;
                var portType = fromNode?.GetOutputPort(edge.FromPort)?.Type ?? ParamType.Float;
                edgePairs.Add((new EdgeViewModel(edge.Id, edge.FromNodeId, edge.FromPort, edge.ToNodeId, edge.ToPort), portType));
            }
            _edgeVisualManager.RebuildAllEdgeVisuals(edgePairs);
        }

        private void ApplyNodeRotations(
            GraphState state,
            IControllerInput? controllerInput,
            IReadOnlyDictionary<string, Quaternion>? savedRotations)
        {
            var hasHead = controllerInput != null;
            var headPos = controllerInput?.HeadPosition ?? Vector3.zero;
            var headForward = controllerInput?.HeadForward ?? Vector3.forward;

            foreach (var node in state.Nodes.Values)
            {
                var visual = _visualManager.GetVisual(node.Id);
                if (visual == null) continue;

                if (savedRotations != null && savedRotations.TryGetValue(node.Id, out var saved))
                {
                    visual.transform.rotation = saved;
                    continue;
                }

                if (!hasHead) continue;

                var pos = visual.transform.position;
                var diff = pos - headPos;
                // 特異点: head が node 位置と重なると LookRotation(0) → identity に潰れて
                // プレイヤー側を向かない。headForward (プレイヤーの視線方向) を fallback に使う。
                var forward = diff.sqrMagnitude < HeadSingularityEpsilonSqr ? headForward : diff;
                visual.transform.rotation = Quaternion.LookRotation(forward);
            }
        }
    }
}
