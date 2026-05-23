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
    /// 「保存したときのローテーションのまま」原則 (2026-05-23, user 指示): ロード時に
    /// プレイヤー側へ自動回転 (LookRotation) する処理は廃止。保存値があればそれを適用、
    /// 無い node (旧形式 cue) は <see cref="NodeVisualManager.CreateNodeVisual"/> が初期化した
    /// identity rotation のままにする。旧 cue を player 向きに戻したい場合は再保存が必要。
    /// </remarks>
    public sealed class GraphLoadCoordinator
    {
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
        /// <param name="controllerInput">未使用 (旧 LookRotation fallback の引数)。signature 互換のため残存。</param>
        /// <param name="savedRotations">
        /// id → quaternion の保存済み rotation map。null または node id 未登録なら rotation は
        /// 触らない (identity / 既存 transform.rotation のまま)。
        /// </param>
        public void Rebuild(
            GraphState state,
            IControllerInput? controllerInput,
            IReadOnlyDictionary<string, Quaternion>? savedRotations = null)
        {
            _ = controllerInput; // 互換のため受け取るが未使用 (LookRotation fallback 廃止)
            RebuildNodeVisuals(state);
            RebuildEdgeVisuals(state);
            ApplySavedRotations(state, savedRotations);
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

        private void ApplySavedRotations(
            GraphState state,
            IReadOnlyDictionary<string, Quaternion>? savedRotations)
        {
            if (savedRotations == null || savedRotations.Count == 0) return;

            foreach (var node in state.Nodes.Values)
            {
                if (!savedRotations.TryGetValue(node.Id, out var saved)) continue;
                var visual = _visualManager.GetVisual(node.Id);
                if (visual == null) continue;
                visual.transform.rotation = saved;
            }
        }
    }
}
