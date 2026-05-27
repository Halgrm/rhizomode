#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.SharedKernel;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Interaction.GraphAdapter
{
    /// <summary>
    /// ScrollMenu 選択 + Const/Toggle/Trigger 自動 spawn の graph mutation 翻訳 adapter。
    /// </summary>
    /// <remarks>
    /// F-Vf-d.2: F-Vf-d.1 で Rhizomode.Interaction 配下に置いた本 service を Rhizomode.Interaction.GraphAdapter
    /// へ移送 (Codex review #4 COMMANDORIGIN_INVARIANT 解消)。
    /// <see cref="CommandOrigin.Interaction"/> を発行する layer は GraphAdapter のみという CI invariant
    /// に整合させた (feedback_command_origin 参照)。
    ///
    /// 全 graph mutation は <see cref="GraphCommandDispatcher.BeginScope"/> 経由で atomic に実行する
    /// (Codex review #3 NON_ATOMIC_MULTI_DISPATCH 解消)。<see cref="GraphCommandScope.TryExecute"/> が
    /// 1 件でも失敗すれば scope 全体が entry snapshot に rollback され、孤児ノードが残らない。
    ///
    /// ParamType → source ノード解決は <see cref="ParamTypeNodeMap.GetSourceDescriptor"/> 経由で
    /// (Codex review #5 TYPENAME_DOUBLE_DEFINITION 解消)、本 service は具体 typeName 文字列 ("Toggle" 等)
    /// を一切 知らない。
    ///
    /// visual 創出 (<c>Rhizomode.UI.MenuNodeSpawnCoordinator</c>) は <see cref="InputSpawnResult"/> DTO を
    /// 介して疎結合。本 service は "graph mutation 層"、coordinator は "UI 反映層"。
    /// </remarks>
    public sealed class NodeSpawnService
    {
        private const float DefaultMenuSpawnOffset = 0.3f;
        private const float InputNodeHorizontalOffset = 0.35f;
        private const float InputNodeVerticalSpacing = 0.18f;
        private const float TriggerNodeHorizontalOffset = 0.3f;

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
            var nodeId = Guid.NewGuid().ToString();

            using var scope = _dispatcher.BeginScope();
            if (!scope.TryExecute(new AddNodeCommand(
                    CommandOrigin.Interaction, nodeId, typeName, ToRz(spawnPos))))
            {
                Debug.LogWarning($"[NodeSpawnService] Failed to create node '{typeName}'");
                return null;
            }
            scope.Commit();

            if (!_graphState.Nodes.TryGetValue(nodeId, out var node)) return null;
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
                if (!targetNode.ShouldAutoSpawnInputSource(kvp.Key)) continue;
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
                if (!targetNode.ShouldAutoSpawnInputSource(portName)) continue;

                var descriptor = ParamTypeNodeMap.GetSourceDescriptor(portType);
                if (descriptor == null) continue;

                var pos = new Vector3(
                    startOffset.x,
                    topY - slotIndex * InputNodeVerticalSpacing,
                    startOffset.z);

                var spawned = TrySpawnSourceAtomic(descriptor, portType, pos, targetNode.Id, portName, nodeRight);
                if (spawned != null) results.Add(spawned);

                slotIndex++;
            }

            return results;
        }

        /// <summary>
        /// 1 入力ポート分の AddNode + ConnectPorts + (Toggle 時) AddNode + ConnectPorts を atomic 単位で発行。
        /// </summary>
        private InputSpawnResult? TrySpawnSourceAtomic(
            SourceNodeDescriptor descriptor,
            ParamType portType,
            Vector3 sourcePos,
            string targetNodeId,
            string targetPortName,
            Vector3 nodeRight)
        {
            var sourceNodeId = Guid.NewGuid().ToString();
            var edgeId = Guid.NewGuid().ToString();

            var hasTrigger = descriptor.TriggerSourceTypeName != null;
            var triggerNodeId = hasTrigger ? Guid.NewGuid().ToString() : null;
            var triggerEdgeId = hasTrigger ? Guid.NewGuid().ToString() : null;
            var triggerPos = hasTrigger
                ? sourcePos - nodeRight * TriggerNodeHorizontalOffset
                : Vector3.zero;

            using var scope = _dispatcher.BeginScope();

            if (!scope.TryExecute(new AddNodeCommand(
                    CommandOrigin.Interaction, sourceNodeId, descriptor.TypeName, ToRz(sourcePos))))
                return null;

            if (!scope.TryExecute(new ConnectPortsCommand(
                    CommandOrigin.Interaction, edgeId,
                    sourceNodeId, descriptor.OutputPortName,
                    targetNodeId, targetPortName)))
                return null;

            if (hasTrigger)
            {
                if (!scope.TryExecute(new AddNodeCommand(
                        CommandOrigin.Interaction,
                        triggerNodeId!, descriptor.TriggerSourceTypeName!,
                        ToRz(triggerPos))))
                    return null;

                if (!scope.TryExecute(new ConnectPortsCommand(
                        CommandOrigin.Interaction,
                        triggerEdgeId!,
                        triggerNodeId!, descriptor.TriggerOutputPortName!,
                        sourceNodeId, descriptor.TriggerTargetPortName!)))
                    return null;
            }

            scope.Commit();

            if (!_graphState.Nodes.TryGetValue(sourceNodeId, out var sourceNode)) return null;
            NodeBase? triggerNode = null;
            if (hasTrigger && !_graphState.Nodes.TryGetValue(triggerNodeId!, out triggerNode)) return null;

            // Const の初期値を再発行 (R3 Subject はリプレイしないため、接続後の最初の emission が必要)
            sourceNode.PrimeInitialEmission();

            var primaryEdge = new SpawnedEdgeInfo(
                edgeId, sourceNodeId, descriptor.OutputPortName, targetNodeId, targetPortName);
            SpawnedEdgeInfo? triggerEdge = hasTrigger
                ? new SpawnedEdgeInfo(
                    triggerEdgeId!, triggerNodeId!, descriptor.TriggerOutputPortName!,
                    sourceNodeId, descriptor.TriggerTargetPortName!)
                : null;

            return new InputSpawnResult(
                sourceNode, sourcePos, portType,
                primaryEdge, triggerNode, triggerPos, triggerEdge);
        }

        private static RzVector3 ToRz(Vector3 v) => new RzVector3(v.x, v.y, v.z);
    }

    /// <summary>ScrollMenu spawn 結果 (visual 創出は coordinator が担当)。</summary>
    public sealed record SpawnResult(NodeBase Node, Vector3 Position);
}
