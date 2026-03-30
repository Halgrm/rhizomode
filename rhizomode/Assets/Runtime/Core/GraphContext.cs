#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using UnityEngine;

namespace Rhizomode.Core
{
    /// <summary>
    /// ノードグラフの中核管理クラス。ノード登録・エッジ接続・信号フロー仲介・シリアライズを担う。
    /// </summary>
    public class GraphContext : IDisposable
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
        /// ノードを登録し、Setup()を呼び出す。
        /// </summary>
        public void RegisterNode(NodeBase node)
        {
            _nodes[node.Id] = node;
            try
            {
                node.Setup(this);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphContext] Node setup failed: {node.Id} ({node.NodeType}) — {e.Message}");
            }
        }

        /// <summary>
        /// ノードを削除し、関連するエッジをすべて切断する。
        /// </summary>
        public void RemoveNode(string nodeId)
        {
            if (!_nodes.TryGetValue(nodeId, out var node))
            {
                Debug.LogWarning($"[GraphContext] Node not found: {nodeId}");
                return;
            }

            var relatedEdges = _edges
                .Where(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId)
                .ToList();

            foreach (var edge in relatedEdges)
            {
                edge.Subscription?.Dispose();
                _edges.Remove(edge);
            }

            try
            {
                node.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphContext] Node dispose failed: {nodeId} — {e.Message}");
            }

            _nodes.Remove(nodeId);
        }

        /// <summary>
        /// ノード間のエッジ接続を試行する。型が不一致の場合はfalseを返す。
        /// </summary>
        /// <returns>接続成功でtrue。型不一致またはポート未発見でfalse。</returns>
        public bool TryConnect(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            try
            {
                if (!_nodes.TryGetValue(fromNodeId, out var fromNode))
                {
                    Debug.LogWarning($"[GraphContext] Source node not found: {fromNodeId}");
                    return false;
                }
                if (!_nodes.TryGetValue(toNodeId, out var toNode))
                {
                    Debug.LogWarning($"[GraphContext] Target node not found: {toNodeId}");
                    return false;
                }

                var output = fromNode.GetOutputPort(fromPort);
                var input = toNode.GetInputPort(toPort);

                if (output == null)
                {
                    Debug.LogWarning($"[GraphContext] Output port not found: {fromNodeId}.{fromPort}");
                    return false;
                }
                if (input == null)
                {
                    Debug.LogWarning($"[GraphContext] Input port not found: {toNodeId}.{toPort}");
                    return false;
                }

                if (output.Type != input.Type)
                {
                    Debug.LogWarning($"[GraphContext] Type mismatch: {output.Type} → {input.Type}");
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
                Debug.LogError($"[GraphContext] TryConnect failed — {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 指定されたエッジを切断する。
        /// </summary>
        public void Disconnect(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            var edge = _edges.Find(e =>
                e.FromNodeId == fromNodeId && e.FromPort == fromPort &&
                e.ToNodeId == toNodeId && e.ToPort == toPort);

            if (edge == null)
            {
                Debug.LogWarning($"[GraphContext] Edge not found: {fromNodeId}.{fromPort} → {toNodeId}.{toPort}");
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

            Debug.LogError($"[GraphContext] Input port '{portName}' not found or type mismatch on node '{node.Id}'");
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
                    Debug.LogWarning($"[GraphContext] Output port '{portName}' not found or type mismatch on node '{node.Id}'");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphContext] SetOutput failed: node={node.Id}, port={portName} — {e.Message}");
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
                    Debug.LogError($"[GraphContext] Serialize node failed: {node.Id} — {e.Message}");
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
            Clear();

            foreach (var nodeData in data.nodes)
            {
                if (!_nodeFactories.TryGetValue(nodeData.type, out var factory))
                {
                    Debug.LogWarning($"[GraphContext] Unknown node type: {nodeData.type}");
                    continue;
                }

                try
                {
                    var node = factory(nodeData.id);
                    node.Position = nodeData.ToVector3();
                    RegisterNode(node);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GraphContext] Deserialize node failed: {nodeData.id} ({nodeData.type}) — {e.Message}");
                }
            }

            foreach (var edgeData in data.edges)
            {
                if (!TryConnect(edgeData.from, edgeData.fromPort, edgeData.to, edgeData.toPort))
                {
                    Debug.LogWarning($"[GraphContext] Failed to restore edge: {edgeData.from}.{edgeData.fromPort} → {edgeData.to}.{edgeData.toPort}");
                }
            }
        }

        /// <summary>
        /// 全ノード・全エッジを破棄する。
        /// </summary>
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
                    Debug.LogError($"[GraphContext] Clear dispose failed: {node.Id} — {e.Message}");
                }
            }
            _nodes.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
