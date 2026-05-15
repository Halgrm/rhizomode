#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.Input.Contracts;
using Rhizomode.SharedKernel;
using Rhizomode.UI;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// グラフロード完了時のビジュアル再構築を担う coordinator。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 V-final (Vf-a): 旧 Rhizomode.XR.GraphLoadCoordinator を Bootstrap asmdef へ verbatim 移送。
    /// Plan v5.4 §15 「Bootstrap は業務ロジック禁止」に対する transitional 違反 (F-Vf-a.1) —
    /// 本来 UI.GraphAdapter へ置くべきだが NodeVisualManager / EdgeVisualManager (Rhizomode.UI asmdef)
    /// + NodeViewAdapter (UI.GraphAdapter) の両方を要するため Bootstrap に集約。
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
        /// <param name="controllerInput">プレイヤー視点。null ならノード回転をスキップ。</param>
        public void Rebuild(GraphState state, IControllerInput? controllerInput)
        {
            RebuildNodeVisuals(state);
            RebuildEdgeVisuals(state);
            RotateNodesToHead(state, controllerInput);
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

        private void RotateNodesToHead(GraphState state, IControllerInput? controllerInput)
        {
            if (controllerInput == null) return;

            var headPos = controllerInput.HeadPosition;
            foreach (var node in state.Nodes.Values)
            {
                var visual = _visualManager.GetVisual(node.Id);
                if (visual == null) continue;
                var pos = visual.transform.position;
                visual.transform.rotation = Quaternion.LookRotation(pos - headPos);
            }
        }
    }
}
