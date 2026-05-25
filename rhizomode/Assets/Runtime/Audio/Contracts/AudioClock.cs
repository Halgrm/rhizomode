#nullable enable

using System;
using UnityEngine;

namespace Rhizomode.Audio.Contracts
{
    /// <summary>
    /// Audio 駆動 node (BeatDetector 等) が「ビートが鳴った瞬間」を求めるための時刻ソース。
    /// </summary>
    /// <remarks>
    /// LASP / オーディオ I/F の buffer / SteamVR + Quest Link の経路には数十 ms の遅延がある。
    /// VR ライブで映像が音から遅れて見える原因の一つ。<see cref="LatencyOffsetSeconds"/> を実機で
    /// 実測キャリブして引くことで補正する (AudioAnalyzer の Inspector で公開、PlayerPrefs で永続化)。
    ///
    /// TapTempoNode のような controller-driven node は本 clock を使わず
    /// <see cref="UnityEngine.Time.unscaledTime"/> を直接読む (タップ遅延は audio I/F と独立)。
    ///
    /// 単体テスト用: <see cref="NowProvider"/> を差し替えれば <see cref="UnityEngine.Time"/> を
    /// 経由せず deterministic な「今」を返せる。
    /// </remarks>
    public static class AudioClock
    {
        private static float _latencyOffsetSeconds;

        /// <summary>
        /// Audio I/F 経由イベントから差し引く遅延 (秒)。
        /// 既定 0、AudioAnalyzer / PlayerPrefs から実機キャリブ値が注入される想定。
        /// </summary>
        public static float LatencyOffsetSeconds
        {
            get => _latencyOffsetSeconds;
            set => _latencyOffsetSeconds = float.IsFinite(value) ? value : 0f;
        }

        /// <summary>
        /// 現在時刻を返す関数 (差し替え可能、test 用)。既定は <see cref="UnityEngine.Time.unscaledTime"/>。
        /// </summary>
        public static Func<float> NowProvider { get; set; } = () => Time.unscaledTime;

        /// <summary>
        /// オーディオ補正後の「今」(秒)。BeatDetector 等の audio-driven node が使う。
        /// </summary>
        public static float Now => NowProvider() - _latencyOffsetSeconds;

        /// <summary>テスト用: state を初期値に戻す (テスト間 isolation 用)。</summary>
        public static void ResetForTest()
        {
            _latencyOffsetSeconds = 0f;
            NowProvider = () => Time.unscaledTime;
        }
    }
}
