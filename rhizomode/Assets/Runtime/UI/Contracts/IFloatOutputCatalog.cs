#nullable enable

using System;
using System.Collections.Generic;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// グラフ内の全 Float 出力ポートを列挙し、購読を提供する catalog。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 9 Round E (E5): CameraManagerPanelController が GraphState を
    /// 直接掘らずに Float 出力一覧 + Observable を取得するための UI.Contracts 抽象。
    /// 実装は UI.GraphAdapter 層が <see cref="Graph.Model.GraphState"/> から構築する。
    /// </remarks>
    public interface IFloatOutputCatalog
    {
        /// <summary>現時点で利用可能な全 Float 出力ポート一覧。</summary>
        IReadOnlyList<FloatOutputRef> GetFloatOutputs();

        /// <summary>
        /// 指定 nodeId.portName の Float 出力を購読する。
        /// </summary>
        /// <param name="nodeId">node id</param>
        /// <param name="portName">port 名</param>
        /// <param name="callback">値変更時に呼ぶ delegate</param>
        /// <returns>購読解除用 IDisposable。該当ポートが見つからない / 型不一致なら null。</returns>
        IDisposable? Subscribe(string nodeId, string portName, Action<float> callback);
    }

    /// <summary>
    /// catalog 内の 1 つの Float 出力ポートを指す DTO。
    /// </summary>
    public readonly struct FloatOutputRef
    {
        public readonly string NodeId;
        public readonly string PortName;
        public readonly string DisplayName;

        public FloatOutputRef(string nodeId, string portName, string displayName)
        {
            NodeId = nodeId;
            PortName = portName;
            DisplayName = displayName;
        }
    }
}
