#nullable enable

using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Model
{
    /// <summary>
    /// ノードパネル内にスライダーUIを表示するためのインターフェース。
    /// ConstFloatなど、ユーザーが直接値を操作するノードが実装する。
    /// </summary>
    public interface IInlineSlider
    {
        /// <summary>現在のスライダー値。</summary>
        float SliderValue { get; set; }

        /// <summary>スライダーの最小値。</summary>
        float SliderMin { get; }

        /// <summary>スライダーの最大値。</summary>
        float SliderMax { get; }

        /// <summary>スライダーのラベル表示名。</summary>
        string SliderLabel { get; }
    }
}
