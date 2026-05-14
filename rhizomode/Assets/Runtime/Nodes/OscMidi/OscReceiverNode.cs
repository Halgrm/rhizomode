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
    /// 指定OSCアドレスの値を受信してfloat出力するノード。
    /// <see cref="IOscSource"/> の Observable を購読する。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>OscServer.Instance</c> singleton 直参照を解消。
    /// <see cref="IOscSourceConsumer"/> を実装し、<c>OscMidiTransportLifecycleProcessor</c>
    /// が Setup 前に <see cref="OscSource"/> を注入する。
    /// </remarks>
    [NodeType("OscReceiver", "OSC Receiver", NodeCategory.Input)]
    public class OscReceiverNode : NodeBase, IOscSourceConsumer
    {
        private const string DefaultAddress = "/1/fader1";
        private const int DefaultPort = 9000;

        private readonly OutputPort<float> _valueOut;
        private string _address;
        private int _port;

        public string Address => _address;
        public int Port => _port;

        /// <summary><c>OscMidiTransportLifecycleProcessor</c> が Setup 前に注入する。</summary>
        public IOscSource? OscSource { get; set; }

        public OscReceiverNode(string id) : this(id, DefaultAddress, DefaultPort)
        {
        }

        public OscReceiverNode(string id, string address, int port) : base(id, "OscReceiver")
        {
            _address = address;
            _port = port;
            _valueOut = RegisterOutput<float>("Value", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            if (OscSource == null)
            {
                Debug.LogWarning($"[OscReceiverNode] OscSource not injected. Node {Id} inactive.");
                return;
            }

            AddSubscription(
                OscSource.GetAddressObservable(_address)
                    .Subscribe(v => _valueOut.Emit(Mathf.Clamp01(v))));
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<OscReceiverParams>(paramsJson);
                if (!string.IsNullOrEmpty(p.address))
                    _address = p.address;
                if (p.port > 0)
                    _port = p.port;
            }
            catch (Exception)
            {
                // 破損したJSONは無視、デフォルト値を維持
            }
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new OscReceiverParams
            {
                address = _address,
                port = _port
            });
            return data;
        }

        [Serializable]
        private struct OscReceiverParams
        {
            public string address;
            public int port;
        }
    }
}
