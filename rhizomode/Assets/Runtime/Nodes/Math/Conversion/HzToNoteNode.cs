#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.Nodes.Math
{
    /// <summary>
    /// 周波数 (Hz) を MIDI ノートナンバー (0-127) に変換する。A4=440Hz=note 69。
    /// </summary>
    /// <remarks>
    /// 公式: note = 12 * log2(hz / 440) + 69
    /// hz &lt;= 0 の場合は 0 を返す (log の domain error 防止)。
    /// </remarks>
    [NodeType("HzToNote", "Hz → Note", NodeCategory.Math)]
    public class HzToNoteNode : NodeBase
    {
        private const float A4Hz = 440f;
        private const float A4Note = 69f;

        private readonly OutputPort<float> _noteOut;

        public HzToNoteNode(string id) : base(id, "HzToNote")
        {
            RegisterInput<float>("Hz", ParamType.Float, PortUnit.Hz);
            _noteOut = RegisterOutput<float>("Note", ParamType.Float, PortUnit.Note);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "Hz")
                    .Subscribe(hz => _noteOut.Emit(Compute(hz))));
        }

        private static float Compute(float hz)
        {
            if (!float.IsFinite(hz) || hz <= 0f) return 0f;
            return 12f * Mathf.Log(hz / A4Hz, 2f) + A4Note;
        }
    }
}
