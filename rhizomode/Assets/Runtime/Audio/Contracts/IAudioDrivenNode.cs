#nullable enable

namespace Rhizomode.Audio.Contracts
{
    /// <summary>
    /// audio frame を毎 tick 受け取って処理するノード contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10: AudioBandNode / AudioTriggerNode / BeatDetectorNode 等の audio
    /// 駆動 node が実装する。<see cref="AudioFrame"/> は `in` で渡し、ref struct のまま
    /// stack 上で消費する (heap allocation なし)。
    /// </remarks>
    public interface IAudioDrivenNode
    {
        /// <summary>
        /// 毎 tick 呼ばれる。frame は ref struct なので保持禁止。
        /// </summary>
        /// <param name="frame">現在の audio frame (ReadOnlySpan を含む zero-copy view)</param>
        void OnAudioFrame(in AudioFrame frame);
    }
}
