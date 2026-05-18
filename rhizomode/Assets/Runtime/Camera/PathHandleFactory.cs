#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// Spline Knot に対応する PathControlPointVisual (球) を生成する pure C# factory。
    /// Miniature モード時は MiniaturePathMapper 経由で位置変換と書き戻し変換を仕込む。
    /// </summary>
    public sealed class PathHandleFactory
    {
        private readonly Material? _handleMaterial;

        public PathHandleFactory(Material? handleMaterial)
        {
            _handleMaterial = handleMaterial;
        }

        /// <summary>
        /// Spline Knot ごとに球ハンドルを生成する。
        /// </summary>
        /// <param name="container">対象 Spline。</param>
        /// <param name="mapper">Miniature モード時の座標変換 (null なら Direct モード = 恒等)。</param>
        /// <param name="handleRadius">球の半径 (m)。</param>
        /// <param name="parent">生成した球を parent 配下に SetParent する (worldPositionStays=true で world 位置維持)。
        /// MirrorHiddenScope を持つ管理 GameObject を渡すと layer 自動適用が効く。null なら world root。</param>
        public List<PathControlPointVisual> Create(
            SplineContainer container,
            MiniaturePathMapper? mapper,
            float handleRadius,
            Transform? parent = null)
        {
            var visuals = new List<PathControlPointVisual>();
            var spline = container.Spline;
            var xform = container.transform;
            bool isMini = mapper != null;
            string namePrefix = isMini ? "MiniPathKnot" : "PathKnot";

            for (int i = 0; i < spline.Count; i++)
            {
                var knot = spline[i];
                var realWorldPos = xform.TransformPoint((Vector3)knot.Position);
                var visualPos = isMini ? mapper!.ToMini(realWorldPos) : realWorldPos;

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"{namePrefix}_{i}";
                go.transform.position = visualPos;
                go.transform.localScale = Vector3.one * (handleRadius * 2f);
                if (parent != null) go.transform.SetParent(parent, worldPositionStays: true);

                if (_handleMaterial != null)
                {
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = _handleMaterial;
                }

                var visual = go.AddComponent<PathControlPointVisual>();
                if (isMini)
                {
                    var capturedMapper = mapper!;
                    visual.Initialize(i, mini => capturedMapper.ToReal(mini));
                }
                else
                {
                    visual.Initialize(i);
                }
                visuals.Add(visual);
            }

            return visuals;
        }
    }
}
