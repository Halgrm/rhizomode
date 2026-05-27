#nullable enable

using NUnit.Framework;
using Rhizomode.Audio.Analysis;
using Rhizomode.Audio.GraphAdapter;
using Rhizomode.Graph.Model;
using UnityEngine;

namespace Rhizomode.Audio.GraphAdapter.Tests
{
    /// <summary>
    /// <see cref="AudioDriverHost.ShouldDriveMonitors"/> の throttle 契約検証 (P2-B)。
    /// AudioMonitor / SpectrumMonitor の inline UI 描画を 30Hz に絞ることで、60+ ノード時の
    /// main thread 負荷を削減する設計。
    /// </summary>
    public sealed class AudioDriverHostMonitorThrottleTests
    {
        private GameObject? _analyzerGo;
        private GraphState? _graphState;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _analyzerGo = new GameObject("AudioAnalyzerHost");
            _analyzerGo.AddComponent<AudioAnalyzer>();
            _graphState = new GraphState();
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_graphState != null) { _graphState.Dispose(); _graphState = null; }
            if (_analyzerGo != null) { Object.DestroyImmediate(_analyzerGo); _analyzerGo = null; }
        }

        [Test]
        public void Default_Interval_IsApproximately30Hz()
        {
            var host = CreateHost();
            Assert.AreEqual(AudioDriverHost.DefaultMonitorUpdateIntervalSec,
                host.MonitorUpdateIntervalSec, 1e-6f);
        }

        [Test]
        public void FirstCall_AlwaysPushes()
        {
            var host = CreateHost();
            Assert.IsTrue(host.ShouldDriveMonitors(0f), "起動直後の最初の判定は push を許可する");
        }

        [Test]
        public void WithinInterval_DoesNotPush()
        {
            var host = CreateHost();
            host.MonitorUpdateIntervalSec = 0.1f; // 10Hz

            host.ShouldDriveMonitors(0f);  // 1st push, nextPushTime = 0.1
            Assert.IsFalse(host.ShouldDriveMonitors(0.05f),
                "interval 未満は push しない");
        }

        [Test]
        public void AfterInterval_PushesAgain()
        {
            var host = CreateHost();
            host.MonitorUpdateIntervalSec = 0.1f;

            host.ShouldDriveMonitors(0f);
            Assert.IsTrue(host.ShouldDriveMonitors(0.1f),
                "interval 経過丁度で push 許可 (now >= nextPushTime)");
        }

        [Test]
        public void IntervalZero_PushesEveryCall()
        {
            var host = CreateHost();
            host.MonitorUpdateIntervalSec = 0f;

            Assert.IsTrue(host.ShouldDriveMonitors(0f));
            Assert.IsTrue(host.ShouldDriveMonitors(0.001f));
            Assert.IsTrue(host.ShouldDriveMonitors(0.002f),
                "interval=0 は毎フレ駆動 (P2-B 前の挙動)");
        }

        [Test]
        public void NegativeInterval_FallsBackToDefault()
        {
            var host = CreateHost();
            host.MonitorUpdateIntervalSec = -1f;

            Assert.AreEqual(AudioDriverHost.DefaultMonitorUpdateIntervalSec,
                host.MonitorUpdateIntervalSec, 1e-6f);
        }

        [Test]
        public void NaNInterval_FallsBackToDefault()
        {
            var host = CreateHost();
            host.MonitorUpdateIntervalSec = float.NaN;

            Assert.AreEqual(AudioDriverHost.DefaultMonitorUpdateIntervalSec,
                host.MonitorUpdateIntervalSec, 1e-6f);
        }

        [Test]
        public void InfinityInterval_FallsBackToDefault()
        {
            var host = CreateHost();
            host.MonitorUpdateIntervalSec = float.PositiveInfinity;
            Assert.AreEqual(AudioDriverHost.DefaultMonitorUpdateIntervalSec,
                host.MonitorUpdateIntervalSec, 1e-6f);
        }

        [Test]
        public void NonFiniteNow_AlwaysPushes()
        {
            var host = CreateHost();
            host.MonitorUpdateIntervalSec = 0.1f;

            // 時刻が NaN/Inf に飛ぶ異常時は安全側に倒して描画は止めない
            Assert.IsTrue(host.ShouldDriveMonitors(float.NaN));
            Assert.IsTrue(host.ShouldDriveMonitors(float.PositiveInfinity));
        }

        [Test]
        public void TenHzSimulation_PushesTenTimesPerSecond()
        {
            var host = CreateHost();
            host.MonitorUpdateIntervalSec = 0.1f;

            var pushed = 0;
            for (var i = 0; i < 100; i++) // 0.01s 刻みで 1 秒分 (100 frames @ 100fps)
            {
                if (host.ShouldDriveMonitors(i * 0.01f)) pushed++;
            }

            // 0.0, 0.1, 0.2, ..., 0.9 で 10 回 push (i=0 + i=10..90 step 10) + 0 origin
            Assert.AreEqual(10, pushed, "10Hz throttle で 1 秒中 10 回 push");
        }

        private AudioDriverHost CreateHost()
        {
            var analyzer = _analyzerGo!.GetComponent<AudioAnalyzer>();
            return AudioDriverHost.CreateForTests(analyzer, _graphState!, () => _now);
        }
    }
}
