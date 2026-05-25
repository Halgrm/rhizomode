#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.Nodes.Math
{
    /// <summary>
    /// MIDI ノートナンバー (0-127) を周波数 (Hz) に変換する。note 69 = A4 = 440Hz。
    /// </summary>
    /// <remarks>
    /// 公式: hz = 440 * 2^((note - 69) / 12)
    /// NaN/Inf 入力は 0 にフォールバック。
    /// </remarks>
    [NodeType("NoteToHz", "Note → Hz", NodeCategory.Math)]
    public class NoteToHzNode : NodeBase
    {
        private const float A4Hz = 440f;
        private const float A4Note = 69f;

        private readonly OutputPort<float> _hzOut;

        public NoteToHzNode(string id) : base(id, "NoteToHz")
        {
            RegisterInput<float>("Note", ParamType.Float, PortUnit.Note);
            _hzOut = RegisterOutput<float>("Hz", ParamType.Float, PortUnit.Hz);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "Note")
                    .Subscribe(note => _hzOut.Emit(Compute(note))));
        }

        private static float Compute(float note)
        {
            if (!float.IsFinite(note)) return 0f;
            return A4Hz * Mathf.Pow(2f, (note - A4Note) / 12f);
        }
    }
}
