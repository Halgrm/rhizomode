#nullable enable

using System;
using System.Collections.Generic;
using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// 出力ポートからのドラッグによるエッジ接続を管理する。
    /// ドラッグ中はプレビューラインを表示し、互換ポートへのスナップを行う。
    /// </summary>
    public class EdgeDragHandler : MonoBehaviour
    {
        private const float PortGrabRadius = 0.05f;
        private const float PortSnapRadius = 0.10f;
        private const float RayMaxDistance = 5f;
        private const float PreviewLineWidth = 0.003f;
        private const float DefaultRayEndDistance = 2f;

        private IRayProvider? _rayProvider;
        private IControllerInput? _controllerInput;
        private NodeVisualManager? _visualManager;
        private GraphContextBehaviour? _graphContext;
        private EdgeVisualManager? _edgeVisualManager;
        private IDisposable? _selectSubscription;

        // ドラッグ状態
        private bool _isDragging;
        private string? _sourceNodeId;
        private string? _sourcePortName;
        private ParamType _sourceParamType;
        private LineRenderer? _previewLine;
        private GameObject? _previewGo;

        // スナップ状態
        private string? _snapNodeId;
        private string? _snapPortName;

        /// <summary>
        /// 依存関係を設定し、入力を購読する。
        /// </summary>
        public void Initialize(
            IRayProvider rayProvider,
            IControllerInput controllerInput,
            NodeVisualManager visualManager,
            GraphContextBehaviour graphContext,
            EdgeVisualManager edgeVisualManager)
        {
            _rayProvider = rayProvider;
            _controllerInput = controllerInput;
            _visualManager = visualManager;
            _graphContext = graphContext;
            _edgeVisualManager = edgeVisualManager;

            _selectSubscription = controllerInput.OnSelect
                .Subscribe(OnSelectChanged);
        }

        private void Update()
        {
            if (!_isDragging || _rayProvider == null) return;

            UpdatePreviewLine();
            UpdateSnapTarget();
        }

        private void OnSelectChanged(bool pressed)
        {
            if (pressed)
                TryStartDrag();
            else if (_isDragging)
                EndDrag();
        }

        private void TryStartDrag()
        {
            if (_rayProvider == null || _visualManager == null) return;

            var ray = new Ray(_rayProvider.RayOrigin, _rayProvider.RayDirection);
            if (!Physics.Raycast(ray, out var hit, RayMaxDistance)) return;

            var nodeVisual = hit.collider.GetComponent<NodeVisualController>();
            if (nodeVisual?.Node == null) return;

            // 出力ポートの近接判定
            var (portName, paramType) = FindNearestOutputPort(nodeVisual, hit.point);
            if (portName == null) return;

            StartDrag(nodeVisual.Node.Id, portName, paramType);
        }

        private (string? portName, ParamType type) FindNearestOutputPort(
            NodeVisualController nodeVisual, Vector3 hitPoint)
        {
            if (nodeVisual.Node == null) return (null, ParamType.Float);

            string? closestPort = null;
            var closestDist = PortGrabRadius;
            var closestType = ParamType.Float;

            foreach (var port in nodeVisual.Node.GetPortDefinitions())
            {
                if (port.direction != PortDirection.Output) continue;

                var portPos = nodeVisual.GetPortWorldPosition(port.name);
                var dist = Vector3.Distance(portPos, hitPoint);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestPort = port.name;
                    closestType = port.type;
                }
            }

            return (closestPort, closestType);
        }

        private void StartDrag(string nodeId, string portName, ParamType paramType)
        {
            _isDragging = true;
            _sourceNodeId = nodeId;
            _sourcePortName = portName;
            _sourceParamType = paramType;

            _previewGo = new GameObject("EdgePreview");
            _previewLine = _previewGo.AddComponent<LineRenderer>();
            ConfigurePreviewLine(_previewLine, paramType);
        }

        private void UpdatePreviewLine()
        {
            if (_previewLine == null || _rayProvider == null || _visualManager == null) return;

            var fromVisual = _visualManager.GetVisual(_sourceNodeId!);
            if (fromVisual == null) return;

            var startPos = fromVisual.GetPortWorldPosition(_sourcePortName!);
            var endPos = _snapPortName != null && _snapNodeId != null
                ? GetSnapPortPosition()
                : _rayProvider.RayOrigin + _rayProvider.RayDirection * DefaultRayEndDistance;

            _previewLine.SetPosition(0, startPos);
            _previewLine.SetPosition(1, endPos);
        }

        private Vector3 GetSnapPortPosition()
        {
            if (_visualManager == null || _snapNodeId == null || _snapPortName == null)
                return Vector3.zero;

            var visual = _visualManager.GetVisual(_snapNodeId);
            return visual != null ? visual.GetPortWorldPosition(_snapPortName) : Vector3.zero;
        }

        private void UpdateSnapTarget()
        {
            if (_visualManager == null || _rayProvider == null) return;

            ClearCurrentSnap();

            var rayEnd = _rayProvider.RayOrigin + _rayProvider.RayDirection * DefaultRayEndDistance;
            string? bestNodeId = null;
            string? bestPortName = null;
            var bestDist = PortSnapRadius;

            foreach (var (nodeId, visual) in _visualManager.Visuals)
            {
                if (nodeId == _sourceNodeId || visual.Node == null) continue;

                foreach (var port in visual.Node.GetPortDefinitions())
                {
                    if (port.direction != PortDirection.Input) continue;
                    if (port.type != _sourceParamType) continue;

                    var portPos = visual.GetPortWorldPosition(port.name);
                    var dist = Vector3.Distance(portPos, rayEnd);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestNodeId = nodeId;
                        bestPortName = port.name;
                    }
                }
            }

            if (bestNodeId != null && bestPortName != null)
            {
                _snapNodeId = bestNodeId;
                _snapPortName = bestPortName;
                var visual = _visualManager.GetVisual(bestNodeId);
                visual?.SetPortHighlight(bestPortName, true);
            }
        }

        private void ClearCurrentSnap()
        {
            if (_snapNodeId != null && _snapPortName != null && _visualManager != null)
            {
                var visual = _visualManager.GetVisual(_snapNodeId);
                visual?.SetPortHighlight(_snapPortName, false);
            }
            _snapNodeId = null;
            _snapPortName = null;
        }

        private void EndDrag()
        {
            if (_snapNodeId != null && _snapPortName != null)
            {
                TryConnect();
            }

            ClearCurrentSnap();
            DestroyPreview();
            _isDragging = false;
            _sourceNodeId = null;
            _sourcePortName = null;
        }

        private void TryConnect()
        {
            if (_graphContext == null || _edgeVisualManager == null) return;
            if (_sourceNodeId == null || _sourcePortName == null) return;
            if (_snapNodeId == null || _snapPortName == null) return;

            var context = _graphContext.Context;
            if (!context.TryConnect(_sourceNodeId, _sourcePortName, _snapNodeId, _snapPortName))
                return;

            // 接続成功: エッジVisualを生成
            var edges = context.Edges;
            for (var i = edges.Count - 1; i >= 0; i--)
            {
                var edge = edges[i];
                if (edge.FromNodeId == _sourceNodeId && edge.FromPort == _sourcePortName &&
                    edge.ToNodeId == _snapNodeId && edge.ToPort == _snapPortName)
                {
                    _edgeVisualManager.CreateEdgeVisual(edge, _sourceParamType);
                    break;
                }
            }
        }

        private void DestroyPreview()
        {
            if (_previewGo != null)
            {
                Destroy(_previewGo);
                _previewGo = null;
                _previewLine = null;
            }
        }

        private static void ConfigurePreviewLine(LineRenderer line, ParamType paramType)
        {
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = PreviewLineWidth;
            line.endWidth = PreviewLineWidth;
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
            line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")!);
            line.material.color = color;
        }

        private void OnDestroy()
        {
            _selectSubscription?.Dispose();
            DestroyPreview();
        }
    }
}
