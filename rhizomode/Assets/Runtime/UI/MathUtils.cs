#nullable enable

using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// ジオメトリ計算ユーティリティ。エッジ切断判定等に使用。
    /// </summary>
    public static class MathUtils
    {
        /// <summary>
        /// レイと線分の最短距離を求める。
        /// </summary>
        /// <param name="rayOrigin">レイの始点。</param>
        /// <param name="rayDir">レイの方向（正規化推奨）。</param>
        /// <param name="segA">線分の始点。</param>
        /// <param name="segB">線分の終点。</param>
        /// <returns>レイと線分の最短距離。</returns>
        public static float RayToSegmentDistance(Vector3 rayOrigin, Vector3 rayDir, Vector3 segA, Vector3 segB)
        {
            var segDir = segB - segA;
            var segLenSq = segDir.sqrMagnitude;

            // 退化ケース: 線分が点の場合
            if (segLenSq < 1e-8f)
            {
                return RayToPointDistance(rayOrigin, rayDir, segA);
            }

            // レイとセグメントの最近接点を求める
            var w = rayOrigin - segA;
            var a = Vector3.Dot(rayDir, rayDir);
            var b = Vector3.Dot(rayDir, segDir);
            var c = Vector3.Dot(segDir, segDir);
            var d = Vector3.Dot(rayDir, w);
            var e = Vector3.Dot(segDir, w);

            var denom = a * c - b * b;

            float rayT, segT;

            if (denom < 1e-8f)
            {
                // 平行ケース: レイ上の最近接点を線分の各端点から探す
                rayT = 0f;
                segT = e / c;
            }
            else
            {
                rayT = (b * e - c * d) / denom;
                segT = (a * e - b * d) / denom;
            }

            // クランプ後に相手パラメータを再射影
            if (rayT < 0f)
            {
                rayT = 0f;
                segT = Vector3.Dot(segA - rayOrigin, segDir) / c;
            }

            segT = Mathf.Clamp01(segT);

            // セグメント端点からレイへ再射影
            var closestOnSeg = segA + segDir * segT;
            rayT = Mathf.Max(0f, Vector3.Dot(closestOnSeg - rayOrigin, rayDir) / a);

            var closestOnRay = rayOrigin + rayDir * rayT;

            return Vector3.Distance(closestOnRay, closestOnSeg);
        }

        private static float RayToPointDistance(Vector3 rayOrigin, Vector3 rayDir, Vector3 point)
        {
            var w = point - rayOrigin;
            var t = Mathf.Max(0f, Vector3.Dot(w, rayDir) / Vector3.Dot(rayDir, rayDir));
            var closestOnRay = rayOrigin + rayDir * t;
            return Vector3.Distance(closestOnRay, point);
        }
    }
}
