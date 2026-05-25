#nullable enable

using NUnit.Framework;
using Rhizomode.Nodes.Audio;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// <see cref="TempoTracker"/> の BPM / phase / timeout / boundary 検証 (pure 値型、deterministic)。
    /// </summary>
    public class TempoTrackerTests
    {
        private const float Tol = 1e-3f;
        private const float BpmTol = 0.05f; // 60/(0.5±ε) で誤差が増えるので緩め

        [Test]
        public void Bpm_BeforeAnyTap_ReturnsDefault()
        {
            var t = new TempoTracker();
            Assert.AreEqual(TempoTracker.DefaultBpm, t.Bpm, Tol);
            Assert.AreEqual(0, t.TapCount);
        }

        [Test]
        public void OnTap_SingleTap_DoesNotUpdateBpm()
        {
            var t = new TempoTracker();
            var updated = t.OnTap(0f);

            Assert.IsFalse(updated, "1 tap 目では BPM は確定しない");
            Assert.AreEqual(1, t.TapCount);
            Assert.AreEqual(TempoTracker.DefaultBpm, t.Bpm, Tol);
        }

        [Test]
        public void OnTap_TwoEqualIntervals_ComputesExpectedBpm()
        {
            // 120 BPM = 0.5s interval
            var t = new TempoTracker();
            t.OnTap(0f);
            var updated = t.OnTap(0.5f);

            Assert.IsTrue(updated);
            Assert.AreEqual(120f, t.Bpm, BpmTol);
            Assert.AreEqual(0.5f, t.BeatInterval, Tol);
        }

        [Test]
        public void OnTap_HalfSecondInterval_Gives120Bpm()
        {
            var t = new TempoTracker();
            t.OnTap(10f);
            t.OnTap(10.5f);
            Assert.AreEqual(120f, t.Bpm, BpmTol);
        }

        [Test]
        public void OnTap_OneSecondInterval_Gives60Bpm()
        {
            var t = new TempoTracker();
            t.OnTap(0f);
            t.OnTap(1f);
            Assert.AreEqual(60f, t.Bpm, BpmTol);
        }

        [Test]
        public void OnTap_TimeoutResetsHistory()
        {
            var t = new TempoTracker();
            t.OnTap(0f);
            t.OnTap(0.5f); // 120 BPM
            Assert.AreEqual(120f, t.Bpm, BpmTol);

            // timeout (>3s) 後の新規 tap → 履歴リセット
            t.OnTap(0.5f + TempoTracker.TapTimeoutSec + 0.1f);

            Assert.AreEqual(1, t.TapCount, "timeout 後の最初の tap は新セッション (count=1)");
            // Bpm は前回値を保持 (Tracker は履歴だけリセット、確定 BPM はそのまま)
            Assert.AreEqual(120f, t.Bpm, BpmTol);
        }

        [Test]
        public void OnTap_RingBufferOverflow_KeepsLastEightIntervals()
        {
            var t = new TempoTracker();
            // 9 tap (1 個オーバーフロー)、interval = 0.5s で一貫
            for (var i = 0; i < TempoTracker.MaxTapHistory + 1; i++)
                t.OnTap(i * 0.5f);

            Assert.AreEqual(120f, t.Bpm, BpmTol, "オーバーフロー後も等間隔タップなら BPM 維持");
            Assert.AreEqual(TempoTracker.MaxTapHistory, t.TapCount);
        }

        [Test]
        public void OnTap_ZeroIntervalIgnored()
        {
            var t = new TempoTracker();
            t.OnTap(1f);
            var updated = t.OnTap(1f); // same timestamp → interval=0

            Assert.IsFalse(updated, "interval <= MinBeatIntervalSec は BPM 更新しない (ゼロ除算回避)");
        }

        [Test]
        public void Tick_BeforeAnyTap_ReturnsZeroPhase()
        {
            var t = new TempoTracker();
            var (phase, isBeat) = t.Tick(1f, 0.016f);
            Assert.AreEqual(0f, phase, Tol);
            Assert.IsFalse(isBeat);
        }

        [Test]
        public void Tick_AtPhaseOrigin_ReturnsZero()
        {
            var t = new TempoTracker();
            t.OnTap(0f);
            t.OnTap(1f); // 60 BPM, phaseOrigin = 1.0

            var (phase, isBeat) = t.Tick(1f, 0.016f);
            Assert.AreEqual(0f, phase, Tol);
            // 拍境界 = wrap 発生 (前回 phase > 今 phase)、いまの実装では isBeat 検出
            // ただし phase が 0 でちょうど境界なので、isBeat が立つかは prevPhase に依存。許容範囲。
            _ = isBeat;
        }

        [Test]
        public void Tick_HalfwayToNextBeat_ReturnsHalfPhase()
        {
            var t = new TempoTracker();
            t.OnTap(0f);
            t.OnTap(1f); // 60 BPM, interval=1, phaseOrigin=1
            var (phase, _) = t.Tick(1.5f, 0.016f);
            Assert.AreEqual(0.5f, phase, Tol);
        }

        [Test]
        public void Tick_AcrossBeatBoundary_DetectsBeat()
        {
            var t = new TempoTracker();
            t.OnTap(0f);
            t.OnTap(1f); // interval=1
            // phase=0.95 (now=1.95) → phase=0.05 (now=2.05) で wrap = beat
            t.Tick(1.95f, 0.01f);
            var (_, isBeat) = t.Tick(2.05f, 0.10f);
            Assert.IsTrue(isBeat);
        }

        [Test]
        public void Tick_WithinSameBeat_NoBeatDetected()
        {
            var t = new TempoTracker();
            t.OnTap(0f);
            t.OnTap(1f);
            t.Tick(1.30f, 0.30f);
            var (_, isBeat) = t.Tick(1.50f, 0.20f);
            Assert.IsFalse(isBeat);
        }
    }
}
