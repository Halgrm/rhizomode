#nullable enable

using UnityEngine;

using Rhizomode.SharedKernel;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// ノードパネル内にHSVカラーピッカーUIを表示するためのインターフェース。
    /// ConstColorなど、ユーザーが色を直接操作するノードが実装する。
    /// </summary>
    public interface IInlineColorPicker
    {
        /// <summary>現在の選択色。</summary>
        Color PickerColor { get; set; }
    }
}
