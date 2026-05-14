#nullable enable

using UnityEngine;

namespace Rhizomode.Audio.Analysis.Infrastructure
{
    /// <summary>
    /// FFT サイズの妥当性検証ヘルパー。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10C: AudioAnalyzer.ValidateFftSize の logic を pure static helper に
    /// 抽出。FFT は 2 の累乗のみ受け付け、不正値は最近傍に補正する。
    /// </remarks>
    public static class FftSizeValidator
    {
        public static readonly int[] ValidSizes = { 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

        /// <summary>
        /// 入力 size を最近傍の有効な FFT サイズに丸める。
        /// </summary>
        public static int Snap(int fftSize)
        {
            var closest = ValidSizes[0];
            var minDiff = int.MaxValue;
            foreach (var valid in ValidSizes)
            {
                var diff = Mathf.Abs(fftSize - valid);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = valid;
                }
            }
            return closest;
        }
    }
}
