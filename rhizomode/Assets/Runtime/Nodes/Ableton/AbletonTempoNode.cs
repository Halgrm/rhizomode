#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Ableton.Transport;
using UnityEngine;

namespace Rhizomode.Nodes.Ableton
{
    /// <summary>
    /// AbletonのテンポとビートをリアルタイムでFloat出力するノード。
    /// BPM: テンポ値そのまま。Beat: 拍番号（Remap等で正規化して使用）。
    /// </summary>
    public class AbletonTempoNode : NodeBase
    {
        private readonly OutputPort<float> _bpmOut;
        private readonly OutputPort<float> _beatOut;

        public AbletonTempoNode(string id) : base(id, "AbletonTempo")
        {
            _bpmOut = RegisterOutput<float>("BPM", ParamType.Float);
            _beatOut = RegisterOutput<float>("Beat", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            var link = AbletonLink.Instance;
            if (link == null)
            {
                Debug.LogWarning($"[AbletonTempoNode] AbletonLink not found. Node {Id} inactive.");
                return;
            }

            link.StartListening("/live/song", "tempo");
            AddSubscription(
                link.GetAddressObservable("/live/song/get/tempo")
                    .Subscribe(msg =>
                    {
                        if (msg.FloatArgs.Length > 0)
                            _bpmOut.Emit(msg.FloatArgs[0]);
                    }));

            link.StartListening("/live/song", "beat");
            AddSubscription(
                link.GetAddressObservable("/live/song/get/beat")
                    .Subscribe(msg =>
                    {
                        if (msg.FloatArgs.Length > 0)
                            _beatOut.Emit(msg.FloatArgs[0]);
                    }));

            // Dispose時にlistener解除
            AddSubscription(new ActionDisposable(() =>
            {
                link.StopListening("/live/song", "tempo");
                link.StopListening("/live/song", "beat");
            }));
        }

        /// <summary>IDisposable実装。Action実行用。</summary>
        private sealed class ActionDisposable : IDisposable
        {
            private Action? _action;
            public ActionDisposable(Action action) => _action = action;
            public void Dispose()
            {
                _action?.Invoke();
                _action = null;
            }
        }
    }
}
