#nullable enable

using System;
using Rhizomode.Ableton.Session;
using TMPro;
using UnityEngine;

namespace Rhizomode.Ableton.GraphAdapter
{
    /// <summary>
    /// 拡張用スタイル指定。クリップごとに見た目を上書きしたい場合に注入。
    /// </summary>
    public struct ClipObjectStyle
    {
        public Color BaseColor;
        public float EmissionScale;
        public int LabelMaxChars;
    }

    /// <summary>
    /// AbletonクリップスロットのVR物理表現。1キューブ = 1クリップ。
    /// レイヒット可能なBoxCollider付き。状態遷移はMaterialPropertyBlockで反映。
    /// </summary>
    [DisallowMultipleComponent]
    public class ClipObject : MonoBehaviour
    {
        [SerializeField] private MeshRenderer? cubeRenderer;
        [SerializeField] private TMP_Text? label;
        [SerializeField, Range(0.05f, 1.0f)] private float pulseDuration = 0.3f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // Monochrome palette: empty(枠線のみ的に薄い), stopped(中間グレー), playing(白)
        private static readonly Color EmptyColor = new(0.15f, 0.15f, 0.15f);
        private static readonly Color StoppedColor = new(0.45f, 0.45f, 0.45f);
        private static readonly Color PlayingColor = new(1f, 1f, 1f);

        private MaterialPropertyBlock? _mpb;
        private float _emissionScale = 1f;
        private bool _isPlaying;
        private float _pulseUntil;

        public int TrackIndex { get; private set; }
        public int SceneIndex { get; private set; }
        public bool HasClip { get; private set; }

        /// <summary>
        /// クリップ情報で初期化する。GridManagerから1度だけ呼ぶ。
        /// </summary>
        public void Initialize(int track, int scene, AbletonClipMeta meta,
            string trackName, ClipObjectStyle? styleOverride = null)
        {
            TrackIndex = track;
            SceneIndex = scene;
            HasClip = meta.HasClip;

            var maxChars = styleOverride?.LabelMaxChars ?? 4;
            _emissionScale = styleOverride?.EmissionScale ?? 1f;

            if (label != null)
                label.text = BuildLabelText(meta.Name, trackName, meta.HasClip, maxChars);

            _mpb = new MaterialPropertyBlock();
            ApplyVisualState();
        }

        /// <summary>
        /// クリップの再生状態を設定する。Liveからのis_playing応答で呼ばれる。
        /// </summary>
        public void SetPlayingState(bool isPlaying)
        {
            if (_isPlaying == isPlaying) return;
            _isPlaying = isPlaying;
            ApplyVisualState();
        }

        /// <summary>
        /// 発火送信時のパルスアニメを開始する。
        /// </summary>
        public void OnTriggered()
        {
            _pulseUntil = Time.time + pulseDuration;
            ApplyVisualState();
        }

        private void Update()
        {
            if (_pulseUntil > 0f)
            {
                if (Time.time > _pulseUntil)
                {
                    _pulseUntil = 0f;
                    ApplyVisualState();
                }
                else
                {
                    ApplyPulseFrame();
                }
            }
        }

        private void ApplyVisualState()
        {
            if (cubeRenderer == null || _mpb == null) return;

            Color baseCol;
            Color emissionCol;

            if (!HasClip)
            {
                baseCol = EmptyColor;
                emissionCol = Color.black;
            }
            else if (_isPlaying)
            {
                baseCol = PlayingColor;
                emissionCol = PlayingColor * (0.4f * _emissionScale);
            }
            else
            {
                baseCol = StoppedColor;
                emissionCol = Color.black;
            }

            _mpb.SetColor(BaseColorId, baseCol);
            _mpb.SetColor(EmissionColorId, emissionCol);
            cubeRenderer.SetPropertyBlock(_mpb);
        }

        private void ApplyPulseFrame()
        {
            if (cubeRenderer == null || _mpb == null) return;

            var remaining = Mathf.Max(0f, _pulseUntil - Time.time);
            var t = remaining / Mathf.Max(0.0001f, pulseDuration);   // 1→0
            var emissionStrength = Mathf.Lerp(0.4f, 1.6f, t);

            _mpb.SetColor(BaseColorId, PlayingColor);
            _mpb.SetColor(EmissionColorId, PlayingColor * (emissionStrength * _emissionScale));
            cubeRenderer.SetPropertyBlock(_mpb);
        }

        private static string BuildLabelText(string clipName, string trackName, bool hasClip, int maxChars)
        {
            if (!hasClip) return string.Empty;

            var clipShort = TruncateHead(clipName, maxChars);
            var trackShort = TruncateHead(trackName, maxChars);
            if (string.IsNullOrEmpty(clipShort) && string.IsNullOrEmpty(trackShort))
                return string.Empty;
            if (string.IsNullOrEmpty(trackShort))
                return clipShort;
            if (string.IsNullOrEmpty(clipShort))
                return trackShort;
            return $"{clipShort}\n<size=60%>{trackShort}</size>";
        }

        private static string TruncateHead(string s, int maxChars)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.Length <= maxChars) return s;
            return s.Substring(0, maxChars) + "…";
        }
    }
}
