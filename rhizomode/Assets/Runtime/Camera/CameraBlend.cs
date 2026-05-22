#nullable enable

namespace Rhizomode.Cameras
{
    /// <summary>
    /// CinemachineBrain のカメラ切替ブレンド形状。
    /// Rector の同名 enum を移植。<see cref="Unity.Cinemachine.CinemachineBlendDefinition.Styles"/>
    /// のうち、固有のカーブ指定が必要な <c>Custom</c> を除いた 7 種をパネルで選択可能にする。
    /// </summary>
    public enum CameraBlend
    {
        /// <summary>瞬間切替 (ブレンド時間 0)。</summary>
        Cut = 0,

        /// <summary>S 字カーブ。出入りとも滑らか。</summary>
        EaseInOut = 1,

        /// <summary>出はリニア、入りはイーズ。</summary>
        EaseIn = 2,

        /// <summary>出はイーズ、入りはリニア。</summary>
        EaseOut = 3,

        /// <summary>出はイーズ、入りはハード。</summary>
        HardIn = 4,

        /// <summary>出はハード、入りはイーズ。</summary>
        HardOut = 5,

        /// <summary>リニア。機械的な等速ブレンド。</summary>
        Linear = 6,
    }
}
