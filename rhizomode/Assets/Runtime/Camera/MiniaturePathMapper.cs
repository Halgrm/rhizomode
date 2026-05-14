#nullable enable

using UnityEngine;
using UnityEngine.Splines;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// 実空間の Spline Knot 群を anchor 周囲の固定サイズ立方に縮小マッピングする。
    /// 実空間 ↔ miniature 空間の双方向変換 (Matrix4x4) を保持する pure C# class。
    /// </summary>
    public sealed class MiniaturePathMapper
    {
        private const float MinValidExtent = 0.0001f;

        public Matrix4x4 RealToMini { get; }
        public Matrix4x4 MiniToReal { get; }

        /// <summary>
        /// Spline Knot 群の bbox を計算し、anchor 周囲 boxSize の立方に scale するマトリクスを構築する。
        /// </summary>
        /// <param name="container">対象 Spline。</param>
        /// <param name="anchor">miniature の中心となる world 位置。</param>
        /// <param name="boxSize">miniature 空間でのターゲット bbox 最大辺長 (m)。</param>
        /// <param name="includeOriginInBbox">実空間原点を bbox に含めるかどうか (座標参照表示に必要)。</param>
        /// <param name="minBboxExtent">bbox 最大辺長の下限 (Knot が 1 点しかない時の 0 除算回避)。</param>
        public MiniaturePathMapper(
            SplineContainer container,
            Vector3 anchor,
            float boxSize,
            bool includeOriginInBbox,
            float minBboxExtent)
        {
            // 0 / 負値が inspector 経由で渡る可能性に備えて defensive clamp
            // (divide-by-zero と inverse 計算崩壊回避)。
            boxSize = Mathf.Max(MinValidExtent, boxSize);
            minBboxExtent = Mathf.Max(MinValidExtent, minBboxExtent);

            var spline = container.Spline;
            var xform = container.transform;

            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;
            for (int i = 0; i < spline.Count; i++)
            {
                var w = xform.TransformPoint((Vector3)spline[i].Position);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
            if (includeOriginInBbox)
            {
                min = Vector3.Min(min, Vector3.zero);
                max = Vector3.Max(max, Vector3.zero);
            }

            var center = (min + max) * 0.5f;
            var size = max - min;
            var maxExtent = Mathf.Max(size.x, size.y, size.z, minBboxExtent);
            var scale = boxSize / maxExtent;

            // realToMini = T(anchor) * S(scale) * T(-center)
            var t1 = Matrix4x4.Translate(-center);
            var s = Matrix4x4.Scale(Vector3.one * scale);
            var t2 = Matrix4x4.Translate(anchor);
            RealToMini = t2 * s * t1;
            MiniToReal = RealToMini.inverse;
        }

        public Vector3 ToMini(Vector3 realWorld) => RealToMini.MultiplyPoint3x4(realWorld);
        public Vector3 ToReal(Vector3 miniWorld) => MiniToReal.MultiplyPoint3x4(miniWorld);
    }
}
