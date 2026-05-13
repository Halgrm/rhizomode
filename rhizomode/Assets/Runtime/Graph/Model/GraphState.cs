#nullable enable

using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

using Rhizomode.SharedKernel;
using Rhizomode.Graph.Serialization;

namespace Rhizomode.Graph.Model
{
    /// <summary>
    /// ノードグラフの中核管理クラス。ノード登録・エッジ接続・信号フロー仲介・シリアライズを担う。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 3: ミューテーション 5 メソッド (RegisterNode/RemoveNode/TryConnect/Disconnect/Clear)
    /// は <c>[Obsolete]</c> でマーク済。新規呼び出しは <c>IGraphCommand</c> + <c>GraphCommandDispatcher</c>
    /// 経由に書き換えること。Phase 8 で [Obsolete] を削除し internal 化する。
    /// </remarks>
#pragma warning disable CS0618 // クラス内部での自己呼び出しの warning を抑制 (Phase 3-8 transitional)
    public class GraphState : IDisposable
    {
        private readonly Dictionary<string, NodeBase> _nodes = new();
        private readonly List<Edge> _edges = new();
        private readonly Dictionary<string, Func<string, NodeBase>> _nodeFactories = new();

        public IReadOnlyDictionary<string, NodeBase> Nodes => _nodes;
        public IReadOnlyList<Edge> Edges => _edges;

        /// <summary>
        /// デシリアライズ用のノードファクトリを登録する。
        /// </summary>
        /// <param name="nodeType">ノードタイプ名（PascalCase文字列）</param>
        /// <param name="factory">ノードIDを受け取りNodeBaseインスタンスを返すファクトリ</param>
        public void RegisterNodeFactory(string nodeType, Func<string, NodeBase> factory)
        {
            _nodeFactories[nodeType] = factory;
        }

        /// <summary>
        /// ファクトリを使ってノードを生成する。登録は行わない。
        /// 未登録のタイプの場合はnullを返す。
        /// </summary>
        public NodeBase? CreateNode(string nodeType)
        {
            if (!_nodeFactories.TryGetValue(nodeType, out var factory))
            {
                Debug.LogWarning($"[GraphState] No factory for node type: {nodeType}");
                return null;
            }

            var nodeId = Guid.NewGuid().ToString();
            return factory(nodeId);
        }

        /// <summary>
        /// ノードを登録し、Setup()を呼び出す。
        /// </summary>
        [System.Obsolete("Phase 3: Use IGraphCommand + GraphCommandDispatcher.Execute(AddNodeCommand). " +
                         "Direct calls will be removed in Phase 8.")]
        public void RegisterNode(NodeBase node)
        {
            _nodes[node.Id] = node;
            try
            {
                node.Setup(this);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphState] Node setup failed: {node.Id} ({node.NodeType}) — {e.Message}");
            }
        }

        /// <summary>
        /// ノードを削除し、関連するエッジをすべて切断する。
        /// </summary>
        [System.Obsolete("Phase 3: Use IGraphCommand + GraphCommandDispatcher.Execute(RemoveNodeCommand). " +
                         "Direct calls will be removed in Phase 8.")]
        public void RemoveNode(string nodeId)
        {
            if (!_nodes.TryGetValue(nodeId, out var node))
            {
                Debug.LogWarning($"[GraphState] Node not found: {nodeId}");
                return;
            }

            // LINQ不使用（GC alloc回避）
            for (var i = _edges.Count - 1; i >= 0; i--)
            {
                var edge = _edges[i];
                if (edge.FromNodeId != nodeId && edge.ToNodeId != nodeId) continue;
                edge.Subscription?.Dispose();
                _edges.RemoveAt(i);
            }

            try
            {
                node.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphState] Node dispose failed: {nodeId} — {e.Message}");
            }

