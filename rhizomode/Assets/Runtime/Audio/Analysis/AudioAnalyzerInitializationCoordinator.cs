#nullable enable

using System;

namespace Rhizomode.Audio.Analysis
{
    internal sealed class AudioAnalyzerInitializationCoordinator
    {
        // LASP closes an inactive InputDeviceHandle stream when its sleep timer exceeds 10 updates.
        internal const int MinSwitchDelayFrames = 11;

        private int _shutdownFrame = -1;

        internal string? PendingDevice { get; private set; }
        internal bool IsShuttingDown { get; private set; }
        internal bool IsTransitionActive => IsShuttingDown;

        internal AudioAnalyzerInitializeAction RequestInitialize(
            string? deviceName,
            bool hasCapture,
            string? currentDevice,
            int frameCount)
        {
            if (string.IsNullOrEmpty(deviceName))
                return AudioAnalyzerInitializeAction.None;
            ClearStalePending();
            if (IsShuttingDown)
                return RequestDuringShutdown(deviceName);
            if (hasCapture && IsSameDevice(currentDevice, deviceName))
                return AudioAnalyzerInitializeAction.None;
            if (!hasCapture)
                return AudioAnalyzerInitializeAction.InitializeNow;

            PendingDevice = deviceName;
            BeginShutdown(frameCount);
            return AudioAnalyzerInitializeAction.ShutdownBeforePending;
        }

        internal (AudioAnalyzerUpdateAction action, string? deviceName) RequestUpdate(
            bool hasCapture,
            int frameCount)
        {
            if (!IsShuttingDown)
                return (AudioAnalyzerUpdateAction.None, null);
            if (PendingDevice == null)
                return ClearInconsistent();
            if (hasCapture)
                return (AudioAnalyzerUpdateAction.ShutdownBeforePending, null);
            if (!HasSwitchDelayElapsed(frameCount))
                return (AudioAnalyzerUpdateAction.WaitForNextFrame, null);

            var deviceName = PendingDevice;
            Clear();
            return (AudioAnalyzerUpdateAction.InitializePending, deviceName);
        }

        internal void BeginShutdown(int frameCount)
        {
            IsShuttingDown = true;
            _shutdownFrame = frameCount;
        }

        internal void Clear()
        {
            PendingDevice = null;
            IsShuttingDown = false;
            _shutdownFrame = -1;
        }

        internal void ForceStateForTests(
            bool isShuttingDown,
            string? pendingDevice,
            int shutdownFrame)
        {
            IsShuttingDown = isShuttingDown;
            PendingDevice = pendingDevice;
            _shutdownFrame = shutdownFrame;
        }

        private AudioAnalyzerInitializeAction RequestDuringShutdown(string deviceName)
        {
            if (IsSameDevice(PendingDevice, deviceName))
                return AudioAnalyzerInitializeAction.None;

            // Device selection is a final-state choice; replaying obsolete devices only adds glitches.
            PendingDevice = deviceName;
            return AudioAnalyzerInitializeAction.None;
        }

        private (AudioAnalyzerUpdateAction action, string? deviceName) ClearInconsistent()
        {
            Clear();
            return (AudioAnalyzerUpdateAction.ClearInconsistent, null);
        }

        private void ClearStalePending()
        {
            if (!IsShuttingDown)
                PendingDevice = null;
        }

        private bool HasSwitchDelayElapsed(int frameCount)
        {
            return _shutdownFrame < 0 || frameCount - _shutdownFrame >= MinSwitchDelayFrames;
        }

        private static bool IsSameDevice(string? left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}
