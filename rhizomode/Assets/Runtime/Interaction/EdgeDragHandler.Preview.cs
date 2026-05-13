#nullable enable

using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="EdgeDragHandler"/> の partial: プレビューライン (LineRenderer) の
    /// 生成 / 破棄 / 更新。
    /// Phase 9 Round D で本体から分離。
    /// </summary>
    public partial class EdgeDragHandler
    {
        private void UpdatePreviewLine()
        {
            if (_previewLine == null || _rayProvider == null || _visualManager == null) return;
            if (_sourceNodeId == null || _sourcePortName == null) return;

            var fromVisual = _visualManager.GetVisual(_sourceNodeId);
            if (fromVisual == null) return;

            var startPos = fromVisual.GetPortWorldPosition(_sourcePortName);
            var endPos = _rayProvider.RayOrigin + _rayProvider.RayDirection * defaultRayEndDistance;

            // スナップ先があればそこに引く
            if (_highlightedNodeId != null && _highlightedNodeId != _sourceNodeId &&
                _highlightedPortName != null)
            {
                var targetVisual = _visualManager.GetVisual(_highlightedNodeId);
                if (targetVisual != null)
                {
                    endPos = targetVisual.GetPortWorldPosition(_highlightedPortName);
                }
            }

            _previewLine.SetPosition(0, startPos);
            _previewLine.SetPosition(1, endPos);
        }

        private void CreatePreviewLine(ParamType paramType)
        {
            _previewGo = new GameObject("EdgePreview");
            _previewLine = _previewGo.AddComponent<LineRenderer>();
            ConfigurePreviewLine(_previewLine, paramType);
        }

        private void DestroyPreview()
        {
            if (_previewMaterial != null)
            {
                Destroy(_previewMaterial);
                _previewMaterial = null;
            }
            if (_previewGo != null)
            {
                Destroy(_previewGo);
                _previewGo = null;
                _previewLine = null;
            }
        }

        private void ConfigurePreviewLine(LineRenderer line, ParamType paramType)
        {
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = previewLineWidth;
            line.endWidth = previewLineWidth;
            line.numCapVertices = 4;

            var color = paramType switch
            {
                ParamType.Float => new Color(0.63f, 0.82f, 0.94f, 0.6f),
                ParamType.Color => new Color(0.94f, 0.78f, 0.39f, 0.6f),
                ParamType.Bool => new Color(0.94f, 0.55f, 0.55f, 0.6f),
                _ => new Color(0.63f, 0.82f, 0.94f, 0.6f)
            };

            line.startColor = color;
            line.endColor = color;

            if (CachedPreviewShader == null)
            {
                CachedPreviewShader = Shader.Find("Universal Render Pipeline/Unlit")
                                      ?? Shader.Find("Unlit/Color");
            }
            if (CachedPreviewShader != null)
            {
                _previewMaterial = new Material(CachedPreviewShader);
                _previewMaterial.color = color;
                line.material = _previewMaterial;
            }
        }
    }
}
