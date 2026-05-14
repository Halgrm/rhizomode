#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Ableton.Contracts;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Ableton
{
    /// <summary>
    /// Abletonのトランスポート状態を出力するノード。
    /// IsPlaying: 再生中かどうか(bool)。SongTime: 現在の曲時間(float, beats)。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>AbletonLink.Instance</c> singleton 直参照を解消。
    /// <see cref="IAbletonLinkConsumer"/> を実装し、<c>AbletonTransportLifecycleProcessor</c>
    /// が Setup 前に <see cref="Link"/> を注入する。
    /// </remarks>
    [NodeType("AbletonTransport", "Ableton Transport", NodeCategory.Input)]
    public class AbletonTransportNode : NodeBase, IAbletonLinkConsumer
    {
        private readonly OutputPort<bool> _isPlayingOut;
        private readonly OutputPort<float> _songTimeOut;

        /// <summary><c>AbletonTransportLifecycleProcessor</c> が Setup 前に注入する。</summary>
        public IAbletonLink? Link { get; set; }

        public AbletonTransportNode(string id) : base(id, "AbletonTransport")
        {
            _isPlayingOut = RegisterOutput<bool>("IsPlaying", ParamType.Bool);
            _songTimeOut = RegisterOutput<float>("SongTime", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            var link = Link;
            if (link == null)
            {
                Debug.LogWarning($"[AbletonTransportNode] AbletonLink not injected. Node {Id} inactive.");
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
