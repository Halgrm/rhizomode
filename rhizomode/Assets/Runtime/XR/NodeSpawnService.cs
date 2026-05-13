#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Modules;
using Rhizomode.Nodes.Modules;
using Rhizomode.Nodes.Input;
using Rhizomode.Nodes.Time;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// ScrollMenu 選択 + Const/Toggle/Trigger 自動 spawn の graph mutation ロジックを集約する service。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 8 Round C (F-8.2 抽出 1/N): 旧 GameBootstrap.OnScrollMenuNodeSelected +
    /// SpawnInputNodes の graph 操作を本 service に分離。visual 創出 (NodeVisualManager / EdgeVisualManager)
    /// は GameBootstrap が引き続き担当 — service は "data 層"、bootstrap は "UI 反映層"。
    ///
    /// 配置: 暫定 Rhizomode.XR (graphContext / NodeRuntime / Module 各 layer に access 必要)。
    /// Plan v5.3 上の正規 location は Interaction.GraphAdapter だが、UI.Presentation 依存を解消する
    /// (visual 創出を callback / event 経由にする) refactor が必要なため、本 round では XR 配置に留める。
    /// </remarks>
    public sealed class NodeSpawnService
    {
        private const float DefaultMenuSpawnOffset = 0.3f;
        private const float InputNodeHorizontalOffset = 0.35f;
        private const float InputNodeVerticalSpacing = 0.18f;
        private const float TriggerNodeHorizontalOffset = 0.3f;

        private readonly GraphState _graphState;
        private readonly NodeRuntime _nodeRuntime;

        public NodeSpawnService(GraphState graphState, NodeRuntime nodeRuntime)
        {
            _graphState = graphState;
            _nodeRuntime = nodeRuntime;
        }

        /// <summary>
        /// ScrollMenu で選択された typeName からノードを生成し、graph に登録する。
        /// </summary>
        /// <param name="typeName">ScrollMenu が emit した typeName。</param>
        /// <param name="headPosition">プレイヤー head 位置 (spawn 位置算出用)。</param>
        /// <param name="headForward">プレイヤー head 前方ベクトル。</param>
        /// <returns>spawn 結果。typeName が未登録の場合 null。</returns>
        public SpawnResult? TrySpawnFromMenu(string typeName, Vector3 headPosition, Vector3 headForward)
        {
            var node = _graphState.CreateNode(typeName);
            if (node == null)
            {
                Debug.LogWarning($"[NodeSpawnService] Failed to create node '{typeName}'");
                return null;
            }

            var spawnPos = headPosition + headForward * DefaultMenuSpawnOffset;
            node.Position = spawnPos;
            _nodeRuntime.RegisterNode(node, NodeInitMode.FreshSpawn);

            return new SpawnResult(node, spawnPos);
        }

        /// <summary>
        /// 指定 targetNode の全入力ポートに対応する Const/Toggle ノードを自動 spawn + プリコネクトする。
        /// Toggle 入力には追加で Trigger ノードを spawn して Trigger ポートに繋ぐ。
        /// </summary>
        /// <param name="targetNode">入力 spawn 対象のノード。</param>
        /// <param name="targetPosition">target ノードの world 位置 (Const/Toggle 配置の起点)。</param>
        /// <param name="headPosition">プレイヤー head 位置 (ノード方向ベクトル算出用)。</param>
        /// <returns>spawn された各 input の詳細 (visual 創出用)。</returns>
        public IReadOnlyList<InputSpawnResult> SpawnInputNodes(
            NodeBase targetNode, Vector3 targetPosition, Vector3 headPosition)
        {
            var results = new List<InputSpawnResult>();
            var inputPorts = targetNode.InputPorts;
            if (inputPorts.Count == 0) return results;

            var moduleNode = targetNode as ModuleNodeBase;
            var portCount = 0;
            foreach (var kvp in inputPorts)
            {
                if (moduleNode != null && moduleNode.Definition.IsEvent(kvp.Key)) continue;
                portCount++;
            }
            if (portCount == 0) return results;

            var nodeForward = (targetPosition - headPosition).normalized;
            var nodeRight = Vector3.Cross(Vector3.up, nodeForward).normalized;
            var startOffset = targetPosition - nodeRight * InputNodeHorizontalOffset;
            var topY = startOffset.y + (portCount - 1) * InputNodeVerticalSpacing * 0.5f;
            var slotIndex = 0;

            foreach (var kvp in inputPorts)
            {
                var portName = kvp.Key;
                var portType = kvp.Value.Type;

                if (moduleNode != null && moduleNode.Definition.IsEvent(portName)) continue;

                var sourceNode = CreateSourceNode(portType);
                if (sourceNode == null) continue;

                var pos = new Vector3(
                    startOffset.x,
                    topY - slotIndex * InputNodeVerticalSpacing,
                    startOffset.z);
                sourceNode.Position = pos;

                _nodeRuntime.RegisterNode(sourceNode, NodeInitMode.FreshSpawn);

                var outputPort = sourceNode is ToggleNode ? "State" : "Value";
                var edgeId = Guid.NewGuid().ToString();
                var connectedEdge = _nodeRuntime.AddEdge(edgeId, sourceNode.Id, outputPort, targetNode.Id, portName)
                    ? FindEdge(sourceNode.Id, outputPort, targetNode.Id, portName)
                    : null;

                // Const の初期値を再発行 (R3 Subject はリプレイしないため、接続後の最初の emission が必要)
                if (connectedEdge != null)
                {
                    if (sourceNode is ConstFloatNode constFloat) constFloat.Value = constFloat.Value;
                    else if (sourceNode is ConstColorNode constColor) constColor.Value = constColor.Value;
                }

                // Toggle には Trigger ノードを追加 spawn
                NodeBase? triggerNode = null;
                Edge? triggerEdge = null;
                Vector3 triggerPos = default;
                if (sourceNode is ToggleNode toggleNode)
                {
                    triggerNode = new TriggerNode(Guid.NewGuid().ToString());
                    triggerPos = pos - nodeRight * TriggerNodeHorizontalOffset;
                    triggerNode.Position = triggerPos;
                    _nodeRuntime.RegisterNode(triggerNode, NodeInitMode.FreshSpawn);

                    var triggerEdgeId = Guid.NewGuid().ToString();
                    triggerEdge = _nodeRuntime.AddEdge(triggerEdgeId, triggerNode.Id, "Trigger", toggleNode.Id, "Trigger")
                        ? FindEdge(triggerNode.Id, "Trigger", toggleNode.Id, "Trigger")
                        : null;
                }

                results.Add(new InputSpawnResult(
                    sourceNode, pos, portType, connectedEdge,
                    triggerNode, triggerPos, triggerEdge));

                slotIndex++;
            }

            return results;
        }

        private static NodeBase? CreateSourceNode(ParamType portType) => portType switch
        {
            ParamType.Float => new ConstFloatNode(Guid.NewGuid().ToString()),
            ParamType.Color => new ConstColorNode(Guid.NewGuid().ToString()),
            ParamType.Bool => new ToggleNode(Guid.NewGuid().ToString()),
            _ => null
        };

        /// <summary>
        /// 新規追加された edge を endpoint 一致で _state.Edges から逆引き。
        /// AddEdge が GraphState.TryConnect 経由のため、edge ID は生成側で取れない (Phase 7 で id 指定 API 化予定)。
        /// </summary>
        private Edge? FindEdge(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            var edges = _graphState.Edges;
            for (var i = edges.Count - 1; i >= 0; i--)
            {
                var e = edges[i];
                if (e.FromNodeId == fromNodeId && e.FromPort == fromPort &&
                    e.ToNodeId == toNodeId && e.ToPort == toPort)
                {
                    return e;
                }
            }
            return null;
        }
    }

    /// <summary>ScrollMenu spawn 結果 (visual 創出は GameBootstrap が担当)。</summary>
    public sealed record SpawnResult(NodeBase Node, Vector3 Position);

    /// <summary>
    /// 入力 spawn の 1 件分。Toggle 入力の場合は <see cref="TriggerNode"/> も入る。
    /// </summary>
    public sealed record InputSpawnResult(
        NodeBase Source,
        Vector3 SourcePosition,
        ParamType PortType,
        Edge? PrimaryEdge,
        NodeBase? TriggerNode,
        Vector3 TriggerPosition,
        Edge? TriggerEdge);
}
