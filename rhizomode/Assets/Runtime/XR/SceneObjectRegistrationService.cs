#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Nodes.Scene;
using Rhizomode.UI;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;

namespace Rhizomode.XR
{
    /// <summary>
    /// シーン上の <see cref="SceneObjectBridge"/> を検出して SceneObjectNode を auto-spawn する service。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 8 F-8.2 抽出 2/N: 旧 GameBootstrap.RegisterSceneObjects を本 service に分離。
    /// graph mutation 部 (type 登録 + 各 bridge に対する node 生成 + RegisterNode) を担当、
    /// visual 創出 + orientation は引き続き GameBootstrap が処理。
    /// </remarks>
    public sealed class SceneObjectRegistrationService
    {
        private const string SceneObjectTypeName = "SceneObject";

        private readonly NodeTypeRegistry _typeRegistry;
        private readonly GraphState _graphState;
        private readonly NodeRuntime _nodeRuntime;

        public SceneObjectRegistrationService(
            NodeTypeRegistry typeRegistry, GraphState graphState, NodeRuntime nodeRuntime)
        {
            _typeRegistry = typeRegistry;
            _graphState = graphState;
            _nodeRuntime = nodeRuntime;
        }

        /// <summary>
        /// SceneObject タイプを <see cref="NodeTypeRegistry"/> に登録し、復元 factory を <see cref="GraphState"/>
        /// に登録する。Bootstrap 起動時に 1 度だけ呼ぶ。
        /// </summary>
        public void RegisterTypeAndFactory()
        {
            _typeRegistry.Register(new NodeTypeInfo(
                SceneObjectTypeName, "Scene Object", NodeCategory.Utility));

            // デシリアライズ用 factory (実体 target は bridge 再連結で設定されるため、ここでは仮の引数)
            _graphState.RegisterNodeFactory(SceneObjectTypeName, id =>
                new SceneObjectNode(id, "Restored", true, true, true));
        }

        /// <summary>
        /// 渡された <see cref="SceneObjectBridge"/> 群から SceneObjectNode を生成・登録する。
        /// </summary>
        /// <returns>各 spawn 結果のリスト (visual 創出用)。</returns>
        public IReadOnlyList<SceneObjectSpawnResult> RegisterBridges(IEnumerable<SceneObjectBridge> bridges)
        {
            var results = new List<SceneObjectSpawnResult>();
            foreach (var bridge in bridges)
            {
                try
                {
                    var nodeId = Guid.NewGuid().ToString();
                    var node = new SceneObjectNode(
                        nodeId, bridge.gameObject.name,
                        bridge.ExposePosition, bridge.ExposeRotation, bridge.ExposeScale);
                    node.SetTarget(bridge.transform);
                    bridge.NodeId = nodeId;

                    // SceneObject に ISceneLoaderConsumer / IPerformanceModule は無いため processors は no-op
                    _nodeRuntime.RegisterNode(node, NodeInitMode.FreshSpawn);

                    var spawnPos = bridge.transform.position + Vector3.up * 0.3f;
                    results.Add(new SceneObjectSpawnResult(node, spawnPos));
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[SceneObjectRegistrationService] Setup failed for '{bridge.gameObject.name}': {e.Message}");
                }
            }
            return results;
        }
    }

    /// <summary>1 個の SceneObjectBridge に対する node spawn 結果 (visual は caller が生成)。</summary>
    public sealed record SceneObjectSpawnResult(NodeBase Node, Vector3 SpawnPosition);
}
