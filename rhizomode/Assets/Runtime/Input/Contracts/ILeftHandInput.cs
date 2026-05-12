#nullable enable

using R3;

namespace Rhizomode.Input.Contracts
{
    /// <summary>
    /// 左手コントローラーのトリガー・グリップ入力。
    /// </summary>
    public interface ILeftHandInput
    {
        /// <summary>左トリガー（press/release）。</summary>
        Observable<bool> OnLeftSelect { get; }

        /// <summary>左グリップ（press/release）。</summary>
        Observable<bool> OnLeftGrab { get; }
    }
}
