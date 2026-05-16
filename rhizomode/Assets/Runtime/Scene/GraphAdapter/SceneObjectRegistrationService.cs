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

namespace Rhizomode.Scene.GraphAdapter
{
    /// <summary>
    /// シーン上の <c>SceneObjectBridge</c> (Rhizomode.UI) を検出して SceneObjectNode を auto-spawn する service。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 F-Vf-a.1 Phase C: 旧 Rhizomode.Bootstrap.SceneObjectRegistrationService を
    /// Scene.GraphAdapter asmdef へ移送。Scene.GraphAdapter の asmdef refs に
    /// NodeCatalog.Contracts/Runtime + Nodes.Scene + UI.Presentation を追加することで本来の所属層へ集約した。
    ///
    /// graph mutation 部 (type 登録 + 各 bridge に対する node 生成 + RegisterNode) を担当。
    /// visual 創出 + orientation は <c>SceneObjectsBootstrapWiring</c> (Bootstrap.Wiring) が担当。
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
        /// <remarks>
        /// N1 fix (2026-05-16): paramsJson を受け取る factory overload を使い、保存時の objectName /
        /// expose flags を正しく復元する。target Transform は bridge 再連結で設定されるため null のまま OK。
        /// </remarks>
        public void RegisterTypeAndFactory()
        {
            _typeRegistry.Register(new NodeTypeInfo(
                SceneObjectTypeName, "Scene Object", NodeCategory.Utility));

            _graphState.RegisterNodeFactory(SceneObjectTypeName,
                (Func<string, string, NodeBase>)((id, paramsJson) =>
                {
                    var (objectName, exposePos, exposeRot, exposeScale) = ParseSceneObjectParams(paramsJson);
                    return new SceneObjectNode(id, objectName, exposePos, exposeRot, exposeScale);
                }));
        }

        /// <summary>
        /// paramsJson から SceneObjectNode constructor 引数を取り出す。失敗時は全 expose true で fail-open。
        /// </summary>
        private static (string ObjectName, bool ExposePos, bool ExposeRot, bool ExposeScale)
            ParseSceneObjectParams(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson))
                return ("Restored", true, true, true);
            try
            {
                var p = UnityEngine.JsonUtility.FromJson<SceneObjectNode.PersistedParams>(paramsJson);
                return (
                    string.IsNullOrEmpty(p.objectName) ? "Restored" : p.objectName,
                    p.exposePosition, p.exposeRotation, p.exposeScale);
            }
            catch
            {
                return ("Restored", true, true, true);
            }
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
