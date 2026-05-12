#nullable enable

using System;
using UnityEngine;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// パス制御点 (Spline Knot) の VR 内ハンドル。球メッシュ + SphereCollider を持ち、
    /// 右グリップで掴めるレイキャストターゲットになる。
    /// Miniature 編集モードでは toRealMapper を介して miniature ワールド座標から
    /// 実 Spline のワールド座標へ変換した上で OnPositionChanged を発火する。
    /// </summary>
    public class PathControlPointVisual : MonoBehaviour
    {
        /// <summary>Spline 上のノット番号 (0..N-1)。</summary>
        public int KnotIndex { get; private set; }

        /// <summary>
        /// 位置が変わった時に呼ばれる。Miniature モードでは「実空間に換算した」world position が渡る。
        /// </summary>
        public event Action<int, Vector3>? OnPositionChanged;

        private Func<Vector3, Vector3>? _toRealMapper;

        /// <summary>
        /// 初期化。Manager から呼び出される。
        /// </summary>
        /// <param name="knotIndex">対応する Spline Knot 番号。</param>
        /// <param name="toRealMapper">
        /// グラブ位置 (Visual の world pos) を「実 Spline のあるべき world pos」に変換する関数。
        /// Miniature モード時のみ非 null。Direct モードでは null (恒等変換)。
        /// </param>
        public void Initialize(int knotIndex, Func<Vector3, Vector3>? toRealMapper = null)
        {
            KnotIndex = knotIndex;
            _toRealMapper = toRealMapper;
        }

        /// <summary>
        /// グラブ中に位置を更新する。OnPositionChanged を発火し Spline に反映させる。
        /// </summary>
        public void UpdateWorldPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            var realPos = _toRealMapper != null
                ? _toRealMapper(worldPosition)
                : worldPosition;
            OnPositionChanged?.Invoke(KnotIndex, realPos);
        }
    }
}
