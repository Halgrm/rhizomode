#nullable enable

using System.Collections.Generic;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// 全エッジの視覚表現（LineRenderer）を管理する。
    /// エッジの生成・破棄・毎フレーム更新・ハイライト・レイ判定を担う。
    /// </summary>
    public class EdgeVisualManager : MonoBehaviour
    {
        private const float EdgeWidth = 0.003f;
        private const float EdgeHighlightWidth = 0.006f;
        private const float EdgeCutThreshold = 0.02f;
        private const float EdgeMaxRayDistance = 5f;

        private static readonly Color FloatColor = new(0.63f, 0.82f, 0.94f);
        private static readonly Color ColorColor = new(0.94f, 0.78f, 0.39f);
        private static readonly Color BoolColor = new(0.94f, 0.55f, 0.55f);
        private static readonly Color HighlightColor = new(1f, 1f, 1f);

        private NodeVisualManager? _visualManager;
        private readonly Dictionary<string, EdgeVisual> _edgeVisuals = new();

        /// <summary>全エッジVisualへの読み取り専用アクセス。</summary>
        public IReadOnlyDictionary<string, EdgeVisual> EdgeVisuals => _edgeVisuals;

        /// <summary>
        /// NodeVisualManagerを設定する。
        /// </summary>
        public void Initialize(NodeVisualManager visualManager)
        {
            _visualManager = visualManager;
        }

        /// <summary>
        /// エッジの視覚表現を生成する。
        /// </summary>
        public void CreateEdgeVisual(Edge edge, ParamType portType)
        {
            if (_edgeVisuals.ContainsKey(edge.Id)) return;

            var go = new GameObject($"Edge_{edge.Id}");
            go.transform.SetParent(transform);

            var line = go.AddComponent<LineRenderer>();
            ConfigureLineRenderer(line, portType);

            var visual = new EdgeVisual(
                edge.Id, go, line,
                edge.FromNodeId, edge.FromPort,
                edge.ToNodeId, edge.ToPort
            );
            _edgeVisuals[edge.Id] = visual;

            UpdateEdgePositions(visual);
        }

        /// <summary>
        /// エッジの視覚表現を破棄する。
        /// </summary>
        public void DestroyEdgeVisual(string edgeId)
        {
            if (!_edgeVisuals.TryGetValue(edgeId, out var visual)) return;

            _edgeVisuals.Remove(edgeId);
            if (visual.Go != null)
                Destroy(visual.Go);
        }

        /// <summary>
        /// エッジのハイライト状態を切り替える。
        /// </summary>
        public void SetHighlight(string edgeId, bool highlight)
        {
            if (!_edgeVisuals.TryGetValue(edgeId, out var visual)) return;

            visual.IsHighlighted = highlight;
            var line = visual.Line;
            if (line == null) return;

            line.startWidth = highlight ? EdgeHighlightWidth : EdgeWidth;
            line.endWidth = highlight ? EdgeHighlightWidth : EdgeWidth;

            if (highlight)
            {
                line.startColor = HighlightColor;
                line.endColor = HighlightColor;
            }
            else
            {
                var color = GetPortTypeColor(ParamType.Float);
                line.startColor = color;
                line.endColor = color;
            }
        }

        /// <summary>
        /// レイに最も近いエッジIDを返す。閾値内に無ければnull。
        /// </summary>
        public string? GetEdgeIdNearRay(Vector3 rayOrigin, Vector3 rayDirection, float maxDistance = EdgeMaxRayDistance)
        {
            string? closestId = null;
            var closestDist = EdgeCutThreshold;

            foreach (var (id, visual) in _edgeVisuals)
            {
                if (visual.Line == null || visual.Line.positionCount < 2) continue;

                var segA = visual.Line.GetPosition(0);
                var segB = visual.Line.GetPosition(1);

                var dist = MathUtils.RayToSegmentDistance(rayOrigin, rayDirection, segA, segB);
                if (dist < closestDist)
                {
                    // maxDistance以内かも確認
                    var midPoint = (segA + segB) * 0.5f;
                    if (Vector3.Distance(rayOrigin, midPoint) > maxDistance) continue;

                    closestDist = dist;
                    closestId = id;
                }
            }

            return closestId;
        }

        /// <summary>
        /// 全エッジのハイライトをクリアする。
        /// </summary>
        public void ClearAllHighlights()
        {
            foreach (var (id, visual) in _edgeVisuals)
            {
                if (visual.IsHighlighted)
                    SetHighlight(id, false);
            }
        }

        /// <summary>
        /// 全エッジVisualを破棄する。
        /// </summary>
        public void Clear()
        {
            foreach (var visual in _edgeVisuals.Values)
            {
                if (visual.Go != null)
                    Destroy(visual.Go);
            }
            _edgeVisuals.Clear();
        }

        private void LateUpdate()
        {
            foreach (var visual in _edgeVisuals.Values)
            {
                UpdateEdgePositions(visual);
            }
        }

        private void UpdateEdgePositions(EdgeVisual visual)
        {
            if (_visualManager == null || visual.Line == null) return;

            var fromVisual = _visualManager.GetVisual(visual.FromNodeId);
            var toVisual = _visualManager.GetVisual(visual.ToNodeId);
            if (fromVisual == null || toVisual == null) return;

            var startPos = fromVisual.GetPortWorldPosition(visual.FromPort);
            var endPos = toVisual.GetPortWorldPosition(visual.ToPort);

            visual.Line.SetPosition(0, startPos);
            visual.Line.SetPosition(1, endPos);
        }

        private void ConfigureLineRenderer(LineRenderer line, ParamType portType)
        {
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = EdgeWidth;
            line.endWidth = EdgeWidth;
            line.numCapVertices = 4;

            var color = GetPortTypeColor(portType);
            line.startColor = color;
            line.endColor = color;

            // URP Unlit マテリアル
            line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")!);
            line.material.color = color;
        }

        private static Color GetPortTypeColor(ParamType type) => type switch
        {
            ParamType.Float => FloatColor,
            ParamType.Color => ColorColor,
            ParamType.Bool => BoolColor,
            _ => FloatColor
        };

        private void OnDestroy()
        {
            Clear();
        }
    }
}
