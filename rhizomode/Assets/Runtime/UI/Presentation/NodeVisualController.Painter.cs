#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="NodeVisualController"/> の partial: waveform / spectrum 描画 (generateVisualContent コールバック)。
    /// Phase 9 Round A で本体から分離。
    /// </summary>
    public partial class NodeVisualController
    {
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
    }
}
