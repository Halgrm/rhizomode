#nullable enable

using System;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// NDI 受信ノードの marker + setter contract。
    /// </summary>
    /// <remarks>
    /// UI.Presentation の <c>NdiReceiverPresenter</c> が <c>INodeView.AsNdiReceiver()</c> 経由で
    /// node を識別し、sourceName を観測する。実装は <c>Rhizomode.Nodes.Video.NdiReceiverNode</c>。
    /// Klak.NDI 依存は presenter 側に閉じ、本 interface は文字列のみで純粋。
    /// </remarks>
    public interface INdiReceiverNode
    {
        /// <summary>受信対象 NDI ソース名 (空文字なら presenter が auto-pick)。</summary>
        string SourceName { get; }

        /// <summary>SourceName が presenter / paramsJson 経由で変更されたときに発火する。</summary>
        event Action<string>? OnSourceNameChanged;

        /// <summary>presenter が auto-pick / ユーザー操作で source を変更する経路。</summary>
        void SetSourceName(string sourceName);

        /// <summary>
        /// ユーザーがノード UI で「次のソースに切り替えて」を要求したとき発火する。
        /// presenter が Klak.NDI 側で enumerate → 次の未使用 source を選び <see cref="SetSourceName"/> を呼ぶ。
        /// </summary>
        /// <remarks>
        /// Nodes 層は Klak.NDI を参照しないため、enumerate ロジックは presenter に閉じ込め、
        /// node からは「次が欲しい」の信号のみ event で流す non-circular 設計。
        /// </remarks>
        event Action? OnNextSourceRequested;

        /// <summary>UI ボタン経由で「次のソース」を要求する (event を発火する thin trampoline)。</summary>
        void RequestNextSource();
    }
}
