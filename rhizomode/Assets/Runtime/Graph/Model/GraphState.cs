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
    /// Plan v5.3 Phase 8: ミューテーション系メソッド (RegisterNode/RemoveNode/TryConnect/Disconnect/
    /// Clear/Deserialize/MergePreset) は全て <c>internal</c>。public API は
    /// <c>IGraphCommand</c> + <c>GraphCommandDispatcher</c> 経由のみ。
    /// 正規 consumer は <c>Graph.Mutation</c> / <c>Graph.Runtime</c> / <c>Graph.Tests</c>
    /// (transitional: XR / UI.GraphAdapter / Interaction — 各 caller を Phase 8 で migrate)。
    /// 詳細は <see cref="InternalsVisibleTo"/> 宣言 (Assets/Runtime/Graph/Model/InternalsVisibleTo.cs)。
    /// </remarks>
    public class GraphState : IDisposable
    {
        private readonly Dictionary<string, NodeBase> _nodes = new();
        private readonly List<Edge> _edges = new();
        private readonly Dictionary<string, Func<string, NodeBase>> _nodeFactories = new();
        // N1 fix (2026-05-16): constructor 依存ノード (SceneObjectNode 等) 用に paramsJson を
        // 受け取るファクトリも別系統で登録できる。Deserialize/MergePreset で paramsJson が
        // ある場合に優先して使われる。
        private readonly Dictionary<string, Func<string, string, NodeBase>> _nodeFactoriesWithParams = new();

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
        /// constructor で paramsJson を必要とするノード (SceneObjectNode 等) 用のファクトリを登録する。
        /// </summary>
        /// <remarks>
        /// N1 fix: 旧 <see cref="RegisterNodeFactory(string, Func{string, NodeBase})"/> は (id) のみを
        /// 渡すため、constructor 引数で port 構成が決まるノードは Restore 時に元の構成を再現できなかった。
        /// 本 overload は (id, paramsJson) を渡し、factory が paramsJson を parse して正しい引数を渡せる。
        /// Deserialize / MergePreset は paramsJson 付き factory がある type を優先する。
        /// </remarks>
        public void RegisterNodeFactory(string nodeType, Func<string, string, NodeBase> factory)
        {
            _nodeFactoriesWithParams[nodeType] = factory;
        }

        /// <summary>
        /// ファクトリを使ってノードを生成する。登録は行わない。
        /// 未登録のタイプの場合はnullを返す。
        /// </summary>
        public NodeBase? CreateNode(string nodeType)
        {
            if (_nodeFactories.TryGetValue(nodeType, out var factory))
            {
                var nodeId = Guid.NewGuid().ToString();
                return factory(nodeId);
            }
            if (_nodeFactoriesWithParams.TryGetValue(nodeType, out var paramFactory))
            {
                var nodeId = Guid.NewGuid().ToString();
                return paramFactory(nodeId, string.Empty);
            }
            Debug.LogWarning($"[GraphState] No factory for node type: {nodeType}");
            return null;
        }

        /// <summary>
        /// paramsJson を伴うファクトリ呼び出し (Deserialize/MergePreset 内部用)。
        /// </summary>
        private NodeBase? InvokeFactory(string nodeType, string nodeId, string paramsJson)
        {
            if (_nodeFactoriesWithParams.TryGetValue(nodeType, out var paramFactory))
                return paramFactory(nodeId, paramsJson);
            if (_nodeFactories.TryGetValue(nodeType, out var factory))
                return factory(nodeId);
            return null;
        }

        /// <summary>
        /// ノードを登録し、Setup()を呼び出す。
        /// </summary>
        internal void RegisterNode(NodeBase node)
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
        internal void RemoveNode(string nodeId)
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
        /// <param name="edgeId">supplied edge id (Phase 8 Codex Axis A fix)。null/empty なら GUID 自動生成。
        /// Hydration 経路で snapshot/serialization 上の元 ID を保持するために使う。</param>
        /// <returns>接続成功でtrue。型不一致またはポート未発見でfalse。</returns>
        internal bool TryConnect(string fromNodeId, string fromPort, string toNodeId, string toPort, string? edgeId = null)
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

            // 循環防止: R3 push 型では cycle が再入 + スタック増大を起こし映像停止につながる。
            // toNodeId から DFS して fromNodeId に到達できれば cycle。
            if (WouldCreateCycle(fromNodeId, toNodeId))
            {
                Debug.LogWarning($"[GraphState] Cycle would be created: {fromNodeId} → {toNodeId} (refused)");
                return false;
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
                    string.IsNullOrEmpty(edgeId) ? Guid.NewGuid().ToString() : edgeId!,
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
        /// (fromNodeId → toNodeId) を追加すると循環が生じるかを DFS で判定する。
        /// </summary>
        /// <remarks>
        /// 既存 <see cref="CycleDetector"/> は <see cref="EdgeIndex"/> に依存しているが、GraphState は
        /// 現在 List ベースで保持しているため inline DFS で代用する (List→EdgeIndex 全面置換は
        /// Phase 2 残課題 — メモリ allocation を増やしたくない launch 直前の最小変更)。
        /// </remarks>
        private bool WouldCreateCycle(string fromNodeId, string toNodeId)
        {
            // self-loop は呼び出し側 (TryConnect 冒頭) で既に弾いているが防御的にチェック
            if (fromNodeId == toNodeId) return true;

            var visited = new HashSet<string>();
            var stack = new Stack<string>();
            stack.Push(toNodeId);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!visited.Add(current)) continue;
                if (current == fromNodeId) return true;

                for (var i = 0; i < _edges.Count; i++)
                {
                    var e = _edges[i];
                    if (e.FromNodeId != current) continue;
                    if (!visited.Contains(e.ToNodeId))
                        stack.Push(e.ToNodeId);
                }
            }
            return false;
        }

        /// <summary>
        /// 指定されたエッジを切断する。
        /// </summary>
        internal void Disconnect(string fromNodeId, string fromPort, string toNodeId, string toPort)
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
        internal void Deserialize(GraphData data)
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
                    var node = InvokeFactory(nodeData.type, nodeData.id, nodeData.paramsJson);
                    if (node == null)
                    {
                        Debug.LogWarning($"[GraphState] Unknown node type: {nodeData.type}");
                        continue;
                    }

                    try
                    {
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
                    var node = InvokeFactory(nodeData.type, nodeData.id, nodeData.paramsJson);
                    if (node == null) continue;
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
        internal List<string> MergePreset(GraphData presetGraph, Vector3 spawnOffset)
        {
            var addedNodeIds = new List<string>();
            var idMap = new Dictionary<string, string>();

            // ステップ 1: 全ノードIDをリマップ（新GUID生成）
            foreach (var nodeData in presetGraph.nodes)
            {
                idMap[nodeData.id] = Guid.NewGuid().ToString();
            }

            // ステップ 2: ノードを生成・登録
            foreach (var nodeData in presetGraph.nodes)
            {
                var newId = idMap[nodeData.id];

                var node = InvokeFactory(nodeData.type, newId, nodeData.paramsJson);
                if (node == null)
                {
                    Debug.LogWarning($"[GraphState] MergePreset: Unknown node type '{nodeData.type}'");
                    continue;
                }

                try
                {
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

            // ステップ 3: エッジをリマップして接続 (MergePreset 内部処理の 3 段目)
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
        internal void Clear()
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
}
