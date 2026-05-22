#nullable enable

using Rhizomode.Graph.Serialization;

namespace Rhizomode.UI
{
    /// <summary>
    /// カメラ状態 (レンズ / ターゲット / パス / Motion ソース / ライブカメラ) を
    /// <see cref="CameraStateData"/> へ捕捉し、ロード時にシーンへ復元するサービス。
    /// グラフのセーブ/ロード経路 (<see cref="GraphSaveLoadManager"/>) から呼ばれる。
    /// </summary>
    public interface ICameraStatePersistence
    {
        /// <summary>現在のシーンの全 CinemachineCamera の状態を捕捉する。</summary>
        CameraStateData Capture();

        /// <summary>
        /// <see cref="CameraStateData"/> をシーンのカメラへ復元する。
        /// null / 空は no-op。個々のカメラ/パスの失敗はログのみで握りつぶす (fail-open)。
        /// </summary>
        void Restore(CameraStateData? state);
    }
}
