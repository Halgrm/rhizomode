#nullable enable

using NUnit.Framework;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Core.Tests
{
    public class MathUtilsTests
    {
        [Test]
        public void RayToSegment_Perpendicular_ReturnsExactDistance()
        {
            // レイ: (0,0,0) → (0,0,1), 線分: (1,0,0)〜(1,0,2) → 距離1
            var dist = MathUtils.RayToSegmentDistance(
                Vector3.zero, Vector3.forward,
                new Vector3(1, 0, 0), new Vector3(1, 0, 2));
            Assert.AreEqual(1f, dist, 0.001f);
        }

        [Test]
        public void RayToSegment_Parallel_ReturnsMinDistance()
        {
            // 平行: レイ(0,0,0)→(1,0,0), 線分(0,1,0)〜(2,1,0) → 距離1
            var dist = MathUtils.RayToSegmentDistance(
                Vector3.zero, Vector3.right,
                new Vector3(0, 1, 0), new Vector3(2, 1, 0));
            Assert.AreEqual(1f, dist, 0.001f);
        }

        [Test]
        public void RayToSegment_Intersecting_ReturnsZero()
        {
            // 交差: レイ(0,0,0)→(1,0,0), 線分(-0.5,0,0)〜(0.5,0,0)
            var dist = MathUtils.RayToSegmentDistance(
                Vector3.zero, Vector3.right,
                new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0));
            Assert.AreEqual(0f, dist, 0.001f);
        }

        [Test]
        public void RayToSegment_DegenerateSegment_ReturnsPointDistance()
        {
            // 退化線分(点): レイ(0,0,0)→(0,0,1), 点(1,0,1) → 距離1
            var dist = MathUtils.RayToSegmentDistance(
                Vector3.zero, Vector3.forward,
                new Vector3(1, 0, 1), new Vector3(1, 0, 1));
            Assert.AreEqual(1f, dist, 0.001f);
        }

        [Test]
        public void RayToSegment_BehindRay_UsesRayOrigin()
        {
            // レイの後方: レイ(0,0,0)→(0,0,1), 線分(0,1,-2)〜(0,1,-1)
            // レイのtは0にクランプされるので、origin(0,0,0)からの距離
            var dist = MathUtils.RayToSegmentDistance(
                Vector3.zero, Vector3.forward,
                new Vector3(0, 1, -2), new Vector3(0, 1, -1));
            Assert.AreEqual(Mathf.Sqrt(1f + 1f), dist, 0.001f);
        }

        [Test]
        public void RayToSegment_SegmentEndpoint_ClosestToEndpoint()
        {
            // レイ(0,0,5)→(0,0,1)方向, 線分(0,0,0)〜(0,0,1) → 線分終端で距離≈0
            var dist = MathUtils.RayToSegmentDistance(
                new Vector3(0, 0, 5), Vector3.back,
                Vector3.zero, Vector3.forward);
            Assert.AreEqual(0f, dist, 0.01f);
        }
    }
}
