#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Interaction
{
    /// <summary>
    /// ScrollMenu 選択 + Const/Toggle/Trigger 自動 spawn の graph mutation ロジックを集約する service。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §13 (F-Vf-d.1 完了): 全 graph mutation は <see cref="GraphCommandDispatcher"/> 経由で実行される。
    /// 旧実装は <c>NodeRuntime.RegisterNode</c> + <c>AddEdge</c> を直接呼び <see cref="CommandAuditLog"/> を bypass
    /// していたが、本 service は <see cref="AddNodeCommand"/> + <see cref="ConnectPortsCommand"/> を Dispatcher
    /// 経由で発行する。Undo/Redo + audit log と統合される。
    ///
    /// 具体ノード型 (ConstFloatNode / ConstColorNode / ToggleNode / TriggerNode / ModuleNodeBase) には依存せず、
    /// 以下の抽象 API のみ使用する:
    ///   - <see cref="ParamTypeNodeMap.GetSourceTypeName"/> で typeName 解決
    ///   - <see cref="NodeBase.IsInputPortEvent"/> で event ポート判定
    ///   - <see cref="NodeBase.PrimeInitialEmission"/> で接続直後の初期値再発行
    ///
    /// graph mutation 部 (本 service) と visual 創出 (<c>Rhizomode.UI.MenuNodeSpawnCoordinator</c>) は
    /// <c>InputSpawnResult</c> DTO を介して疎結合 — service は "data 層"、coordinator は "UI 反映層"。
    /// </remarks>
    public sealed class NodeSpawnService
    {
        private const float DefaultMenuSpawnOffset = 0.3f;
        private const float InputNodeHorizontalOffset = 0.35f;
        private const float InputNodeVerticalSpacing = 0.18f;
        private const float TriggerNodeHorizontalOffset = 0.3f;

        private const string ToggleTypeName = "Toggle";
        private const string TriggerTypeName = "Trigger";

        private readonly GraphState _graphState;
        private readonly GraphCommandDispatcher _dispatcher;

        public NodeSpawnService(GraphState graphState, GraphCommandDispatcher dispatcher)
        {
            _graphState = graphState;
            _dispatcher = dispatcher;
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
            var spawnPos = headPosition + headForward * DefaultMenuSpawnOffset;
            var node = DispatchAddNode(typeName, spawnPos);
            if (node == null)
            {
                Debug.LogWarning($"[NodeSpawnService] Failed to create node '{typeName}'");
                return null;
            }
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

            var portCount = 0;
            foreach (var kvp in inputPorts)
            {
                if (targetNode.IsInputPortEvent(kvp.Key)) continue;
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

                if (targetNode.IsInputPortEvent(portName)) continue;

                var sourceTypeName = ParamTypeNodeMap.GetSourceTypeName(portType);
                if (sourceTypeName == null) continue;

                var pos = new Vector3(
                    startOffset.x,
                    topY - slotIndex * InputNodeVerticalSpacing,
                    startOffset.z);

                var sourceNode = DispatchAddNode(sourceTypeName, pos);
                if (sourceNode == null) continue;

                var outputPort = sourceTypeName == ToggleTypeName ? "State" : "Value";
                var edgeId = Guid.NewGuid().ToString();
                _dispatcher.Execute(new ConnectPortsCommand(
                    CommandOrigin.Interaction, edgeId,
                    sourceNode.Id, outputPort, targetNode.Id, portName));
                var connectedEdge = FindEdgeById(edgeId);

                // Const の初期値を再発行 (R3 Subject はリプレイしないため、接続後の最初の emission が必要)
                if (connectedEdge != null) sourceNode.PrimeInitialEmission();

                NodeBase? triggerNode = null;
                Edge? triggerEdge = null;
                Vector3 triggerPos = default;
                if (sourceTypeName == ToggleTypeName)
                {
                    triggerPos = pos - nodeRight * TriggerNodeHorizontalOffset;
                    triggerNode = DispatchAddNode(TriggerTypeName, triggerPos);
                    if (triggerNode != null)
                    {
                        var triggerEdgeId = Guid.NewGuid().ToString();
                        _dispatcher.Execute(new ConnectPortsCommand(
                            CommandOrigin.Interaction, triggerEdgeId,
                            triggerNode.Id, "Trigger", sourceNode.Id, "Trigger"));
                        triggerEdge = FindEdgeById(triggerEdgeId);
                    }
                }

                results.Add(new InputSpawnResult(
                    sourceNode, pos, portType, connectedEdge,
                    triggerNode, triggerPos, triggerEdge));

                slotIndex++;
            }

            return results;
        }

        /// <summary>
        /// AddNodeCommand を dispatch し、適用後の <see cref="NodeBase"/> インスタンスを GraphState から取得する。
        /// </summary>
        private NodeBase? DispatchAddNode(string typeName, Vector3 position)
        {
            var nodeId = Guid.NewGuid().ToString();
            _dispatcher.Execute(new AddNodeCommand(
                CommandOrigin.Interaction,
                nodeId,
                typeName,
                new RzVector3(position.x, position.y, position.z)));
            return _graphState.Nodes.TryGetValue(nodeId, out var node) ? node : null;
        }

        /// <summary>新規追加された edge を id 一致で _state.Edges から取得する。</summary>
        private Edge? FindEdgeById(string edgeId)
        {
            var edges = _graphState.Edges;
            for (var i = edges.Count - 1; i >= 0; i--)
            {
                if (edges[i].Id == edgeId) return edges[i];
            }
            return null;
        }
    }

    /// <summary>ScrollMenu spawn 結果 (visual 創出は coordinator が担当)。</summary>
    public sealed record SpawnResult(NodeBase Node, Vector3 Position);
}
