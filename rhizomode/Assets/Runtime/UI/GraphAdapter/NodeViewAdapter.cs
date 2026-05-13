#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Rhizomode.Graph.Model;
using Rhizomode.SharedKernel;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="NodeBase"/> を <see cref="INodeView"/> (UI.Contracts) に wrap する live adapter。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 9 Round E (E3+E4): UI.Presentation が NodeBase を直接参照しない
    /// 構造にするための adapter。port 一覧は constructor で 1 回スナップして以後 immutable
    /// (ノードの port 構成は spawn 後変化しない前提)。
    /// </remarks>
    public sealed class NodeViewAdapter : INodeView
    {
        private readonly NodeBase _node;
        private readonly ReadOnlyCollection<PortViewModel> _inputs;
        private readonly ReadOnlyCollection<PortViewModel> _outputs;

        public NodeViewAdapter(NodeBase node)
        {
            _node = node;
            var inputs = new List<PortViewModel>();
            var outputs = new List<PortViewModel>();
            foreach (var p in node.GetPortDefinitions())
            {
                var vm = new PortViewModel(p.name, p.type, IsConnected: false);
                if (p.direction == PortDirection.Input) inputs.Add(vm);
                else outputs.Add(vm);
            }
            // Codex E review (Criterion 3): backing List ではなく ReadOnlyCollection を保持し、
            // caller が cast で書き込み可能な List に到達できないようにする。
            _inputs = new ReadOnlyCollection<PortViewModel>(inputs);
            _outputs = new ReadOnlyCollection<PortViewModel>(outputs);
        }

        public string NodeId => _node.Id;
        public string TypeName => _node.NodeType;

        public Vector3 Position
        {
            get => _node.Position;
            set => _node.Position = value;
        }

        public IReadOnlyList<PortViewModel> InputPorts => _inputs;
        public IReadOnlyList<PortViewModel> OutputPorts => _outputs;

        public IInlineSlider? AsSlider() => _node as IInlineSlider;
        public IInlineButton? AsButton() => _node as IInlineButton;
        public IInlineMonitor? AsMonitor() => _node as IInlineMonitor;
        public IInlineWaveform? AsWaveform() => _node as IInlineWaveform;
        public IInlineSpectrum? AsSpectrum() => _node as IInlineSpectrum;
        public IInlineColorPicker? AsColorPicker() => _node as IInlineColorPicker;
    }
}
