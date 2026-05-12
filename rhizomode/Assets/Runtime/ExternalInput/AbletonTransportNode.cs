#nullable enable

using System;
using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.ExternalInput
{
    /// <summary>
    /// Abletonのトランスポート状態を出力するノード。
    /// IsPlaying: 再生中かどうか(bool)。SongTime: 現在の曲時間(float, beats)。
    /// </summary>
    public class AbletonTransportNode : NodeBase
    {
        private readonly OutputPort<bool> _isPlayingOut;
        private readonly OutputPort<float> _songTimeOut;

        public AbletonTransportNode(string id) : base(id, "AbletonTransport")
        {
            _isPlayingOut = RegisterOutput<bool>("IsPlaying", ParamType.Bool);
            _songTimeOut = RegisterOutput<float>("SongTime", ParamType.Float);
        }

        public override void Setup(GraphContext context)
        {
            var link = AbletonLink.Instance;
            if (link == null)
            {
                Debug.LogWarning($"[AbletonTransportNode] AbletonLink not found. Node {Id} inactive.");
                return;
            }

            link.StartListening("/live/song", "is_playing");
            AddSubscription(
                link.GetAddressObservable("/live/song/get/is_playing")
                    .Subscribe(msg =>
                    {
                        if (msg.IntArgs.Length > 0)
                            _isPlayingOut.Emit(msg.IntArgs[0] != 0);
                    }));

            link.StartListening("/live/song", "current_song_time");
            AddSubscription(
                link.GetAddressObservable("/live/song/get/current_song_time")
                    .Subscribe(msg =>
                    {
                        if (msg.FloatArgs.Length > 0)
                            _songTimeOut.Emit(msg.FloatArgs[0]);
                    }));

            AddSubscription(new ActionDisposable(() =>
            {
                link.StopListening("/live/song", "is_playing");
                link.StopListening("/live/song", "current_song_time");
            }));
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
