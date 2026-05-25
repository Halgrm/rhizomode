#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using R3;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Audio;
using Rhizomode.SharedKernel;
using Rhizomode.UI.Contracts;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// AudioBand / AudioMonitor / SpectrumMonitor 3 ノードの sanitize / buffer 入力契約。
    /// </summary>
    public class AudioBandAndMonitorNodeTests
    {
        private const float Tol = 1e-5f;

        // ---------- AudioBand ----------

        [Test]
        public void AudioBand_SetBandLevels_EmitsFourOutputs()
        {
            var node = new AudioBandNode("n1");
            using var disposables = new CompositeDisposable();
            var (level, lo, mid, hi) = SubscribeBand(node, disposables);

            node.SetBandLevels(0.4f, 0.1f, 0.2f, 0.3f);

            Assert.AreEqual(0.4f, level[0], Tol);
            Assert.AreEqual(0.1f, lo[0], Tol);
            Assert.AreEqual(0.2f, mid[0], Tol);
            Assert.AreEqual(0.3f, hi[0], Tol);
        }

        [Test]
        public void AudioBand_NaN_SanitizedToZero()
        {
            var node = new AudioBandNode("n1");
            using var disposables = new CompositeDisposable();
            var (level, lo, mid, hi) = SubscribeBand(node, disposables);

            node.SetBandLevels(float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.NaN);

            Assert.AreEqual(0f, level[0], Tol);
            Assert.AreEqual(0f, lo[0], Tol);
            Assert.AreEqual(0f, mid[0], Tol);
            Assert.AreEqual(0f, hi[0], Tol);
        }

        // ---------- AudioMonitor ----------

        [Test]
        public void AudioMonitor_SetLevel_EmitsSanitized()
        {
            var node = new AudioMonitorNode("n1");
            var port = (OutputPort<float>)node.GetOutputPort("Level")!;
            var levels = new List<float>();
            using var subscription = port.Observable.Subscribe(v => levels.Add(v));

            node.SetLevel(float.NaN);
            node.SetLevel(float.PositiveInfinity);
            node.SetLevel(float.NegativeInfinity);
            node.SetLevel(0.6f);

            CollectionAssert.AreEqual(new[] { 0f, 0f, 0f, 0.6f }, levels);
        }

        [Test]
        public void AudioMonitor_SetWaveform_NullIsNoOp()
        {
            var node = new AudioMonitorNode("n1");
            Assert.DoesNotThrow(() => node.SetWaveform(null!));

            var waveform = (IInlineWaveform)node;
            Assert.IsNotNull(waveform.WaveformBuffer);
            Assert.AreEqual(64, waveform.WaveformLength);
        }

        [Test]
        public void AudioMonitor_SetWaveform_ShorterThanBuffer_CopiesPrefix()
        {
            var node = new AudioMonitorNode("n1");
            var input = new[] { 0.1f, 0.2f, 0.3f };
            node.SetWaveform(input);

            var waveform = (IInlineWaveform)node;
            var buf = waveform.WaveformBuffer!;
            Assert.AreEqual(0.1f, buf[0], Tol);
            Assert.AreEqual(0.2f, buf[1], Tol);
            Assert.AreEqual(0.3f, buf[2], Tol);
            Assert.AreEqual(3, waveform.WaveformWriteIndex,
                "短い入力時、WriteIndex は入力長 mod buffer size");
        }

        [Test]
        public void AudioMonitor_SetWaveform_EmptyInput_SetsWriteIndexZero()
        {
            var node = new AudioMonitorNode("n1");

            Assert.DoesNotThrow(() => node.SetWaveform(System.Array.Empty<float>()));

            var waveform = (IInlineWaveform)node;
            Assert.AreEqual(0, waveform.WaveformWriteIndex);
        }

        [Test]
        public void AudioMonitor_SetWaveform_LongerThanBuffer_TruncatedAt64()
        {
            var node = new AudioMonitorNode("n1");
            var input = new float[128];
            for (var i = 0; i < input.Length; i++) input[i] = i * 0.01f;

            Assert.DoesNotThrow(() => node.SetWaveform(input));

            var waveform = (IInlineWaveform)node;
            var buf = waveform.WaveformBuffer!;
            Assert.AreEqual(0f, buf[0], Tol);
            Assert.AreEqual(63 * 0.01f, buf[63], Tol);
            Assert.AreEqual(0, waveform.WaveformWriteIndex);
        }

        [Test]
        public void AudioMonitor_WaveformLabel_ReflectsLastLevel()
        {
            var node = new AudioMonitorNode("n1");
            node.SetLevel(0.123f);

            var waveform = (IInlineWaveform)node;
            Assert.AreEqual("0.123", waveform.WaveformLabel);
        }

        [Test]
        public void AudioMonitor_WaveformVersion_IncrementsOnSetWaveform()
        {
            var node = new AudioMonitorNode("n1");
            var waveform = (IInlineWaveform)node;
            var initial = waveform.WaveformVersion;

            node.SetWaveform(new[] { 0.1f, 0.2f, 0.3f });
            var afterFirst = waveform.WaveformVersion;
            node.SetWaveform(new[] { 0.4f, 0.5f });
            var afterSecond = waveform.WaveformVersion;

            Assert.AreNotEqual(initial, afterFirst,
                "P2-B: SetWaveform で version が変化することで NodeVisualController が repaint trigger 出来る");
            Assert.AreNotEqual(afterFirst, afterSecond, "後続の SetWaveform でも version が進む");
        }

        [Test]
        public void AudioMonitor_WaveformVersion_NoChangeOnSetLevelAlone()
        {
            var node = new AudioMonitorNode("n1");
            var waveform = (IInlineWaveform)node;
            var initial = waveform.WaveformVersion;

            node.SetLevel(0.5f);

            Assert.AreEqual(initial, waveform.WaveformVersion,
                "SetLevel のみでは波形 version は進まない (waveform repaint を誘発しない)");
        }

        // ---------- SpectrumMonitor ----------

        [Test]
        public void SpectrumMonitor_SetSpectrum_NullIsNoOp()
        {
            var node = new SpectrumMonitorNode("n1");
            Assert.DoesNotThrow(() => node.SetSpectrum(null!));

            var spec = (IInlineSpectrum)node;
            Assert.IsNotNull(spec.SpectrumBuffer);
            Assert.AreEqual(64, spec.SpectrumLength);
        }

        [Test]
        public void SpectrumMonitor_SetSpectrum_ShorterThanBuffer_PadsRemainderWithZero()
        {
            var node = new SpectrumMonitorNode("n1");

            // まず full でロード (残端確認用)
            var full = new float[64];
            for (var i = 0; i < 64; i++) full[i] = 1f;
            node.SetSpectrum(full);

            // 短い入力で上書き → 残端は 0 にパディングされる
            var partial = new[] { 0.5f, 0.5f, 0.5f };
            node.SetSpectrum(partial);

            var spec = (IInlineSpectrum)node;
            var buf = spec.SpectrumBuffer!;
            Assert.AreEqual(0.5f, buf[0], Tol);
            Assert.AreEqual(0.5f, buf[2], Tol);
            Assert.AreEqual(0f, buf[3], Tol, "余分な領域は 0 padding (stale データを残さない)");
            Assert.AreEqual(0f, buf[63], Tol);
        }

        [Test]
        public void SpectrumMonitor_SetSpectrum_EmptyInput_PadsAllWithZero()
        {
            var node = new SpectrumMonitorNode("n1");
            var full = new float[64];
            for (var i = 0; i < 64; i++) full[i] = 1f;
            node.SetSpectrum(full);

            Assert.DoesNotThrow(() => node.SetSpectrum(System.Array.Empty<float>()));

            var spec = (IInlineSpectrum)node;
            var buf = spec.SpectrumBuffer!;
            Assert.AreEqual(0f, buf[0], Tol);
            Assert.AreEqual(0f, buf[63], Tol);
        }

        [Test]
        public void SpectrumMonitor_SetLevel_NaNSanitized()
        {
            var node = new SpectrumMonitorNode("n1");
            var port = (OutputPort<float>)node.GetOutputPort("Level")!;
            var levels = new List<float>();
            using var subscription = port.Observable.Subscribe(v => levels.Add(v));

            node.SetLevel(float.NaN);
            node.SetLevel(float.PositiveInfinity);
            node.SetLevel(float.NegativeInfinity);
            node.SetLevel(0.4f);

            CollectionAssert.AreEqual(new[] { 0f, 0f, 0f, 0.4f }, levels);
        }

        [Test]
        public void SpectrumMonitor_SpectrumVersion_IncrementsOnSetSpectrum()
        {
            var node = new SpectrumMonitorNode("n1");
            var spec = (IInlineSpectrum)node;
            var initial = spec.SpectrumVersion;

            node.SetSpectrum(new[] { 0.1f, 0.2f });
            var afterFirst = spec.SpectrumVersion;
            node.SetSpectrum(new[] { 0.3f });
            var afterSecond = spec.SpectrumVersion;

            Assert.AreNotEqual(initial, afterFirst,
                "P2-B: SetSpectrum で version が変化することで NodeVisualController が repaint trigger 出来る");
            Assert.AreNotEqual(afterFirst, afterSecond);
        }

        [Test]
        public void SpectrumMonitor_SpectrumVersion_NoChangeOnSetLevelAlone()
        {
            var node = new SpectrumMonitorNode("n1");
            var spec = (IInlineSpectrum)node;
            var initial = spec.SpectrumVersion;

            node.SetLevel(0.5f);

            Assert.AreEqual(initial, spec.SpectrumVersion,
                "SetLevel のみでは spectrum version は進まない");
        }

        // ---------- helpers ----------

        private static (List<float> level, List<float> lo, List<float> mid, List<float> hi)
            SubscribeBand(AudioBandNode node, CompositeDisposable disposables)
        {
            var l = new List<float>();
            var lo = new List<float>();
            var mid = new List<float>();
            var hi = new List<float>();
            disposables.Add(((OutputPort<float>)node.GetOutputPort("Level")!).Observable.Subscribe(v => l.Add(v)));
            disposables.Add(((OutputPort<float>)node.GetOutputPort("Low")!).Observable.Subscribe(v => lo.Add(v)));
            disposables.Add(((OutputPort<float>)node.GetOutputPort("Mid")!).Observable.Subscribe(v => mid.Add(v)));
            disposables.Add(((OutputPort<float>)node.GetOutputPort("High")!).Observable.Subscribe(v => hi.Add(v)));
            return (l, lo, mid, hi);
        }
    }
}
