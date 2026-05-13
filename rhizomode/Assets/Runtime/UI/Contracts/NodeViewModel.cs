#nullable enable

using System.Collections.Generic;
using Rhizomode.SharedKernel;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// UI 側がノードを描画するための ViewModel (DTO)。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 5: <c>UI.GraphAdapter.GraphStateToViewModelProjector</c> が
    /// <c>Graph.Query.GraphReadModel</c> + <c>Graph.Events.GraphEventBus</c> を購読して構築する。
    ///
    /// UI.Presentation の <c>NodeVisualController</c> はこの DTO のみを受け取り、
    /// <c>Graph.Model.NodeBase</c> や <c>GraphState</c> を一切知らない。
    /// 旧コードは GraphState 直接参照していたが、Phase 5 で本 ViewModel 経由に migrate する。
    /// </remarks>
    public sealed record NodeViewModel(
        string NodeId,
        string TypeName,
        string Label,
        RzVector3 Position,
        IReadOnlyList<PortViewModel> InputPorts,
        IReadOnlyList<PortViewModel> OutputPorts);
}
