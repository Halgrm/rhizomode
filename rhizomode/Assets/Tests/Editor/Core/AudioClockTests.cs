#nullable enable

using NUnit.Framework;
using Rhizomode.Audio.Contracts;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// <see cref="AudioClock"/> の latency offset + NowProvider 差し替え契約。
    /// </summary>
    public class AudioClockTests
    {
        private const float Tol = 1e-5f;

        [SetUp] public void SetUp() => AudioClock.ResetForTest();
        [TearDown] public void TearDown() => AudioClock.ResetForTest();

        [Test]
        public void LatencyOffset_DefaultsToZero()
        {
            Assert.AreEqual(0f, AudioClock.LatencyOffsetSeconds, Tol);
        }

        [Test]
        public void LatencyOffset_SetValue_AppliesToNow()
        {
            AudioClock.NowProvider = () => 10f;
            AudioClock.LatencyOffsetSeconds = 0.05f; // 50ms

            Assert.AreEqual(9.95f, AudioClock.Now, Tol);
        }

        [Test]
        public void LatencyOffset_NaNCoercedToZero()
        {
            AudioClock.LatencyOffsetSeconds = float.NaN;
            Assert.AreEqual(0f, AudioClock.LatencyOffsetSeconds, Tol);
        }

        [Test]
        public void LatencyOffset_InfinityCoercedToZero()
        {
            AudioClock.LatencyOffsetSeconds = float.PositiveInfinity;
            Assert.AreEqual(0f, AudioClock.LatencyOffsetSeconds, Tol);

            AudioClock.LatencyOffsetSeconds = float.NegativeInfinity;
            Assert.AreEqual(0f, AudioClock.LatencyOffsetSeconds, Tol);
        }

        [Test]
        public void NowProvider_OverrideReturnsCustomValue()
        {
            AudioClock.NowProvider = () => 42f;
            Assert.AreEqual(42f, AudioClock.Now, Tol);
        }

        [Test]
        public void ResetForTest_RestoresDefaults()
        {
            AudioClock.LatencyOffsetSeconds = 0.1f;
            AudioClock.NowProvider = () => 999f;

            AudioClock.ResetForTest();

            Assert.AreEqual(0f, AudioClock.LatencyOffsetSeconds, Tol);
            // NowProvider が Time.unscaledTime に戻る (EditMode では 0 になる)
            Assert.DoesNotThrow(() => { var _ = AudioClock.Now; });
        }
    }
}
