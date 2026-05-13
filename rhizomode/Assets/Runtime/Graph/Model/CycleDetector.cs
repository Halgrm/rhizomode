#nullable enable

using System.Collections.Generic;

namespace Rhizomode.Graph.Model
{
    /// <summary>
    /// グラフの循環検出 (DFS ベース)。
    /// </summary>
    /// <remarks>
    /// Phase 2 で導入。Plan v5.3: 一律サイクル拒否、ただし <c>[FeedbackAllowed]</c> 属性のあるノードは
    /// Phase 8 で escape route を実装予定 (現状は escape route なし)。
    ///
    /// 使用方法: <see cref="WouldCreateCycle"/> でエッジ追加前にチェック。true なら接続を拒否する。
    /// </remarks>
    public static class CycleDetector
    {
        /// <summary>
        /// 既存の EdgeIndex に対し、(fromNodeId → toNodeId) のエッジを追加すると
        /// 循環が生じるかどうかを判定する。
        /// </summary>
        /// <returns>循環が生じる場合 true。</returns>
        public static bool WouldCreateCycle(EdgeIndex index, string fromNodeId, string toNodeId)
        {
            if (fromNodeId == toNodeId) return true;

            // toNodeId から DFS を開始し、fromNodeId に到達できれば循環。
            var visited = new HashSet<string>();
            var stack = new Stack<string>();
            stack.Push(toNodeId);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!visited.Add(current)) continue;

                if (current == fromNodeId) return true;

                foreach (var edgeId in index.OutgoingEdgeIds(current))
                {
                    var edge = index.GetById(edgeId);
                    if (edge != null && !visited.Contains(edge.ToNodeId))
                    {
                        stack.Push(edge.ToNodeId);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 現在のエッジ集合に循環が含まれているかを判定する (デシリアライズ後の検証等)。
        /// </summary>
        public static bool HasCycle(EdgeIndex index)
        {
            var color = new Dictionary<string, byte>();
            const byte White = 0, Gray = 1, Black = 2;

            foreach (var edge in index.Edges)
            {
                if (!color.ContainsKey(edge.FromNodeId)) color[edge.FromNodeId] = White;
                if (!color.ContainsKey(edge.ToNodeId)) color[edge.ToNodeId] = White;
            }

            var nodes = new List<string>(color.Keys);
            foreach (var node in nodes)
            {
                if (color[node] == White && DfsHasCycle(node, index, color, Gray, Black)) return true;
            }
            return false;
        }

        private static bool DfsHasCycle(
            string node, EdgeIndex index, Dictionary<string, byte> color, byte gray, byte black)
        {
            color[node] = gray;
            foreach (var edgeId in index.OutgoingEdgeIds(node))
            {
                var edge = index.GetById(edgeId);
                if (edge == null) continue;
                var next = edge.ToNodeId;
                if (!color.TryGetValue(next, out var c)) c = 0;
                if (c == gray) return true;
                if (c == 0 && DfsHasCycle(next, index, color, gray, black)) return true;
            }
            color[node] = black;
            return false;
        }
    }
}
