#nullable enable

using UnityEngine;

using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Model
{
    /// <summary>
    /// ノードパネル内に値モニターを表示するためのインターフェース。
    /// 入力値をリアルタイム表示するモニターノードが実装する。
    /// </summary>
    public interface IInlineMonitor
    {
        /// <summary>モニター対象のパラメータ型。</summary>
        ParamType MonitorType { get; }

        /// <summary>現在の表示テキスト。毎フレーム参照される。</summary>
        string MonitorDisplayValue { get; }

        /// <summary>Color型モニター用。MonitorTypeがColorの場合のみ使用。</summary>
        Color MonitorColor { get; }
    }
}