            _nodes.Remove(nodeId);
        }

        /// <summary>
        /// ノード間のエッジ接続を試行する。型が不一致の場合はfalseを返す。
        /// </summary>
        /// <returns>接続成功でtrue。型不一致またはポート未発見でfalse。</returns>
        [System.Obsolete("Phase 3: Use IGraphCommand + GraphCommandDispatcher.Execute(ConnectPortsCommand). " +
                         "Direct calls will be removed in Phase 8.")]
        public bool TryConnect(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            // 自己接続はグラフの循環を起こすため禁止
            if (fromNodeId == toNodeId)
            {
                Debug.LogWarning($"[GraphState] Self-connection not allowed: {fromNodeId}");
                return false;
            }

            // 同一エッジの重複作成を防止
            for (var i = 0; i < _edges.Count; i++)
            {
                var e = _edges[i];
                if (e.FromNodeId == fromNodeId && e.FromPort == fromPort &&
                    e.ToNodeId == toNodeId && e.ToPort == toPort)
                {
                    Debug.LogWarning($"[GraphState] Duplicate edge: {fromNodeId}.{fromPort} → {toNodeId}.{toPort}");
                    return false;
                }
            }

            try
            {
                if (!_nodes.TryGetValue(fromNodeId, out var fromNode))
                {
                    Debug.LogWarning($"[GraphState] Source node not found: {fromNodeId}");
                    return false;
                }
                if (!_nodes.TryGetValue(toNodeId, out var toNode))
                {
                    Debug.LogWarning($"[GraphState] Target node not found: {toNodeId}");
                    return false;
                }

                var output = fromNode.GetOutputPort(fromPort);
                var input = toNode.GetInputPort(toPort);

                if (output == null)
                {
                    Debug.LogWarning($"[GraphState] Output port not found: {fromNodeId}.{fromPort}");
                    return false;
                }
                if (input == null)
                {
                    Debug.LogWarning($"[GraphState] Input port not found: {toNodeId}.{toPort}");
                    return false;
                }

                if (output.Type != input.Type)
                {
                    Debug.LogWarning($"[GraphState] Type mismatch: {output.Type} → {input.Type}");
                    return false;
                }

                var edge = new Edge(
                    Guid.NewGuid().ToString(),
                    fromNodeId, fromPort,
                    toNodeId, toPort
                );
                edge.Subscription = output.Subscribe(input);
                _edges.Add(edge);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphState] TryConnect failed — {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 指定されたエッジを切断する。
        /// </summary>
        [System.Obsolete("Phase 3: Use IGraphCommand + GraphCommandDispatcher.Execute(DisconnectEdgeCommand). " +
                         "Direct calls will be removed in Phase 8.")]
        public void Disconnect(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            var edge = _edges.Find(e =>
                e.FromNodeId == fromNodeId && e.FromPort == fromPort &&
                e.ToNodeId == toNodeId && e.ToPort == toPort);

            if (edge == null)
            {
                Debug.LogWarning($"[GraphState] Edge not found: {fromNodeId}.{fromPort} → {toNodeId}.{toPort}");
                return;
            }

            edge.Subscription?.Dispose();
            _edges.Remove(edge);
        }

        /// <summary>
        /// ノードの入力ポートからObservableを取得する。Setup()内で使用。
        /// </summary>
        public Observable<T> GetInputObservable<T>(NodeBase node, string portName)
        {
            var port = node.GetInputPort(portName);
            if (port is InputPort<T> typedPort)
                return typedPort.Observable;

            Debug.LogError($"[GraphState] Input port '{portName}' not found or type mismatch on node '{node.Id}'");
            return R3.Observable.Empty<T>();
        }

        /// <summary>
        /// ノードの出力ポートに値を発行する。
        /// </summary>
        public void SetOutput<T>(NodeBase node, string portName, T value)
        {
            try
            {
                var port = node.GetOutputPort(portName);
                if (port is OutputPort<T> typedPort)
                {
                    typedPort.Emit(value);
                }
                else
                {
                    Debug.LogWarning($"[GraphState] Output port '{portName}' not found or type mismatch on node '{node.Id}'");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphState] SetOutput failed: node={node.Id}, port={portName} — {e.Message}");
            }
        }

        /// <summary>
        /// グラフ全体をシリアライズ用DTOに変換する。
        /// </summary>
        public GraphData Serialize()
        {
            var data = new GraphData();

            foreach (var node in _nodes.Values)
            {
                try
                {
                    data.nodes.Add(node.ToNodeData());
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GraphState] Serialize node failed: {node.Id} — {e.Message}");
                }
            }

            foreach (var edge in _edges)
            {
                data.edges.Add(new EdgeData
                {
                    id = edge.Id,
                    from = edge.FromNodeId,
                    fromPort = edge.FromPort,
                    to = edge.ToNodeId,
                    toPort = edge.ToPort
                });
            }

            return data;
        }

        /// <summary>
        /// DTOからグラフを復元する。既存のグラフはクリアされる。
        /// ノードファクトリが未登録の型はスキップされる。
        /// </summary>
        public void Deserialize(GraphData data)
        {
            if (!string.IsNullOrEmpty(data.version) && data.version != "1.0")
            {
                Debug.LogWarning($"[GraphState] Unknown graph version: {data.version} (expected 1.0)");
            }

            // 部分失敗時の安全性: まずバックアップし、失敗時に復元
            var backup = Serialize();

            try
            {
                Clear();

                foreach (var nodeData in data.nodes)
                {
                    if (!_nodeFactories.TryGetValue(nodeData.type, out var factory))
                    {
                        Debug.LogWarning($"[GraphState] Unknown node type: {nodeData.type}");
                        continue;
                    }

                    try
                    {
                        var node = factory(nodeData.id);
                        node.Position = nodeData.ToVector3();
                        node.RestoreParamsFromJson(nodeData.paramsJson);
                        RegisterNode(node);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[GraphState] Deserialize node failed: {nodeData.id} ({nodeData.type}) — {e.Message}");
                    }
                }

                foreach (var edgeData in data.edges)
                {
                    if (!TryConnect(edgeData.from, edgeData.fromPort, edgeData.to, edgeData.toPort))
                    {
                        Debug.LogWarning($"[GraphState] Failed to restore edge: {edgeData.from}.{edgeData.fromPort} → {edgeData.to}.{edgeData.toPort}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphState] Deserialize failed, restoring backup — {e.Message}");
                RestoreFromBackup(backup);
            }
        }

        private void RestoreFromBackup(GraphData backup)
        {
            try
            {
                Clear();
                foreach (var nodeData in backup.nodes)
                {
                    if (!_nodeFactories.TryGetValue(nodeData.type, out var factory)) continue;
                    var node = factory(nodeData.id);
                    node.Position = nodeData.ToVector3();
                    node.RestoreParamsFromJson(nodeData.paramsJson);
                    RegisterNode(node);
                }
                foreach (var edgeData in backup.edges)
                {
                    TryConnect(edgeData.from, edgeData.fromPort, edgeData.to, edgeData.toPort);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphState] Backup restore also failed — {e.Message}");
            }
        }

        /// <summary>
        /// プリセットのグラフデータを既存グラフに追加統合する。
        /// 既存ノード・エッジは保持され、プリセットのノードは新しいIDで追加される。
        /// </summary>
        /// <param name="presetGraph">プリセットのグラフデータ</param>
        /// <param name="spawnOffset">スポーン位置オフセット</param>
        /// <returns>追加されたノードIDのリスト</returns>
        public List<string> MergePreset(GraphData presetGraph, Vector3 spawnOffset)
        {
            var addedNodeIds = new List<string>();
            var idMap = new Dictionary<string, string>();

            // Phase 1: 全ノードIDをリマップ（新GUID生成）
            foreach (var nodeData in presetGraph.nodes)
            {
                idMap[nodeData.id] = Guid.NewGuid().ToString();
            }

            // Phase 2: ノードを生成・登録
            foreach (var nodeData in presetGraph.nodes)
            {
                var newId = idMap[nodeData.id];

                if (!_nodeFactories.TryGetValue(nodeData.type, out var factory))
                {
                    Debug.LogWarning($"[GraphState] MergePreset: Unknown node type '{nodeData.type}'");
                    continue;
                }

                try
                {
                    var node = factory(newId);
                    var pos = nodeData.ToVector3() + spawnOffset;
                    node.Position = pos;
                    node.RestoreParamsFromJson(nodeData.paramsJson);
                    RegisterNode(node);
                    addedNodeIds.Add(newId);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GraphState] MergePreset node failed: {nodeData.type} — {e.Message}");
                }
            }

            // Phase 3: エッジをリマップして接続
            foreach (var edgeData in presetGraph.edges)
            {
                if (!idMap.TryGetValue(edgeData.from, out var newFrom) ||
                    !idMap.TryGetValue(edgeData.to, out var newTo))
                {
                    Debug.LogWarning($"[GraphState] MergePreset: Edge references unknown node");
                    continue;
                }

                if (!TryConnect(newFrom, edgeData.fromPort, newTo, edgeData.toPort))
                {
                    Debug.LogWarning(
                        $"[GraphState] MergePreset edge failed: {edgeData.fromPort} → {edgeData.toPort}");
                }
            }

            return addedNodeIds;
        }

        /// <summary>
        /// 全ノード・全エッジを破棄する。
        /// </summary>
        [System.Obsolete("Phase 3: Use IGraphCommand + GraphCommandDispatcher.Execute(LoadGraphCommand(empty)). " +
                         "Direct calls will be removed in Phase 8.")]
        public void Clear()
        {
            foreach (var edge in _edges)
                edge.Subscription?.Dispose();
            _edges.Clear();

            foreach (var node in _nodes.Values)
            {
                try { node.Dispose(); }
                catch (Exception e)
                {
                    Debug.LogError($"[GraphState] Clear dispose failed: {node.Id} — {e.Message}");
                }
            }
            _nodes.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
#pragma warning restore CS0618
}
