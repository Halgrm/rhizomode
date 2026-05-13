#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using Rhizomode.OscMidi.Transport;
using UnityEngine;

namespace Rhizomode.Nodes.OscMidi
{
    /// <summary>
    /// 指定OSCアドレスの値を受信してfloat出力するノード。
    /// OscServerシングルトンのObservableを購読する。
    /// </summary>
    public class OscReceiverNode : NodeBase
    {
        private const string DefaultAddress = "/1/fader1";
        private const int DefaultPort = 9000;

        private readonly OutputPort<float> _valueOut;
        private string _address;
        private int _port;

        public string Address => _address;
        public int Port => _port;

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
            var server = OscServer.Instance;
            if (server == null)
            {
                Debug.LogWarning($"[OscReceiverNode] OscServer not found. Node {Id} inactive.");
                return;
            }

            AddSubscription(
                server.GetAddressObservable(_address)
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
