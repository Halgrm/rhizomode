#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using Rhizomode.Ableton.Transport;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Ableton
{
    /// <summary>
    /// Abletonの指定トラックの音量を読み書きするノード。
    /// trackIndexで対象トラックを指定。入力ポートVolume接続時は双方向制御。
    /// </summary>
    [NodeType("AbletonTrackVolume", "Ableton Track Volume", NodeCategory.Input)]
    public class AbletonTrackVolumeNode : NodeBase
    {
        private const int DefaultTrackIndex = 0;

        private readonly OutputPort<float> _volumeOut;
        private readonly InputPort<float> _volumeIn;
        private int _trackIndex;

        public int TrackIndex => _trackIndex;

        public AbletonTrackVolumeNode(string id) : this(id, DefaultTrackIndex)
        {
        }

        public AbletonTrackVolumeNode(string id, int trackIndex) : base(id, "AbletonTrackVolume")
        {
            _trackIndex = Mathf.Max(0, trackIndex);
            _volumeOut = RegisterOutput<float>("Volume", ParamType.Float);
            _volumeIn = RegisterInput<float>("SetVolume", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            var link = AbletonLink.Instance;
            if (link == null)
            {
                Debug.LogWarning($"[AbletonTrackVolumeNode] AbletonLink not found. Node {Id} inactive.");
                return;
            }

            // トラック音量のlistener開始
            var listenAddr = $"/live/track/get/volume";
            link.Send("/live/track/start_listen/volume", _trackIndex);

            AddSubscription(
                link.GetAddressObservable(listenAddr)
                    .Subscribe(msg =>
                    {
                        // 応答: [track_index, volume]
                        if (msg.IntArgs.Length > 0 && msg.FloatArgs.Length > 1 &&
                            msg.IntArgs[0] == _trackIndex)
                        {
                            _volumeOut.Emit(Mathf.Clamp01(msg.FloatArgs[1]));
                        }
                    }));

            // 入力ポートから音量設定（双方向制御）
            AddSubscription(
                _volumeIn.Observable
                    .Subscribe(v =>
                    {
                        link.SendIntFloat("/live/track/set/volume", _trackIndex, Mathf.Clamp01(v));
                    }));

            AddSubscription(new ActionDisposable(() =>
            {
                link.Send("/live/track/stop_listen/volume", _trackIndex);
            }));
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<TrackVolumeParams>(paramsJson);
                _trackIndex = Mathf.Max(0, p.trackIndex);
            }
            catch (Exception)
            {
                // 破損JSONは無視
            }
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new TrackVolumeParams
            {
                trackIndex = _trackIndex
            });
            return data;
        }

        [Serializable]
        private struct TrackVolumeParams
        {
            public int trackIndex;
        }

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
