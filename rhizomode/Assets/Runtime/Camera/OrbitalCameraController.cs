#nullable enable

using Unity.Cinemachine;
using UnityEngine;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// <see cref="CinemachineOrbitalFollow"/> の水平周回角を Drive(0..1) で駆動する周回カメラ。
    /// 0..1 を HorizontalAxis.Range に線形写像する。LFO ノードを Source にすれば連続周回になる。
    /// </summary>
    /// <remarks>
    /// 入力デバイス駆動 (CinemachineInputAxisController) は付けない。軸は本コントローラの
    /// <see cref="SetDrive"/> だけが動かす — PathCameraController の Progress と同じ設計。
    /// </remarks>
    [RequireComponent(typeof(CinemachineCamera))]
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class OrbitalCameraController : MonoBehaviour, ICameraMotion
    {
        [SerializeField] private CinemachineOrbitalFollow? orbitalFollow;
        [SerializeField] private string displayName = "Orbital Camera";

        private float _drive;

        public string DisplayName => displayName;
        public string MotionLabel => "Orbit";
        public float Drive => _drive;

        private void Awake()
        {
            if (orbitalFollow == null)
                orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
        }

        /// <summary>周回角を 0..1 でセットする。HorizontalAxis.Range の両端へ線形写像する。</summary>
        public void SetDrive(float value)
        {
            if (orbitalFollow == null) return;
            _drive = Mathf.Clamp01(value);
            var range = orbitalFollow.HorizontalAxis.Range;
            orbitalFollow.HorizontalAxis.Value = Mathf.Lerp(range.x, range.y, _drive);
        }
    }
}
