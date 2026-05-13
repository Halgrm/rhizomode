#nullable enable

using System.Collections.Generic;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.UI.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// 1つのノードのWorldSpace表示を制御する。NodeBaseの状態を
    /// UIToolkitパネルに反映し、ポートの視覚表現を管理する。
    /// </summary>
    /// <remarks>
    /// Phase 9 Round A で partial class に分割:
    /// - <c>NodeVisualController.cs</c> (本ファイル): Bind/LateUpdate/Port world position
    /// - <c>NodeVisualController.Layout.cs</c>: title/category/port UI 構築
    /// - <c>NodeVisualController.InlineWidgets.cs</c>: inline widget (slider/button/monitor/...) 構築
    /// - <c>NodeVisualController.Painter.cs</c>: waveform/spectrum 描画
    /// </remarks>
    [RequireComponent(typeof(WorldPanelHost))]
    public partial class NodeVisualController : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset? portUxml;

        private WorldPanelHost? _panelHost;
        private NodeBase? _node;
        private NodeTypeInfo? _typeInfo;
        private readonly Dictionary<string, VisualElement> _portElements = new();
        private IInlineMonitor? _monitor;
        private Label? _monitorValueLabel;
        private VisualElement? _monitorColorSwatch;
        private string? _lastMonitorText;
        private IInlineWaveform? _waveform;
        private VisualElement? _waveformElement;
        private Label? _waveformLabel;
        private IInlineSpectrum? _spectrum;
        private VisualElement? _spectrumElement;
        private Label? _spectrumLabel;
        private bool _needsBind;
        private int _bindRetryCount;
        private const int MaxBindRetries = 10;
        private bool _layoutReady;

        /// <summary>バインドされたノード。</summary>
        public NodeBase? Node => _node;

        /// <summary>ノードタイプ情報。</summary>
        public NodeTypeInfo? TypeInfo => _typeInfo;

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
        }

        /// <summary>
        /// ノードデータをバインドし、UIを構築する。
        /// rootVisualElementが未準備の場合は次フレーム以降にリトライする。
        /// </summary>
        public void Bind(NodeBase node, NodeTypeInfo typeInfo)
        {
            _node = node;
            _typeInfo = typeInfo;
            transform.position = node.Position;

            if (!TryBindInternal())
                _needsBind = true;
        }

        private bool TryBindInternal()
        {
            var root = _panelHost?.Root;
            if (root == null) return false;
            if (_node == null || _typeInfo == null) return false;

            SetTitle(root, _typeInfo.DisplayName);
            ApplyCategoryStyle(root, _typeInfo.Category);
            BuildPortUI(root, _node.GetPortDefinitions());
            BuildSlotBars(root, _node.GetPortDefinitions());
            BuildInlineSlider(root, _node);
            BuildInlineButton(root, _node);
            BuildInlineMonitor(root, _node);
            BuildInlineWaveform(root, _node);
            BuildInlineSpectrum(root, _node);
            BuildInlineColorPicker(root, _node);

            // レイアウト完了を待ってからポート座標を有効にする
            _layoutReady = false;
            root.RegisterCallback<GeometryChangedEvent>(OnLayoutReady);

            _needsBind = false;
            return true;
        }

        private void OnLayoutReady(GeometryChangedEvent evt)
        {
            _layoutReady = true;
            var root = _panelHost?.Root;
            root?.UnregisterCallback<GeometryChangedEvent>(OnLayoutReady);
        }

        /// <summary>
        /// 指定ポート名のワールド座標を返す。エッジ描画用。
        /// </summary>
        public Vector3 GetPortWorldPosition(string portName)
        {
            if (!_layoutReady)
                return transform.position;

            if (!_portElements.TryGetValue(portName, out var element))
                return transform.position;

            // port-dot（丸）の中心からエッジを出す
            var dot = element.Q("port-dot");
            var target = dot ?? element;
            var rect = target.worldBound;

            // レイアウト未確定時にworldBoundがNaNを返すことがある
            if (float.IsNaN(rect.x) || float.IsNaN(rect.y) ||
                float.IsNaN(rect.width) || float.IsNaN(rect.height))
                return transform.position;

            var dotCenter = new Vector2(
                rect.x + rect.width * 0.5f,
                rect.y + rect.height * 0.5f
            );

            return PanelToWorldPosition(dotCenter);
        }

        /// <summary>
        /// 指定ポートのスナップターゲットハイライトを設定する。
        /// </summary>
        public void SetPortHighlight(string portName, bool highlight)
        {
            if (!_portElements.TryGetValue(portName, out var element)) return;

            var dot = element.Q("port-dot");
            if (dot == null) return;

            if (highlight)
                dot.AddToClassList("port-dot--snap-target");
            else
                dot.RemoveFromClassList("port-dot--snap-target");
        }

        private void LateUpdate()
        {
            if (_needsBind)
            {
                _bindRetryCount++;
                if (!TryBindInternal() && _bindRetryCount >= MaxBindRetries)
                {
                    Debug.LogWarning($"[NodeVisualController] Bind failed after {MaxBindRetries} retries for node {_node?.Id}");
                    _needsBind = false;
                }
            }

            if (_node != null)
                _node.Position = transform.position;

            if (_monitor != null && _monitorValueLabel != null)
            {
                var displayValue = _monitor.MonitorDisplayValue;
                if (displayValue != _lastMonitorText)
                {
                    _lastMonitorText = displayValue;
                    _monitorValueLabel.text = displayValue;
                    if (_monitorColorSwatch != null && _monitor.MonitorType == ParamType.Color)
                        _monitorColorSwatch.style.backgroundColor = _monitor.MonitorColor;
                }
            }

            if (_waveform != null && _waveformElement != null)
            {
                _waveformElement.MarkDirtyRepaint();
                if (_waveformLabel != null)
                    _waveformLabel.text = _waveform.WaveformLabel;
            }

            if (_spectrum != null && _spectrumElement != null)
            {
                _spectrumElement.MarkDirtyRepaint();
                if (_spectrumLabel != null)
                    _spectrumLabel.text = _spectrum.SpectrumLabel;
            }
        }

        private Vector3 PanelToWorldPosition(Vector2 panelPos)
        {
            if (_panelHost == null) return transform.position;
            if (_panelHost.TextureWidth <= 0 || _panelHost.TextureHeight <= 0)
                return transform.position;

            // パネル座標（ピクセル）を-0.5〜0.5の正規化座標に変換
            float nx = (panelPos.x / _panelHost.TextureWidth) - 0.5f;
            float ny = 0.5f - (panelPos.y / _panelHost.TextureHeight);

            // Quadメッシュは-0.5〜0.5。TransformPointがlocalScale（=worldSize）を適用するので
            // ここでは正規化座標をそのまま使う
            var localPos = new Vector3(nx, ny, 0f);

            return transform.TransformPoint(localPos);
        }
    }
}
