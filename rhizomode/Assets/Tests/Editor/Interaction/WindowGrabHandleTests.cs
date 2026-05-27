#nullable enable

using NUnit.Framework;
using Rhizomode.Interaction;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Interaction.Tests
{
    /// <summary>
    /// <see cref="WindowGrabHandle"/> の pure-math 部 (clamp / scale formula) を runtime
    /// 抜きに検証する EditMode test。XR plumbing 部は manual PlayMode canary で確認。
    /// </summary>
    public sealed class WindowGrabHandleTests
    {
        // --- LockRollClampPitch ---

        [Test]
        public void LockRoll_ZeroesRollAxis()
        {
            var clamped = WindowGrabHandle.LockRollClampPitch(new Vector3(0f, 0f, 45f));
            Assert.AreEqual(0f, clamped.z, "roll は常に 0 lock");
        }

        [Test]
        public void ClampPitch_WithinRange_LeavesUntouched()
        {
            var clamped = WindowGrabHandle.LockRollClampPitch(new Vector3(30f, 90f, 0f));
            Assert.AreEqual(30f, clamped.x, 1e-3f);
            Assert.AreEqual(90f, clamped.y, "yaw は自由 (clamp 無し)");
        }

        [Test]
        public void ClampPitch_AbovePositiveLimit_ClampsToMax()
        {
            // 75° → MaxPitchDeg (60°) で頭打ち
            var clamped = WindowGrabHandle.LockRollClampPitch(new Vector3(75f, 0f, 0f));
            Assert.AreEqual(WindowGrabHandle.MaxPitchDeg, clamped.x, 1e-3f);
        }

        [Test]
        public void ClampPitch_NegativeAsEulerWrap_ClampsToMin()
        {
            // Unity の eulerAngles は常に [0, 360) で返るので、-75° は 285° として入ってくる。
            // 内部で signed 化して -60° に clamp されること。
            var clamped = WindowGrabHandle.LockRollClampPitch(new Vector3(285f, 0f, 0f));
            Assert.AreEqual(-WindowGrabHandle.MaxPitchDeg, clamped.x, 1e-3f);
        }

        // --- ComputeTwoHandScale ---

        [Test]
        public void TwoHandScale_DoubleDistance_DoublesScale()
        {
            // baseline 1.0m, baseline scale 1.0 → 距離倍で scale 2.0 (MaxScale 4.0 以内)
            var s = WindowGrabHandle.ComputeTwoHandScale(1.0f, 2.0f, 1.0f);
            Assert.AreEqual(2.0f, s, 1e-3f);
        }

        [Test]
        public void TwoHandScale_HalfDistance_HalvesScale()
        {
            var s = WindowGrabHandle.ComputeTwoHandScale(1.0f, 0.5f, 1.0f);
            Assert.AreEqual(0.5f, s, 1e-3f);
        }

        [Test]
        public void TwoHandScale_ClampsToMaxScale()
        {
            // 10 倍にしようとしても MaxScale (4.0) で頭打ち
            var s = WindowGrabHandle.ComputeTwoHandScale(1.0f, 10.0f, 1.0f);
            Assert.AreEqual(NdiViewWindow.MaxScale, s, 1e-3f);
        }

        [Test]
        public void TwoHandScale_ClampsToMinScale()
        {
            // 1/100 にしようとしても MinScale (0.1) で下限
            var s = WindowGrabHandle.ComputeTwoHandScale(1.0f, 0.01f, 1.0f);
            Assert.AreEqual(NdiViewWindow.MinScale, s, 1e-3f);
        }

        [Test]
        public void TwoHandScale_ZeroBaseline_DoesNotExplode()
        {
            // baseline=0 (両手が重なってる起動) でも NaN/Inf にならないこと
            var s = WindowGrabHandle.ComputeTwoHandScale(0f, 1.0f, 1.0f);
            Assert.IsTrue(float.IsFinite(s), "0 baseline は epsilon 補正で finite を保つ");
            Assert.LessOrEqual(s, NdiViewWindow.MaxScale);
            Assert.GreaterOrEqual(s, NdiViewWindow.MinScale);
        }
    }
}
