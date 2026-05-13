#nullable enable

using Rhizomode.SharedKernel;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// ノードパネル内にタップボタンを表示するためのインターフェース。
    /// TapTempoなど、ユーザーが直接トリガーするノードが実装する。
    /// </summary>
    public interface IInlineButton
    {
        /// <summary>ボタンのラベル。</summary>
        string ButtonLabel { get; }

        /// <summary>ボタンが押された時に呼ばれる。</summary>
        void OnButtonPressed();
    }
}
