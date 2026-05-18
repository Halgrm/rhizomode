#nullable enable

using System.Collections.Generic;
using Rhizomode.Presentation.Layering;
using Rhizomode.SharedKernel;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// 全エッジの視覚表現（LineRenderer）を管理する。
    /// エッジの生成・破棄・毎フレーム更新・ハイライト・レイ判定を担う。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class EdgeVisualManager : MonoBehaviour
    {
        [Header("エッジ幅")]
        [SerializeField, Range(0.001f, 0.02f), Tooltip("エッジの通常幅（メートル）")]
        private float edgeWidth = 0.003f;

        [SerializeField, Range(0.003f, 0.03f), Tooltip("ハイライト時のエッジ幅（メートル）")]
        private float edgeHighlightWidth = 0.006f;

        [Header("エッジ選択")]
        [SerializeField, Range(0.005f, 0.1f), Tooltip("エッジ切断のレイ距離閾値（メートル）")]
        private float edgeCutThreshold = 0.02f;

        [SerializeField, Range(1f, 20f), Tooltip("エッジ選択の最大レイ距離（メートル）")]
        private float edgeMaxRayDistance = 5f;

        [Header("タイプ別カラー")]
        [SerializeField, ColorUsage(false), Tooltip("Float型エッジの色")]
        private Color floatColor = new(0.63f, 0.82f, 0.94f);

        [SerializeField, ColorUsage(false), Tooltip("Color型エッジの色")]
        private Color colorColor = new(0.94f, 0.78f, 0.39f);

        [SerializeField, ColorUsage(false), Tooltip("Bool型エッジの色")]
        private Color boolColor = new(0.94f, 0.55f, 0.55f);

        [SerializeField, ColorUsage(false), Tooltip("ハイライト時の色")]
        private Color highlightColor = new(1f, 1f, 1f);

        [Header("グロー設定")]
        [SerializeField, Range(0.1f, 5f)]
        private float normalGlowIntensity = 1.0f;

        [SerializeField, Range(0.5f, 10f)]
        private float highlightGlowIntensity = 2.5f;

        private const string EdgeShaderName = "Rhizomode/EdgeGlow";
        private const string FallbackShaderName = "Universal Render Pipeline/Unlit";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");

        private static Shader? CachedEdgeShader;
        private bool _useCustomShader;

        private NodeVisualManager? _visualManager;
        private readonly Dictionary<string, EdgeVisual> _edgeVisuals = new();
        private Material? _sharedEdgeMaterial;
        private readonly HashSet<string> _dirtyNodeIds = new();
        private bool _allDirty;

        /// <summary>全エッジVisualへの読み取り専用アクセス。</summary>
        public IReadOnlyDictionary<string, EdgeVisual> EdgeVisuals => _edgeVisuals;

        /// <summary>
        /// ノードが移動したことを通知する。関連エッジのみ次フレームで更新される。
        /// </summary>
        public void MarkNodeDirty(string nodeId)
        {
            _dirtyNodeIds.Add(nodeId);
        }

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
        public void CreateEdgeVisual(EdgeViewModel edge, ParamType portType)
        {
            if (_edgeVisuals.ContainsKey(edge.EdgeId)) return;

            var go = new GameObject($"Edge_{edge.EdgeId}");
            go.transform.SetParent(transform);
            MirrorHiddenLayer.ApplyRecursive(go);

            var line = go.AddComponent<LineRenderer>();

            var visual = new EdgeVisual(
                edge.EdgeId, go, line,
                edge.FromNodeId, edge.FromPortName,
                edge.ToNodeId, edge.ToPortName,
                portType
            );
            ConfigureLineRenderer(line, portType, visual);
            _edgeVisuals[edge.EdgeId] = visual;
            _allDirty = true;
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

            line.startWidth = highlight ? edgeHighlightWidth : edgeWidth;
            line.endWidth = highlight ? edgeHighlightWidth : edgeWidth;

            // MPB対応: カスタムシェーダー使用時はグロー強度で制御
            if (visual.Mpb != null)
            {
                var color = highlight ? highlightColor : GetPortTypeColor(visual.PortType);
                var intensity = highlight ? highlightGlowIntensity : normalGlowIntensity;
                visual.Mpb.SetColor(BaseColorId, color);
                visual.Mpb.SetFloat(GlowIntensityId, intensity);
                line.SetPropertyBlock(visual.Mpb);
            }
            else if (highlight)
            {
                line.startColor = highlightColor;
                line.endColor = highlightColor;
            }
            else
            {
                var color = GetPortTypeColor(visual.PortType);
                line.startColor = color;
                line.endColor = color;
            }
        }

        /// <summary>
        /// レイに最も近いエッジIDを返す。閾値内に無ければnull。
        /// </summary>
        public string? GetEdgeIdNearRay(Vector3 rayOrigin, Vector3 rayDirection)
        {
            string? closestId = null;
            var closestDist = edgeCutThreshold;

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
                    if (Vector3.Distance(rayOrigin, midPoint) > edgeMaxRayDistance) continue;

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
        /// 用意済みの (edge, portType) ペア集合からエッジビジュアルを再構築する。
        /// グラフロード後に使用。caller (GraphAdapter) が GraphState から
        /// EdgeViewModel + ParamType を抽出して渡す。
        /// </summary>
        public void RebuildAllEdgeVisuals(IReadOnlyList<(EdgeViewModel edge, ParamType portType)> edges)
        {
            Clear();

            foreach (var (edge, portType) in edges)
                CreateEdgeVisual(edge, portType);
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
            // 新規作成直後は全更新、以降はdirtyノード関連のみ
            if (_allDirty)
            {
                foreach (var visual in _edgeVisuals.Values)
                    UpdateEdgePositions(visual);
                _allDirty = false;
            }
            else if (_dirtyNodeIds.Count > 0)
            {
                foreach (var visual in _edgeVisuals.Values)
                {
                    if (_dirtyNodeIds.Contains(visual.FromNodeId) ||
                        _dirtyNodeIds.Contains(visual.ToNodeId))
                    {
                        UpdateEdgePositions(visual);
                    }
                }
            }
            _dirtyNodeIds.Clear();
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

        private void ConfigureLineRenderer(LineRenderer line, ParamType portType, EdgeVisual visual)
        {
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = edgeWidth;
            line.endWidth = edgeWidth;
            line.numCapVertices = 4;

            var color = GetPortTypeColor(portType);
            line.startColor = color;
            line.endColor = color;

            // 共有マテリアルを使い回す（マテリアルリーク防止）
            EnsureSharedMaterial();
            if (_sharedEdgeMaterial != null)
                line.sharedMaterial = _sharedEdgeMaterial;

            // カスタムシェーダー使用時はMaterialPropertyBlockで個別色・グロー設定
            if (_useCustomShader)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(BaseColorId, color);
                mpb.SetFloat(GlowIntensityId, normalGlowIntensity);
                line.SetPropertyBlock(mpb);
                visual.Mpb = mpb;
            }
        }

        private void EnsureSharedMaterial()
        {
            if (_sharedEdgeMaterial != null) return;

            if (CachedEdgeShader == null)
            {
                // カスタムグローシェーダーを優先的に使用
                CachedEdgeShader = Shader.Find(EdgeShaderName);
                if (CachedEdgeShader != null)
                {
                    _useCustomShader = true;
                }
                else
                {
                    Debug.LogWarning($"[EdgeVisualManager] {EdgeShaderName} not found, using fallback");
                    CachedEdgeShader = Shader.Find(FallbackShaderName);
                    if (CachedEdgeShader == null)
                        CachedEdgeShader = Shader.Find("Unlit/Color");
                }
            }

            if (CachedEdgeShader != null)
            {
                _sharedEdgeMaterial = new Material(CachedEdgeShader);
                _sharedEdgeMaterial.color = Color.white;
            }
        }

        private Color GetPortTypeColor(ParamType type) => type switch
        {
            ParamType.Float => floatColor,
            ParamType.Color => colorColor,
            ParamType.Bool => boolColor,
            _ => floatColor
        };

        private void OnDestroy()
        {
            Clear();
            if (_sharedEdgeMaterial != null)
            {
                Destroy(_sharedEdgeMaterial);
                _sharedEdgeMaterial = null;
            }
        }
    }
}
