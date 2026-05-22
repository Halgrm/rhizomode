#nullable enable

using NUnit.Framework;
using Unity.Cinemachine;

namespace Rhizomode.Cameras.Tests
{
    /// <summary>
    /// <see cref="CameraBlendController.ToBlendDefinition"/> の
    /// <see cref="CameraBlend"/> → <see cref="CinemachineBlendDefinition"/> 写像テスト。
    /// </summary>
    public class CameraBlendControllerTests
    {
        [Test]
        public void ToBlendDefinition_Cut_IgnoresTimeAndForcesZero()
        {
            var def = CameraBlendController.ToBlendDefinition(CameraBlend.Cut, 5f);
            Assert.AreEqual(CinemachineBlendDefinition.Styles.Cut, def.Style);
            Assert.AreEqual(0f, def.Time);
        }

        [TestCase(CameraBlend.EaseInOut, CinemachineBlendDefinition.Styles.EaseInOut)]
        [TestCase(CameraBlend.EaseIn, CinemachineBlendDefinition.Styles.EaseIn)]
        [TestCase(CameraBlend.EaseOut, CinemachineBlendDefinition.Styles.EaseOut)]
        [TestCase(CameraBlend.HardIn, CinemachineBlendDefinition.Styles.HardIn)]
        [TestCase(CameraBlend.HardOut, CinemachineBlendDefinition.Styles.HardOut)]
        [TestCase(CameraBlend.Linear, CinemachineBlendDefinition.Styles.Linear)]
        public void ToBlendDefinition_NonCut_MapsStyleAndKeepsTime(
            CameraBlend blend, CinemachineBlendDefinition.Styles expectedStyle)
        {
            var def = CameraBlendController.ToBlendDefinition(blend, 2.5f);
            Assert.AreEqual(expectedStyle, def.Style);
            Assert.AreEqual(2.5f, def.Time);
        }
    }
}
