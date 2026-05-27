#nullable enable

using System.Collections.Generic;
using Rhizomode.Observability.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// NDI receiver presentation health monitor.
    /// </summary>
    public sealed class NdiReceiverHealth : IHealthMonitor
    {
        public const string Id = "NDI";

        // Defense-in-depth cap so a malicious / oversized NDI broadcast name or exception
        // text cannot allocate megabytes of message string. Presenter is also expected
        // to sanitize at intake, but this guarantees the StatusPanel stays well-bounded
        // even if a future caller skips that path.
        public const int MaxMessageLength = 320;

        private readonly Dictionary<int, (HealthStatus Status, string Message)> _receivers = new();

        public string SystemId => Id;

        public HealthSnapshot CurrentSnapshot()
        {
            if (_receivers.Count == 0)
                return new HealthSnapshot(Id, HealthStatus.Unknown, "No receiver active");

            var status = HealthStatus.Healthy;
            string? message = null;
            foreach (var state in _receivers.Values)
                MergeState(state, ref status, ref message);

            return new HealthSnapshot(Id, status, message ?? $"Receivers: {_receivers.Count}");
        }

        public void ReportReceiverReady(int receiverId, string? sourceName)
        {
            _receivers[receiverId] = (
                HealthStatus.Healthy,
                ClampMessage(DescribeReadySource(SanitizeForMessage(sourceName))));
        }

        public void ReportSourceMissing(int receiverId, string sourceName)
        {
            _receivers[receiverId] = (
                HealthStatus.Degraded,
                ClampMessage($"Source disconnected: {SanitizeForMessage(sourceName)}"));
        }

        public void ReportReceiverUnavailable(int receiverId, string reason)
        {
            _receivers[receiverId] = (
                HealthStatus.Failed,
                ClampMessage(SanitizeForMessage(reason)));
        }

        public void ReportReceiverStopped(int receiverId)
        {
            _receivers.Remove(receiverId);
        }

        private static void MergeState(
            (HealthStatus Status, string Message) state,
            ref HealthStatus status,
            ref string? message)
        {
            if (state.Status > status)
            {
                status = state.Status;
                message = state.Message;
                return;
            }

            if (state.Status == status && message is null)
                message = state.Message;
        }

        private static string DescribeReadySource(string? sourceName)
        {
            return string.IsNullOrEmpty(sourceName)
                ? "Receiver ready"
                : $"Receiving: {sourceName}";
        }

        /// <summary>
        /// Strip control / DEL chars from untrusted text before embedding in a status message.
        /// Empty / null → "". No length cap here — <see cref="ClampMessage"/> handles that
        /// after format-string assembly.
        /// </summary>
        public static string SanitizeForMessage(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var src = text!;
            var needsCleanup = false;
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (c < 0x20 || c == 0x7F) { needsCleanup = true; break; }
            }
            if (!needsCleanup) return src;
            var sb = new System.Text.StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (c < 0x20 || c == 0x7F) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Clamp a fully-assembled status message to <see cref="MaxMessageLength"/>.
        /// </summary>
        public static string ClampMessage(string message)
        {
            return message.Length <= MaxMessageLength
                ? message
                : message.Substring(0, MaxMessageLength);
        }
    }
}
