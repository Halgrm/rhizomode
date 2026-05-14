#nullable enable

namespace Rhizomode.Audio.Contracts
{
    /// <summary>
    /// audio frame を毎 tick 供給する source。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10: <see cref="AudioFrame"/> は ref struct のため field に保持できず、
    /// 取得した frame は同一 stack scope 内で消費する必要がある。Bootstrap の
    /// AudioDriverHostTickAdapter が tick ごとに source から frame を取得し、登録済の
    /// <see cref="IAudioDrivenNode"/> に dispatch する pattern を想定。
    /// </remarks>
    public interface IAudioFrameSource
    {
        /// <summary>現在の audio frame を取得する。device 未初期化時は <see cref="AudioFrame.Empty"/>。</summary>
        AudioFrame CurrentFrame { get; }

        /// <summary>capture が active か (device 初期化済 + stream 正常)。</summary>
        bool IsActive { get; }

        /// <summary>現在の device 名 (未初期化なら null)。</summary>
        string? CurrentDevice { get; }
    }
}
