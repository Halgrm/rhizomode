#nullable enable

using NUnit.Framework;
using Rhizomode.Observability.Contracts;

namespace Rhizomode.UI.Tests
{
    public sealed class NdiReceiverHealthTests
    {
        [Test]
        public void SourceMissing_AfterReady_ReportsDegraded()
        {
            var health = new NdiReceiverHealth();
            health.ReportReceiverReady(1, "CAM-1");

            health.ReportSourceMissing(1, "CAM-1");

            var snapshot = health.CurrentSnapshot();
            Assert.AreEqual(NdiReceiverHealth.Id, snapshot.SystemId);
            Assert.AreEqual(HealthStatus.Degraded, snapshot.Status);
            StringAssert.Contains("CAM-1", snapshot.Message);
        }

        [Test]
        public void SourceAvailable_AfterMissing_ReportsHealthyAgain()
        {
            var health = new NdiReceiverHealth();
            health.ReportReceiverReady(1, "CAM-1");
            health.ReportSourceMissing(1, "CAM-1");

            health.ReportReceiverReady(1, "CAM-1");

            var snapshot = health.CurrentSnapshot();
            Assert.AreEqual(HealthStatus.Healthy, snapshot.Status);
            StringAssert.Contains("CAM-1", snapshot.Message);
        }

        [Test]
        public void ReportSourceMissing_StripsControlCharacters()
        {
            var health = new NdiReceiverHealth();
            // \0, \r, \n, \x1F, \x7F are all control / DEL — must be stripped before they
            // reach the status panel (otherwise a hostile NDI broadcast can corrupt the UI
            // or do log injection).
            health.ReportSourceMissing(1, "CAM\0\r\n\x1F\x7F-1");

            var snapshot = health.CurrentSnapshot();
            Assert.AreEqual(HealthStatus.Degraded, snapshot.Status);
            StringAssert.Contains("CAM-1", snapshot.Message);
            StringAssert.DoesNotContain("\0", snapshot.Message);
            StringAssert.DoesNotContain("\r", snapshot.Message);
            StringAssert.DoesNotContain("\n", snapshot.Message);
        }

        [Test]
        public void ReportSourceMissing_ClampsOverlongMessage()
        {
            var health = new NdiReceiverHealth();
            var bigName = new string('A', 4096);

            health.ReportSourceMissing(1, bigName);

            var snapshot = health.CurrentSnapshot();
            Assert.AreEqual(HealthStatus.Degraded, snapshot.Status);
            Assert.NotNull(snapshot.Message);
            Assert.LessOrEqual(
                snapshot.Message!.Length,
                NdiReceiverHealth.MaxMessageLength,
                "status message must not exceed the defensive length cap regardless of input size");
        }

        [Test]
        public void Sanitize_CleanInputReturnsSameReference()
        {
            var clean = "CAM-1 (PGM)";

            var result = NdiReceiverHealth.SanitizeForMessage(clean);

            Assert.AreSame(clean, result, "fast-path must not allocate for already-clean input");
        }

        [Test]
        public void Sanitize_NullReturnsEmpty()
        {
            Assert.AreEqual("", NdiReceiverHealth.SanitizeForMessage(null));
        }
    }
}
