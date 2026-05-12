#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// Master/Tempoスライダー＋トラックごとのVolumeスライダー＋Stopボタンを
    /// VR空間に表示する。グリッド下に配置し、レイ操作で値を変更する。
    /// プリミティブ型のみ扱い、OSC送信はGameBootstrapが仲介する（UI→ExternalInput依存禁止）。
    /// </summary>
    [RequireComponent(typeof(WorldPanelHost))]
    public class AbletonControlPanel : MonoBehaviour
    {
        private const int PanelTextureWidth = 640;
        private const int PanelTextureHeight = 400;
        private const float DefaultWorldWidth = 1.0f;
        private const float MinWorldWidth = 0.4f;
        // 15ms = 約66Hz。VR の手の動きを十分追従しつつ OSC バッファあふれを防ぐ
        private const float SendIntervalSec = 0.015f;
        private const float TempoMin = 20f;
        private const float TempoMax = 200f;
        // パネルテクスチャ高さがアスペクト計算で参照される (GameBootstrap.SpawnAbletonOuterFrame)
        public const float TextureAspectRatio = (float)PanelTextureHeight / PanelTextureWidth;

        [SerializeField] private VisualTreeAsset? panelUxml;
        [SerializeField] private StyleSheet? panelStyleSheet;

        [Header("Track Layout (グリッドと共有する間隔)")]
        [Tooltip("トラック1列あたりの幅 (m)。コントロールパネルの幅とグリッドのX間隔の両方に使われる。")]
        [SerializeField, Range(0.05f, 0.5f)] private float trackHorizontalSpacing = 0.18f;
        [Tooltip("シーン1段あたりの高さ (m)。グリッドのY間隔とパネル下方向の余白に使われる。")]
        [SerializeField, Range(0.05f, 0.5f)] private float sceneVerticalSpacing = 0.18f;

        public float TrackHorizontalSpacing => trackHorizontalSpacing;
        public float SceneVerticalSpacing => sceneVerticalSpacing;

        [Header("Macros")]
        [Tooltip("表示する Macro 数 (Live 11=8, Live 12=最大16)。")]
        [SerializeField, Range(1, 16)] private int macroCount = 15;

        public int MacroCount => macroCount;

        private WorldPanelHost? _panelHost;

        private Slider? _masterSlider;
        private Label? _masterValue;
        private Slider? _tempoSlider;
        private Label? _tempoValue;
        private VisualElement? _tracksRoot;
        private VisualElement? _macrosRoot;

        private readonly Dictionary<int, Slider> _trackSliders = new();
        private readonly Dictionary<int, RadialKnobElement> _macroKnobs = new();
        private readonly Dictionary<int, Label> _macroValueLabels = new();

        private float _lastMasterSentAt;
        private float _lastTempoSentAt;
        private readonly Dictionary<int, float> _lastTrackSentAt = new();
        private readonly Dictionary<int, float> _lastMacroSentAt = new();

        private Label? _macroTargetLabel;

        public event Action<float>? OnMasterVolumeChanged;
        public event Action<float>? OnTempoChanged;
        public event Action<int, float>? OnTrackVolumeChanged;
        public event Action<int>? OnTrackStopRequested;
        public event Action? OnPlayRequested;
        public event Action? OnStopRequested;
        public event Action<int, float>? OnMacroChanged;
        /// <summary>(trackDelta, deviceDelta) を引数に発火。GameBootstrap が範囲補正と再Bindを担当。</summary>
        public event Action<int, int>? OnMacroTargetChangeRequested;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            _panelHost = GetComponent<WorldPanelHost>();
            SetVisualActive(false);
        }

        /// <summary>
        /// トラック数に応じてグリッドを構築し、指定位置に配置する。
        /// </summary>
        public void Build(string[] trackNames, Vector3 origin, Quaternion facing, float worldWidth = DefaultWorldWidth)
        {
            EnsureInitialized();
            BuildTrackStrip(trackNames);

            var width = Mathf.Max(MinWorldWidth, worldWidth);
            var height = width * ((float)PanelTextureHeight / PanelTextureWidth);
            _panelHost?.Resize(width, height);

            transform.SetPositionAndRotation(origin, facing);
            SetVisualActive(true);
            IsVisible = true;
        }

        public void Hide()
        {
            if (!IsVisible) return;
            SetVisualActive(false);
            IsVisible = false;
        }

        /// <summary>
        /// 外部からの状態反映用。ChangeEventを発火させない（OSC再送ループ防止）。
        /// </summary>
        public void SetMasterVolume(float v)
        {
            EnsureInitialized();
            _masterSlider?.SetValueWithoutNotify(Mathf.Clamp01(v));
            UpdateMasterLabel(v);
        }

        public void SetTempo(float bpm)
        {
            EnsureInitialized();
            _tempoSlider?.SetValueWithoutNotify(Mathf.Clamp(bpm, TempoMin, TempoMax));
            UpdateTempoLabel(bpm);
        }

        public void SetTrackVolume(int track, float v)
        {
            EnsureInitialized();
            if (_trackSliders.TryGetValue(track, out var slider))
                slider.SetValueWithoutNotify(Mathf.Clamp01(v));
        }

        /// <summary>
        /// Macro セクションを再構築する。Live から取得した Macro メタを並べる。
        /// </summary>
        public void BuildMacroStrip(string[] names, float[] values, float[] mins, float[] maxs)
        {
            EnsureInitialized();
            if (_macrosRoot == null) return;

            _macrosRoot.Clear();
            _macroKnobs.Clear();
            _macroValueLabels.Clear();
            _lastMacroSentAt.Clear();

            var n = names.Length;
            for (var i = 0; i < n; i++)
            {
                BuildOneMacro(i, names[i], values[i], mins[i], maxs[i]);
            }
        }

        /// <summary>
        /// 外部 (Live 側 listener) からの値反映用。ChangeEvent を発火させない。
        /// </summary>
        public void SetMacroValue(int macroIndex, float value)
        {
            EnsureInitialized();
            if (_macroKnobs.TryGetValue(macroIndex, out var knob))
                knob.SetValueWithoutNotify(value);
            UpdateMacroValueLabel(macroIndex, value);
        }

        private void BuildOneMacro(int macroIndex, string name, float value, float min, float max)
        {
            if (_macrosRoot == null) return;

            var col = new VisualElement();
            col.AddToClassList("abl-macro");

            var nameLabel = new Label(TruncateName(name)) { name = $"macro-name-{macroIndex}" };
            nameLabel.AddToClassList("abl-macro__name");
            col.Add(nameLabel);

            var knob = new RadialKnobElement
            {
                name = $"macro-knob-{macroIndex}",
                lowValue = min,
                highValue = max
            };
            knob.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
            knob.AddToClassList("abl-macro__knob");
            knob.RegisterCallback<ChangeEvent<float>>(evt => HandleMacroChanged(macroIndex, evt.newValue));
            col.Add(knob);

            var valueLabel = new Label(FormatMacroValue(value)) { name = $"macro-value-{macroIndex}" };
            valueLabel.AddToClassList("abl-macro__value");
            col.Add(valueLabel);

            _macrosRoot.Add(col);
            _macroKnobs[macroIndex] = knob;
            _macroValueLabels[macroIndex] = valueLabel;
        }

        private void HandleMacroChanged(int macroIndex, float value)
        {
            UpdateMacroValueLabel(macroIndex, value);

            var last = _lastMacroSentAt.TryGetValue(macroIndex, out var t) ? t : 0f;
            if (Time.unscaledTime - last < SendIntervalSec) return;
            _lastMacroSentAt[macroIndex] = Time.unscaledTime;

            try { OnMacroChanged?.Invoke(macroIndex, value); }
            catch (Exception e) { Debug.LogError($"[AbletonControlPanel] Macro handler failed: {e.Message}"); }
        }

        private void UpdateMacroValueLabel(int macroIndex, float value)
        {
            if (_macroValueLabels.TryGetValue(macroIndex, out var label))
                label.text = FormatMacroValue(value);
        }

        private static string FormatMacroValue(float v)
        {
            // Live の標準 Macro 範囲は 0..127 (整数表示が読みやすい)。それ以外は小数点表示
            return Mathf.Abs(v) >= 10f ? v.ToString("0") : v.ToString("0.00");
        }

        private void EnsureInitialized()
        {
            if (_panelHost == null) _panelHost = GetComponent<WorldPanelHost>();
            if (_panelHost == null || panelUxml == null) return;

            if (!_panelHost.IsInitialized)
            {
                _panelHost.Initialize(panelUxml, panelStyleSheet, PanelTextureWidth, PanelTextureHeight);
                CacheTopElements();
            }
        }

        private void CacheTopElements()
        {
            var root = _panelHost?.Root;
            if (root == null) return;

            _masterSlider = root.Q<Slider>("master-slider");
            _masterValue = root.Q<Label>("master-value");
            _tempoSlider = root.Q<Slider>("tempo-slider");
            _tempoValue = root.Q<Label>("tempo-value");
            _tracksRoot = root.Q("tracks-root");
            _macrosRoot = root.Q("macros-root");

            if (_masterSlider != null)
                _masterSlider.RegisterCallback<ChangeEvent<float>>(evt => HandleMasterChanged(evt.newValue));

            if (_tempoSlider != null)
            {
                _tempoSlider.lowValue = TempoMin;
                _tempoSlider.highValue = TempoMax;
                _tempoSlider.RegisterCallback<ChangeEvent<float>>(evt => HandleTempoChanged(evt.newValue));
            }

            var playBtn = root.Q<Button>("play-btn");
            if (playBtn != null)
                playBtn.RegisterCallback<ClickEvent>(_ => HandlePlay());

            var stopBtn = root.Q<Button>("stop-btn");
            if (stopBtn != null)
                stopBtn.RegisterCallback<ClickEvent>(_ => HandleStop());

            _macroTargetLabel = root.Q<Label>("macro-target-label");

            var trackPrev = root.Q<Button>("track-prev");
            if (trackPrev != null)
                trackPrev.RegisterCallback<ClickEvent>(_ => RaiseTargetChange(-1, 0));

            var trackNext = root.Q<Button>("track-next");
            if (trackNext != null)
                trackNext.RegisterCallback<ClickEvent>(_ => RaiseTargetChange(+1, 0));

            var devicePrev = root.Q<Button>("device-prev");
            if (devicePrev != null)
                devicePrev.RegisterCallback<ClickEvent>(_ => RaiseTargetChange(0, -1));

            var deviceNext = root.Q<Button>("device-next");
            if (deviceNext != null)
                deviceNext.RegisterCallback<ClickEvent>(_ => RaiseTargetChange(0, +1));
        }

        private void RaiseTargetChange(int trackDelta, int deviceDelta)
        {
            try { OnMacroTargetChangeRequested?.Invoke(trackDelta, deviceDelta); }
            catch (Exception e) { Debug.LogError($"[AbletonControlPanel] Target change handler failed: {e.Message}"); }
        }

        /// <summary>
        /// 現在の Macro 対象 (Track index / Device index) をラベルに表示する。
        /// Track -1 は "M" (Master) と表示。
        /// </summary>
        public void SetMacroTargetLabel(int trackIndex, int deviceIndex)
        {
            EnsureInitialized();
            if (_macroTargetLabel == null) return;
            var trackText = trackIndex < 0 ? "M" : $"T{trackIndex}";
            _macroTargetLabel.text = $"{trackText} / D{deviceIndex}";
        }

        private void BuildTrackStrip(string[] trackNames)
        {
            if (_tracksRoot == null) return;
            _tracksRoot.Clear();
            _trackSliders.Clear();
            _lastTrackSentAt.Clear();

            for (var i = 0; i < trackNames.Length; i++)
            {
                BuildOneTrack(i, trackNames[i]);
            }
        }

        private void BuildOneTrack(int trackIndex, string trackName)
        {
            if (_tracksRoot == null) return;

            var col = new VisualElement();
            col.AddToClassList("abl-track");

            var nameLabel = new Label(TruncateName(trackName)) { name = $"track-name-{trackIndex}" };
            nameLabel.AddToClassList("abl-track__name");
            col.Add(nameLabel);

            var slider = new Slider(0f, 1f) { name = $"track-slider-{trackIndex}", value = 0.85f };
            slider.direction = SliderDirection.Vertical;
            slider.AddToClassList("abl-track__slider");
            slider.RegisterCallback<ChangeEvent<float>>(evt => HandleTrackChanged(trackIndex, evt.newValue));
            col.Add(slider);

            var stopBtn = new Button { name = $"track-stop-{trackIndex}", text = "Stop" };
            stopBtn.AddToClassList("abl-track__stop");
            stopBtn.RegisterCallback<ClickEvent>(_ => HandleTrackStop(trackIndex));
            col.Add(stopBtn);

            _tracksRoot.Add(col);
            _trackSliders[trackIndex] = slider;
        }

        private void HandleMasterChanged(float value)
        {
            UpdateMasterLabel(value);
            if (!RateLimit(ref _lastMasterSentAt)) return;
            try { OnMasterVolumeChanged?.Invoke(value); }
            catch (Exception e) { Debug.LogError($"[AbletonControlPanel] Master handler failed: {e.Message}"); }
        }

        private void HandleTempoChanged(float value)
        {
            UpdateTempoLabel(value);
            if (!RateLimit(ref _lastTempoSentAt)) return;
            try { OnTempoChanged?.Invoke(value); }
            catch (Exception e) { Debug.LogError($"[AbletonControlPanel] Tempo handler failed: {e.Message}"); }
        }

        private void HandleTrackChanged(int trackIndex, float value)
        {
            var last = _lastTrackSentAt.TryGetValue(trackIndex, out var t) ? t : 0f;
            if (Time.unscaledTime - last < SendIntervalSec) return;
            _lastTrackSentAt[trackIndex] = Time.unscaledTime;

            try { OnTrackVolumeChanged?.Invoke(trackIndex, value); }
            catch (Exception e) { Debug.LogError($"[AbletonControlPanel] Track handler failed: {e.Message}"); }
        }

        private void HandleTrackStop(int trackIndex)
        {
            try { OnTrackStopRequested?.Invoke(trackIndex); }
            catch (Exception e) { Debug.LogError($"[AbletonControlPanel] Stop handler failed: {e.Message}"); }
        }

        private void HandlePlay()
        {
            try { OnPlayRequested?.Invoke(); }
            catch (Exception e) { Debug.LogError($"[AbletonControlPanel] Play handler failed: {e.Message}"); }
        }

        private void HandleStop()
        {
            try { OnStopRequested?.Invoke(); }
            catch (Exception e) { Debug.LogError($"[AbletonControlPanel] Stop handler failed: {e.Message}"); }
        }

        private bool RateLimit(ref float lastSentAt)
        {
            var now = Time.unscaledTime;
            if (now - lastSentAt < SendIntervalSec) return false;
            lastSentAt = now;
            return true;
        }

        private void UpdateMasterLabel(float v)
        {
            if (_masterValue != null) _masterValue.text = v.ToString("0.00");
        }

        private void UpdateTempoLabel(float bpm)
        {
            if (_tempoValue != null) _tempoValue.text = bpm.ToString("0");
        }

        private static string TruncateName(string name)
        {
            const int maxChars = 6;
            if (string.IsNullOrEmpty(name)) return "--";
            return name.Length <= maxChars ? name : name.Substring(0, maxChars - 1) + "…";
        }

        private void SetVisualActive(bool active)
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.enabled = active;

            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = active;
        }
    }
}
