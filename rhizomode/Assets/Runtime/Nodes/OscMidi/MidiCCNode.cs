#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using Rhizomode.OscMidi.Contracts;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.OscMidi
{
    /// <summary>
    /// 指定MIDI CC番号の値を受信してfloat出力するノード（0-1正規化済み）。
    /// <see cref="IMidiSource"/> の Observable を購読する。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>MidiServer.Instance</c> singleton 直参照を解消。
    /// <see cref="IMidiSourceConsumer"/> を実装し、<c>OscMidiTransportLifecycleProcessor</c>
    /// が Setup 前に <see cref="MidiSource"/> を注入する。
    /// </remarks>
    [NodeType("MidiCC", "MIDI CC", NodeCategory.Input)]
    public class MidiCCNode : NodeBase, IMidiSourceConsumer
    {
        private const int DefaultCCNumber = 1;
        private const int DefaultChannel = 1;

        private readonly OutputPort<float> _valueOut;
        private int _ccNumber;
        private int _channel;

        public int CCNumber => _ccNumber;
        public int Channel => _channel;

        /// <summary><c>OscMidiTransportLifecycleProcessor</c> が Setup 前に注入する。</summary>
        public IMidiSource? MidiSource { get; set; }

        public MidiCCNode(string id) : this(id, DefaultCCNumber, DefaultChannel)
        {
        }

        public MidiCCNode(string id, int ccNumber, int channel) : base(id, "MidiCC")
        {
            _ccNumber = Mathf.Clamp(ccNumber, 0, 127);
            _channel = Mathf.Clamp(channel, 1, 16);
            _valueOut = RegisterOutput<float>("Value", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            if (MidiSource == null)
            {
                Debug.LogWarning($"[MidiCCNode] MidiSource not injected. Node {Id} inactive.");
                return;
            }

            AddSubscription(
                MidiSource.GetCCObservable(_channel, _ccNumber)
                    .Subscribe(v => _valueOut.Emit(v)));
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<MidiCCParams>(paramsJson);
                _ccNumber = Mathf.Clamp(p.ccNumber, 0, 127);
                _channel = Mathf.Clamp(p.channel, 1, 16);
            }
            catch (Exception)
            {
                // 破損したJSONは無視、デフォルト値を維持
            }
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new MidiCCParams
            {
                ccNumber = _ccNumber,
                channel = _channel
            });
            return data;
        }

        [Serializable]
        private struct MidiCCParams
        {
            public int ccNumber;
            public int channel;
        }
    }
}
