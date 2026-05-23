#nullable enable

using NUnit.Framework;
using Rhizomode.UI;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// <see cref="PendingCueLoad"/> static slot の Schedule / Consume invariants。
    /// シーン切替を跨いだ cue 復元の核となる process-static state なので contract を CI で守る。
    /// </summary>
    public class PendingCueLoadTests
    {
        [SetUp]
        public void SetUp()
        {
            // 他テストが残した state を捨てる (process-static のため)
            PendingCueLoad.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PendingCueLoad.Clear();
        }

        [Test]
        public void HasPending_DefaultsToFalse()
        {
            Assert.IsFalse(PendingCueLoad.HasPending, "Clear 直後は pending 無し");
        }

        [Test]
        public void Schedule_SetsPending()
        {
            PendingCueLoad.Schedule("cue_01.json");
            Assert.IsTrue(PendingCueLoad.HasPending);
        }

        [Test]
        public void TryConsume_EmptySlot_ReturnsFalse()
        {
            var ok = PendingCueLoad.TryConsume(out var name);
            Assert.IsFalse(ok);
            Assert.AreEqual("", name, "未予約時は out param は空文字");
        }

        [Test]
        public void TryConsume_ReturnsScheduledNameAndClears()
        {
            PendingCueLoad.Schedule("cue_42.json");
            var ok = PendingCueLoad.TryConsume(out var name);

            Assert.IsTrue(ok);
            Assert.AreEqual("cue_42.json", name);
            Assert.IsFalse(PendingCueLoad.HasPending, "Consume 後は slot がクリアされる");
        }

        [Test]
        public void Schedule_OverwritesPrevious()
        {
            // 単一スロット (キューイングしない) 契約: 後勝ち
            PendingCueLoad.Schedule("first.json");
            PendingCueLoad.Schedule("second.json");

            PendingCueLoad.TryConsume(out var name);
            Assert.AreEqual("second.json", name, "Schedule は後勝ち (キューイングしない)");
        }

        [Test]
        public void TryConsume_Twice_SecondReturnsFalse()
        {
            PendingCueLoad.Schedule("once.json");
            PendingCueLoad.TryConsume(out _);
            var ok = PendingCueLoad.TryConsume(out var name);

            Assert.IsFalse(ok);
            Assert.AreEqual("", name);
        }
    }
}
