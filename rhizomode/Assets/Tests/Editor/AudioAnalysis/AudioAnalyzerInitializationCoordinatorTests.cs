#nullable enable

using NUnit.Framework;
using Rhizomode.Audio.Analysis;

namespace Rhizomode.Audio.Analysis.Tests
{
    public sealed class AudioAnalyzerInitializationCoordinatorTests
    {
        [Test]
        public void RequestInitialize_EmptyDevice_IsNoOp()
        {
            var coordinator = new AudioAnalyzerInitializationCoordinator();

            var action = coordinator.RequestInitialize("", false, null, 0);

            Assert.AreEqual(AudioAnalyzerInitializeAction.None, action);
            Assert.IsFalse(coordinator.IsTransitionActive);
        }

        [Test]
        public void RequestInitialize_Uninitialized_StartsImmediately()
        {
            var coordinator = new AudioAnalyzerInitializationCoordinator();

            var action = coordinator.RequestInitialize("A", false, null, 0);

            Assert.AreEqual(AudioAnalyzerInitializeAction.InitializeNow, action);
            Assert.IsFalse(coordinator.IsTransitionActive);
        }

        [Test]
        public void RequestInitialize_SameRunningDevice_IsNoOp()
        {
            var coordinator = new AudioAnalyzerInitializationCoordinator();

            var action = coordinator.RequestInitialize("A", true, "A", 0);

            Assert.AreEqual(AudioAnalyzerInitializeAction.None, action);
            Assert.IsFalse(coordinator.IsTransitionActive);
        }

        [Test]
        public void RequestInitialize_DifferentRunningDevice_RequestsShutdown()
        {
            var coordinator = new AudioAnalyzerInitializationCoordinator();

            var action = coordinator.RequestInitialize("B", true, "A", 10);

            Assert.AreEqual(AudioAnalyzerInitializeAction.ShutdownBeforePending, action);
            Assert.IsTrue(coordinator.IsTransitionActive);
            Assert.AreEqual("B", coordinator.PendingDevice);
        }

        [Test]
        public void RequestInitialize_DuringShutdown_SamePending_IsNoOp()
        {
            var coordinator = BeginSwitch("B");

            var action = coordinator.RequestInitialize("B", false, null, 11);

            Assert.AreEqual(AudioAnalyzerInitializeAction.None, action);
            Assert.AreEqual("B", coordinator.PendingDevice);
        }

        [Test]
        public void RequestInitialize_DuringShutdown_LastWriteWins()
        {
            var coordinator = BeginSwitch("B");

            var action = coordinator.RequestInitialize("C", false, null, 11);

            Assert.AreEqual(AudioAnalyzerInitializeAction.None, action);
            Assert.AreEqual("C", coordinator.PendingDevice);
        }

        [Test]
        public void RequestUpdate_BeforeDelay_DoesNotInitializePending()
        {
            var coordinator = BeginSwitch("B");

            var (action, deviceName) = coordinator.RequestUpdate(false, 10);

            Assert.AreEqual(AudioAnalyzerUpdateAction.WaitForNextFrame, action);
            Assert.IsNull(deviceName);
            Assert.IsTrue(coordinator.IsTransitionActive);
        }

        [Test]
        public void RequestUpdate_AfterDelay_ConsumesPendingDevice()
        {
            var coordinator = BeginSwitch("B");
            var readyFrame = AudioAnalyzerInitializationCoordinator.MinSwitchDelayFrames;

            var (action, deviceName) = coordinator.RequestUpdate(false, readyFrame);

            Assert.AreEqual(AudioAnalyzerUpdateAction.InitializePending, action);
            Assert.AreEqual("B", deviceName);
            Assert.IsFalse(coordinator.IsTransitionActive);
            Assert.IsNull(coordinator.PendingDevice);
        }

        [Test]
        public void RequestUpdate_InconsistentShutdown_ClearsOnce()
        {
            var coordinator = new AudioAnalyzerInitializationCoordinator();
            coordinator.BeginShutdown(0);

            var first = coordinator.RequestUpdate(false, 1);
            var second = coordinator.RequestUpdate(false, 2);

            Assert.AreEqual(AudioAnalyzerUpdateAction.ClearInconsistent, first.action);
            Assert.AreEqual(AudioAnalyzerUpdateAction.None, second.action);
            Assert.IsFalse(coordinator.IsTransitionActive);
        }

        [Test]
        public void RequestUpdate_LiveCaptureDuringPending_RequiresShutdownFirst()
        {
            var coordinator = BeginSwitch("B");
            var readyFrame = AudioAnalyzerInitializationCoordinator.MinSwitchDelayFrames;

            var (action, deviceName) = coordinator.RequestUpdate(true, readyFrame);

            Assert.AreEqual(AudioAnalyzerUpdateAction.ShutdownBeforePending, action);
            Assert.IsNull(deviceName);
            Assert.AreEqual("B", coordinator.PendingDevice);
        }

        [Test]
        public void RequestInitialize_EightStateCombinations_AreSafe()
        {
            for (var hasCapture = 0; hasCapture <= 1; hasCapture++)
            for (var shutting = 0; shutting <= 1; shutting++)
            for (var hasPending = 0; hasPending <= 1; hasPending++)
                AssertStateCombinationIsSafe(hasCapture == 1, shutting == 1, hasPending == 1);
        }

        private static AudioAnalyzerInitializationCoordinator BeginSwitch(string deviceName)
        {
            var coordinator = new AudioAnalyzerInitializationCoordinator();
            coordinator.RequestInitialize(deviceName, true, "A", 0);
            return coordinator;
        }

        private static void AssertStateCombinationIsSafe(
            bool hasCapture,
            bool isShuttingDown,
            bool hasPending)
        {
            var coordinator = new AudioAnalyzerInitializationCoordinator();
            coordinator.ForceStateForTests(isShuttingDown, hasPending ? "B" : null, 0);

            var action = coordinator.RequestInitialize("C", hasCapture, "A", 1);

            if (isShuttingDown)
                Assert.AreEqual(AudioAnalyzerInitializeAction.None, action,
                    "shutdown state should not request a second parallel shutdown");
            if (isShuttingDown)
                Assert.AreEqual("C", coordinator.PendingDevice);
            if (!isShuttingDown && !hasCapture)
                Assert.IsNull(coordinator.PendingDevice);
        }
    }
}
