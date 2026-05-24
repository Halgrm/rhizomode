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
    }
}
