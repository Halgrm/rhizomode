#nullable enable

using System.Collections.Generic;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;
using UnityEngine.UIElements;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// 1つのノードのWorldSpace表示を制御する。NodeBaseの状態を
    /// UIToolkitパネルに反映し、ポートの視覚表現を管理する。
    /// </summary>
    [RequireComponent(typeof(WorldPanelHost))]
    public class NodeVisualController : MonoBehaviour
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

        private void SetTitle(VisualElement root, string title)
        {
            var label = root.Q<Label>("node-title");
            if (label != null)
                label.text = title;
        }

        private void ApplyCategoryStyle(VisualElement root, NodeCategory category)
        {
            var header = root.Q("header");
            if (header == null) return;

            var className = category switch
            {
                NodeCategory.Input => "node-header--input",
                NodeCategory.Math => "node-header--math",
                NodeCategory.VFX => "node-header--vfx",
                NodeCategory.Shader => "node-header--shader",
                NodeCategory.Time => "node-header--time",
                NodeCategory.Utility => "node-header--utility",
                _ => "node-header--utility"
            };

            header.AddToClassList(className);
        }

        /// <summary>
        /// Rector風スロットバーを上部（入力）・下部（出力）に配置する。
        /// </summary>
        private void BuildSlotBars(VisualElement root, IReadOnlyList<PortDefinition> ports)
        {
            var topSlots = root.Q("slot-list-top");
            var bottomSlots = root.Q("slot-list-bottom");
            if (topSlots == null || bottomSlots == null) return;

            topSlots.Clear();
            bottomSlots.Clear();

            foreach (var port in ports)
            {
                var bar = new VisualElement();
                bar.AddToClassList("port-dot");
                var typeClass = port.type switch
                {
                    ParamType.Float => "port-dot--float",
                    ParamType.Color => "port-dot--color",
                    ParamType.Bool => "port-dot--bool",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(typeClass))
                    bar.AddToClassList(typeClass);

                if (port.direction == PortDirection.Input)
                    topSlots.Add(bar);
                else
                    bottomSlots.Add(bar);
            }
        }

        private void BuildPortUI(VisualElement root, IReadOnlyList<PortDefinition> ports)
        {
            var inputContainer = root.Q("input-ports");
            var outputContainer = root.Q("output-ports");
            if (inputContainer == null || outputContainer == null) return;

            inputContainer.Clear();
            outputContainer.Clear();
            _portElements.Clear();

            foreach (var port in ports)
            {
                var container = port.direction == PortDirection.Input
                    ? inputContainer
                    : outputContainer;

                var portElement = CreatePortElement(port);
                container.Add(portElement);
                _portElements[port.name] = portElement;
            }
        }

        private VisualElement CreatePortElement(PortDefinition port)
        {
            VisualElement element;

            if (portUxml != null)
            {
                element = portUxml.Instantiate();
            }
            else
            {
                element = CreatePortElementFallback();
            }

            // ポート名を設定
            var label = element.Q<Label>("port-label");
            if (label != null)
                label.text = port.name;

            // ポートの型に応じた色クラスを追加
            var dot = element.Q("port-dot");
            if (dot != null)
            {
                var typeClass = port.type switch
                {
                    ParamType.Float => "port-dot--float",
                    ParamType.Color => "port-dot--color",
                    ParamType.Bool => "port-dot--bool",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(typeClass))
                    dot.AddToClassList(typeClass);
            }

            return element;
        }

        private static VisualElement CreatePortElementFallback()
        {
            var row = new VisualElement();
            row.AddToClassList("port-row");

            var dot = new VisualElement();
            dot.name = "port-dot";
            dot.AddToClassList("port-dot");
            row.Add(dot);

            var label = new Label();
            label.name = "port-label";
            label.AddToClassList("port-label");
            row.Add(label);

            return row;
        }

        private void BuildInlineSlider(VisualElement root, NodeBase node)
        {
            if (node is not IInlineSlider slider) return;

            var container = new VisualElement();
            container.AddToClassList("inline-slider-container");

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var sliderElement = new Slider(slider.SliderMin, slider.SliderMax);
            sliderElement.value = slider.SliderValue;
            sliderElement.AddToClassList("inline-slider");
            sliderElement.style.flexGrow = 1;
            row.Add(sliderElement);

            var valueLabel = new Label(slider.SliderValue.ToString("F2"));
            valueLabel.AddToClassList("inline-slider-value");
            row.Add(valueLabel);

            container.Add(row);

            sliderElement.RegisterValueChangedCallback(evt =>
            {
                slider.SliderValue = evt.newValue;
                valueLabel.text = evt.newValue.ToString("F2");
            });

            // ヘッダーとポートコンテナの間に挿入
            var portContainer = root.Q("port-container");
            if (portContainer?.parent != null)
            {
                var parent = portContainer.parent;
                var index = parent.IndexOf(portContainer);
                parent.Insert(index, container);
            }
            else
            {
                root.Add(container);
            }
        }

        private void BuildInlineButton(VisualElement root, NodeBase node)
        {
            if (node is not IInlineButton button) return;

            var container = new VisualElement();
            container.AddToClassList("inline-button-container");

            Button? btnRef = null;
            var btn = new Button(() =>
            {
                button.OnButtonPressed();
                if (btnRef != null) btnRef.text = button.ButtonLabel;

                // スライダー範囲を同期（ConstFloat等のレンジ切替対応）
                if (node is IInlineSlider sliderNode)
                {
                    var slider = root.Q<Slider>();
                    if (slider != null)
                    {
                        slider.lowValue = sliderNode.SliderMin;
                        slider.highValue = sliderNode.SliderMax;
                        slider.SetValueWithoutNotify(sliderNode.SliderValue);
                        var valLabel = root.Q<Label>(className: "inline-slider-value");
                        if (valLabel != null) valLabel.text = sliderNode.SliderValue.ToString("F2");
                    }
                }
            });
            btnRef = btn;
            btn.text = button.ButtonLabel;
            btn.AddToClassList("inline-button");
            container.Add(btn);

            var portContainer = root.Q("port-container");
            if (portContainer?.parent != null)
            {
                var parent = portContainer.parent;
                var index = parent.IndexOf(portContainer);
                parent.Insert(index, container);
            }
            else
            {
                root.Add(container);
            }
        }

        private void BuildInlineMonitor(VisualElement root, NodeBase node)
        {
            if (node is not IInlineMonitor monitor) return;
            _monitor = monitor;

            var container = new VisualElement();
            container.AddToClassList("inline-monitor-container");

            if (monitor.MonitorType == ParamType.Color)
            {
                _monitorColorSwatch = new VisualElement();
                _monitorColorSwatch.AddToClassList("inline-monitor-swatch");
                _monitorColorSwatch.style.backgroundColor = monitor.MonitorColor;
                container.Add(_monitorColorSwatch);
            }

            _monitorValueLabel = new Label(monitor.MonitorDisplayValue);
            _monitorValueLabel.AddToClassList("inline-monitor-value");

            var typeClass = monitor.MonitorType switch
            {
                ParamType.Float => "inline-monitor-value--float",
                ParamType.Bool => "inline-monitor-value--bool",
                ParamType.Color => "inline-monitor-value--color",
                _ => ""
            };
            if (!string.IsNullOrEmpty(typeClass))
                _monitorValueLabel.AddToClassList(typeClass);

            container.Add(_monitorValueLabel);

            var portContainer = root.Q("port-container");
            if (portContainer?.parent != null)
            {
                var parent = portContainer.parent;
                var index = parent.IndexOf(portContainer);
                parent.Insert(index, container);
            }
            else
            {
                root.Add(container);
            }
        }

        private void BuildInlineWaveform(VisualElement root, NodeBase node)
        {
            if (node is not IInlineWaveform waveform) return;
            _waveform = waveform;

            var container = new VisualElement();
            container.AddToClassList("inline-waveform-container");

            _waveformElement = new VisualElement();
            _waveformElement.AddToClassList("inline-waveform");
            _waveformElement.generateVisualContent += DrawWaveform;
            container.Add(_waveformElement);

            _waveformLabel = new Label(waveform.WaveformLabel);
            _waveformLabel.AddToClassList("inline-monitor-value");
            _waveformLabel.AddToClassList("inline-monitor-value--float");
            container.Add(_waveformLabel);

            var portContainer = root.Q("port-container");
            if (portContainer?.parent != null)
            {
                var parent = portContainer.parent;
                var index = parent.IndexOf(portContainer);
                parent.Insert(index, container);
            }
            else
            {
                root.Add(container);
            }
        }

        private void DrawWaveform(MeshGenerationContext ctx)
        {
            if (_waveform?.WaveformBuffer == null || _waveformElement == null) return;

            var painter = ctx.painter2D;
            var rect = _waveformElement.contentRect;
            if (float.IsNaN(rect.width) || rect.width <= 0) return;

            var buffer = _waveform.WaveformBuffer;
            var len = _waveform.WaveformLength;
            if (len <= 0) return;

            var startIndex = _waveform.WaveformWriteIndex;

            // 背景
            painter.fillColor = new Color(0.05f, 0.08f, 0.12f, 0.8f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, 0));
            painter.LineTo(new Vector2(rect.width, 0));
            painter.LineTo(new Vector2(rect.width, rect.height));
            painter.LineTo(new Vector2(0, rect.height));
            painter.ClosePath();
            painter.Fill();

            // 中心線
            var halfH = rect.height * 0.5f;
            painter.strokeColor = new Color(0.3f, 0.4f, 0.5f, 0.4f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, halfH));
            painter.LineTo(new Vector2(rect.width, halfH));
            painter.Stroke();

            // 波形
            painter.strokeColor = new Color(0.3f, 0.8f, 1f, 0.9f);
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            for (var i = 0; i < len; i++)
            {
                var idx = (startIndex + i) % len;
                var x = (float)i / (len - 1) * rect.width;
                // 波形は [-1, 1] 範囲。中心線を基準に上下に描画する。
                var val = Mathf.Clamp(buffer[idx], -1f, 1f);
                var y = halfH - val * halfH;

                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }

            painter.Stroke();
        }

        private void BuildInlineSpectrum(VisualElement root, NodeBase node)
        {
            if (node is not IInlineSpectrum spectrum) return;
            _spectrum = spectrum;

            var container = new VisualElement();
            container.AddToClassList("inline-waveform-container");

            _spectrumElement = new VisualElement();
            _spectrumElement.AddToClassList("inline-waveform");
            _spectrumElement.generateVisualContent += DrawSpectrum;
            container.Add(_spectrumElement);

            _spectrumLabel = new Label(spectrum.SpectrumLabel);
            _spectrumLabel.AddToClassList("inline-monitor-value");
            _spectrumLabel.AddToClassList("inline-monitor-value--float");
            container.Add(_spectrumLabel);

            var portContainer = root.Q("port-container");
            if (portContainer?.parent != null)
            {
                var parent = portContainer.parent;
                var index = parent.IndexOf(portContainer);
                parent.Insert(index, container);
            }
            else
            {
                root.Add(container);
            }
        }

        private void DrawSpectrum(MeshGenerationContext ctx)
        {
            if (_spectrum?.SpectrumBuffer == null || _spectrumElement == null) return;

            var painter = ctx.painter2D;
            var rect = _spectrumElement.contentRect;
            if (float.IsNaN(rect.width) || rect.width <= 0) return;

            var buffer = _spectrum.SpectrumBuffer;
            var len = _spectrum.SpectrumLength;
            if (len <= 0) return;

            // 背景
            painter.fillColor = new Color(0.05f, 0.08f, 0.12f, 0.8f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, 0));
            painter.LineTo(new Vector2(rect.width, 0));
            painter.LineTo(new Vector2(rect.width, rect.height));
            painter.LineTo(new Vector2(0, rect.height));
            painter.ClosePath();
            painter.Fill();

            // スペクトルバー
            var barWidth = rect.width / len;
            var gap = Mathf.Max(barWidth * 0.1f, 0.5f);
            var barDrawWidth = barWidth - gap;
            if (barDrawWidth < 0.5f) barDrawWidth = 0.5f;

            painter.fillColor = new Color(0.2f, 0.6f, 1f, 0.9f);
            for (var i = 0; i < len; i++)
            {
                var val = Mathf.Clamp01(buffer[i]);
                if (val < 0.001f) continue;

                var barHeight = val * rect.height;
                var x = i * barWidth + gap * 0.5f;
                var y = rect.height - barHeight;

                painter.BeginPath();
                painter.MoveTo(new Vector2(x, y));
                painter.LineTo(new Vector2(x + barDrawWidth, y));
                painter.LineTo(new Vector2(x + barDrawWidth, rect.height));
                painter.LineTo(new Vector2(x, rect.height));
                painter.ClosePath();
                painter.Fill();
            }
        }

        private void BuildInlineColorPicker(VisualElement root, NodeBase node)
        {
            if (node is not IInlineColorPicker picker) return;

            var container = new VisualElement();
            container.AddToClassList("inline-color-picker-container");

            // カラープレビュースウォッチ
            var swatch = new VisualElement();
            swatch.AddToClassList("inline-color-swatch");
            swatch.style.backgroundColor = picker.PickerColor;
            container.Add(swatch);

            // HSVスライダー（既存WorldPanelRayBridgeで動作確認済み）
            Color.RGBToHSV(picker.PickerColor, out var initH, out var initS, out var initV);

            var hSlider = CreateHsvSlider("H", initH, 0f, 1f);
            var sSlider = CreateHsvSlider("S", initS, 0f, 1f);
            var vSlider = CreateHsvSlider("V", initV, 0f, 1f);

            container.Add(hSlider.row);
            container.Add(sSlider.row);
            container.Add(vSlider.row);

            // 値変更時にカラー更新
            void OnHsvChanged(ChangeEvent<float> _)
            {
                var color = Color.HSVToRGB(hSlider.slider.value, sSlider.slider.value, vSlider.slider.value);
                picker.PickerColor = color;
                swatch.style.backgroundColor = color;
            }

            hSlider.slider.RegisterValueChangedCallback(OnHsvChanged);
            sSlider.slider.RegisterValueChangedCallback(OnHsvChanged);
            vSlider.slider.RegisterValueChangedCallback(OnHsvChanged);

            var portContainer = root.Q("port-container");
            if (portContainer?.parent != null)
            {
                var parent = portContainer.parent;
                var index = parent.IndexOf(portContainer);
                parent.Insert(index, container);
            }
            else
            {
                root.Add(container);
            }
        }

        private static (VisualElement row, Slider slider) CreateHsvSlider(
            string label, float initialValue, float min, float max)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var lbl = new Label(label);
            lbl.AddToClassList("inline-hsv-label");
            row.Add(lbl);

            var slider = new Slider(min, max);
            slider.value = initialValue;
            slider.AddToClassList("inline-hsv-slider");
            slider.style.flexGrow = 1;
            row.Add(slider);

            return (row, slider);
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
