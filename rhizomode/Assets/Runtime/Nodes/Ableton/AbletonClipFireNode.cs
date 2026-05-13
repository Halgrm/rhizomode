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
    /// Abletonのクリップスロットを発火・状態監視するノード。
    /// Track(Float)とScene(Float)入力ポートで対象クリップを動的に指定可能。
    /// Trigger入力のrising edgeでfire送信。
    /// </summary>
    [NodeType("AbletonClipFire", "Ableton Clip Fire", NodeCategory.Input)]
    public class AbletonClipFireNode : NodeBase
    {
        private const int DefaultTrackIndex = 0;
        private const int DefaultSceneIndex = 0;

        private readonly InputPort<float> _trackIn;
        private readonly InputPort<float> _sceneIn;
        private readonly InputPort<bool> _triggerIn;
        private readonly OutputPort<bool> _isPlayingOut;
        private int _trackIndex;
        private int _sceneIndex;

        /// <summary>現在のトラックインデックス（0始まり）。</summary>
        public int TrackIndex => _trackIndex;

        /// <summary>現在のシーンインデックス（0始まり）。</summary>
        public int SceneIndex => _sceneIndex;

        public AbletonClipFireNode(string id) : this(id, DefaultTrackIndex, DefaultSceneIndex)
        {
        }

        public AbletonClipFireNode(string id, int trackIndex, int sceneIndex) : base(id, "AbletonClipFire")
        {
            _trackIndex = Mathf.Max(0, trackIndex);
            _sceneIndex = Mathf.Max(0, sceneIndex);
            _trackIn = RegisterInput<float>("Track", ParamType.Float);
            _sceneIn = RegisterInput<float>("Scene", ParamType.Float);
            _triggerIn = RegisterInput<bool>("Trigger", ParamType.Bool);
            _isPlayingOut = RegisterOutput<bool>("IsPlaying", ParamType.Bool);
        }

        public override void Setup(GraphState context)
        {
            var link = AbletonLink.Instance;
            if (link == null)
            {
                Debug.LogWarning($"[AbletonClipFireNode] AbletonLink not found. Node {Id} inactive.");
                return;
            }

            // Track入力: Float→int変換でトラック番号を更新
            AddSubscription(
                _trackIn.Observable
                    .Subscribe(v =>
                    {
                        var newIndex = Mathf.Max(0, Mathf.RoundToInt(v));
                        if (newIndex != _trackIndex)
                        {
                            StopClipListener(link);
                            _trackIndex = newIndex;
                            StartClipListener(link);
                        }
                    }));

            // Scene入力: Float→int変換でシーン番号を更新
            AddSubscription(
                _sceneIn.Observable
                    .Subscribe(v =>
                    {
                        var newIndex = Mathf.Max(0, Mathf.RoundToInt(v));
                        if (newIndex != _sceneIndex)
                        {
                            StopClipListener(link);
                            _sceneIndex = newIndex;
                            StartClipListener(link);
                        }
                    }));

            // クリップスロット状態の監視（現在のtrack/sceneに一致する応答のみ処理）
            AddSubscription(
                link.GetAddressObservable("/live/clip_slot/get/is_playing")
                    .Subscribe(msg =>
                    {
                        if (msg.IntArgs.Length >= 3 &&
                            msg.IntArgs[0] == _trackIndex &&
                            msg.IntArgs[1] == _sceneIndex)
                        {
                            _isPlayingOut.Emit(msg.IntArgs[2] != 0);
                        }
                    }));

            // Trigger入力: rising edgeでclip fire
            AddSubscription(
                _triggerIn.Observable
                    .DistinctUntilChanged()
                    .Where(v => v)
                    .Subscribe(_ =>
                    {
                        link.Send("/live/clip_slot/fire", _trackIndex, _sceneIndex);
                    }));

            // 初期listenerを開始
            StartClipListener(link);

            // Dispose時にlistener解除
            AddSubscription(new ActionDisposable(() => StopClipListener(link)));
        }

        private void StartClipListener(AbletonLink link)
        {
            link.Send("/live/clip_slot/start_listen/is_playing", _trackIndex, _sceneIndex);
        }

        private void StopClipListener(AbletonLink link)
        {
            link.Send("/live/clip_slot/stop_listen/is_playing", _trackIndex, _sceneIndex);
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<ClipFireParams>(paramsJson);
                _trackIndex = Mathf.Max(0, p.trackIndex);
                _sceneIndex = Mathf.Max(0, p.sceneIndex);
            }
            catch (Exception)
            {
                // 破損JSONは無視
            }
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new ClipFireParams
            {
                trackIndex = _trackIndex,
                sceneIndex = _sceneIndex
            });
            return data;
        }

        [Serializable]
        private struct ClipFireParams
        {
            public int trackIndex;
            public int sceneIndex;
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
