#nullable enable

using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.UI.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="NodeVisualController"/> の partial: inline widget (slider/button/monitor/waveform/spectrum/color picker) 構築。
    /// Phase 9 Round A で本体から分離。
    /// </summary>
    public partial class NodeVisualController
    {
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

            InsertAboveOrAppendPortContainer(root, container);
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

            InsertAboveOrAppendPortContainer(root, container);
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

            InsertAboveOrAppendPortContainer(root, container);
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

            InsertAboveOrAppendPortContainer(root, container);
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

            InsertAboveOrAppendPortContainer(root, container);
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

            InsertAboveOrAppendPortContainer(root, container);
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

        // ヘッダーとポートコンテナの間に挿入。port-container が見つからなければ root に append。
        private static void InsertAboveOrAppendPortContainer(VisualElement root, VisualElement container)
        {
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
    }
}
