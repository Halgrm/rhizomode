#nullable enable

using System;
using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.ExternalInput
{
    /// <summary>
    /// 指定MIDI CC番号の値を受信してfloat出力するノード（0-1正規化済み）。
    /// MidiServerシングルトンのObservableを購読する。
    /// </summary>
    public class MidiCCNode : NodeBase
    {
        private const int DefaultCCNumber = 1;
        private const int DefaultChannel = 1;

        private readonly OutputPort<float> _valueOut;
        private int _ccNumber;
        private int _channel;

        public int CCNumber => _ccNumber;
        public int Channel => _channel;

        public MidiCCNode(string id) : this(id, DefaultCCNumber, DefaultChannel)
        {
        }

        public MidiCCNode(string id, int ccNumber, int channel) : base(id, "MidiCC")
        {
            _ccNumber = Mathf.Clamp(ccNumber, 0, 127);
            _channel = Mathf.Clamp(channel, 1, 16);
            _valueOut = RegisterOutput<float>("Value", ParamType.Float);
        }

        public override void Setup(GraphContext context)
        {
            var server = MidiServer.Instance;
            if (server == null)
            {
                Debug.LogWarning($"[MidiCCNode] MidiServer not found. Node {Id} inactive.");
                return;
            }

            AddSubscription(
                server.GetCCObservable(_channel, _ccNumber)
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
