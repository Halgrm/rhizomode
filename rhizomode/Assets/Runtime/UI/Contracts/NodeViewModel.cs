#nullable enable

using System;
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
    ///
    /// Codex review fix: <see cref="IReadOnlyList{T}"/> は record class の auto-generated
    /// equality で参照比較になるため、port collection の構造比較を明示実装する。
    /// </remarks>
    public sealed record NodeViewModel(
        string NodeId,
        string TypeName,
        string Label,
        RzVector3 Position,
        IReadOnlyList<PortViewModel> InputPorts,
        IReadOnlyList<PortViewModel> OutputPorts)
    {
        public bool Equals(NodeViewModel? other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null) return false;
            return NodeId == other.NodeId
                && TypeName == other.TypeName
                && Label == other.Label
                && Position.Equals(other.Position)
                && SequenceEqual(InputPorts, other.InputPorts)
                && SequenceEqual(OutputPorts, other.OutputPorts);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(NodeId);
            hash.Add(TypeName);
            hash.Add(Label);
            hash.Add(Position);
            foreach (var p in InputPorts) hash.Add(p);
            foreach (var p in OutputPorts) hash.Add(p);
            return hash.ToHashCode();
        }

        private static bool SequenceEqual<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) return false;
            }
            return true;
        }
    }
}
