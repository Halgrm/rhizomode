#nullable enable

namespace Rhizomode.Cameras
{
    /// <summary>
    /// グラフの Float 出力 (0..1) で 1 軸の動きを駆動できるカメラの抽象。
    /// CameraManagerPanel はこれを実装するカメラに「Motion 駆動」UI を出し、
    /// PathCameraController なら spline 位置、OrbitalCameraController なら周回角へ写像する。
    /// </summary>
    public interface ICameraMotion
    {
        /// <summary>パネルに表示する駆動軸の名前 (例: "Progress", "Orbit")。</summary>
        string MotionLabel { get; }

        /// <summary>現在の駆動値 (0..1)。</summary>
        float Drive { get; }

        /// <summary>駆動値 (0..1) をセットする。実装側で各自の軸へ写像する。</summary>
        void SetDrive(float value);
    }
}
