#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// UI 側がノードを live で表示・操作するためのハンドル。
    /// </summary>
    /// <remarks>
    /// <see cref="NodeViewModel"/> (record DTO) は snapshot 用、<c>INodeView</c> は live ハンドル用。
    /// UI.GraphAdapter が <c>Graph.Model.NodeBase</c> を wrap して提供する。
    ///
    /// Plan v5.3 Phase 9 Round E (E3+E4): UI.Presentation が <c>NodeBase</c> を直接参照しない
    /// ように、UI.Contracts レベルの抽象に切り替える。Position は live setter で transform 位置
    /// 変更を model に反映、IInline* は <c>AsXxx()</c> で取得 (実装側に keyword cast を集約)。
    /// </remarks>
    public interface INodeView
    {
        /// <summary>ノード ID (グラフ内一意)。</summary>
        string NodeId { get; }

        /// <summary>ノードの type 名 (例: "ConstFloat")。</summary>
        string TypeName { get; }

        /// <summary>ワールド座標。setter で model 側にも書き戻す。</summary>
        Vector3 Position { get; set; }

        /// <summary>入力ポート一覧 (描画順)。</summary>
        IReadOnlyList<PortViewModel> InputPorts { get; }

        /// <summary>出力ポート一覧 (描画順)。</summary>
        IReadOnlyList<PortViewModel> OutputPorts { get; }

        /// <summary>IInlineSlider 実装なら取得、未実装なら null。</summary>
        IInlineSlider? AsSlider();

        /// <summary>IInlineButton 実装なら取得、未実装なら null。</summary>
        IInlineButton? AsButton();

        /// <summary>IInlineMonitor 実装なら取得、未実装なら null。</summary>
        IInlineMonitor? AsMonitor();

        /// <summary>IInlineWaveform 実装なら取得、未実装なら null。</summary>
        IInlineWaveform? AsWaveform();

        /// <summary>IInlineSpectrum 実装なら取得、未実装なら null。</summary>
        IInlineSpectrum? AsSpectrum();

        /// <summary>IInlineColorPicker 実装なら取得、未実装なら null。</summary>
        IInlineColorPicker? AsColorPicker();

        /// <summary>INdiReceiverNode 実装なら取得、未実装なら null。</summary>
        INdiReceiverNode? AsNdiReceiver();

        /// <summary>INdiViewWindowState 実装なら取得、未実装なら null (window transform side-channel)。</summary>
        INdiViewWindowState? AsNdiViewWindowState();
    }
}
