#nullable enable

using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Rhizomode.ExternalInput
{
    /// <summary>
    /// AbletonクリップグリッドのVR物理表現を管理する。Bridgeで取得したメタデータから
    /// キューブを自動配置し、Liveからのis_playing応答でビジュアルを更新する。
    /// </summary>
    public class AbletonClipGridManager : MonoBehaviour
    {
        [SerializeField] private GameObject? clipObjectPrefab;
        [SerializeField, Range(1, 8)] private int labelMaxChars = 4;

        // 間隔は AbletonControlPanel が源泉。SetSpacing() で外部から注入。
        private float horizontalSpacing = 0.18f;
        private float verticalSpacing = 0.18f;

        /// <summary>
        /// クリップ間隔を外部から設定する。SpawnGrid 前に呼ぶこと。
        /// </summary>
        public void SetSpacing(float horizontal, float vertical)
        {
            horizontalSpacing = Mathf.Max(0.01f, horizontal);
            verticalSpacing = Mathf.Max(0.01f, vertical);
        }

        private AbletonOscBridge? _bridge;
        private readonly Dictionary<(int t, int s), ClipObject> _clips = new();
        private IDisposable? _stateSubscription;
        private GameObject? _gridRoot;

        public bool IsSpawned => _gridRoot != null;
        public IReadOnlyDictionary<(int t, int s), ClipObject> Clips => _clips;

        public void Initialize(AbletonOscBridge bridge)
        {
            _bridge = bridge;
        }

        /// <summary>
        /// 指定位置にグリッドを生成する。配置基準はoriginを左下、X=track / Y=scene。
        /// </summary>
        public void SpawnGrid(Vector3 origin, Quaternion facing)
        {
            if (clipObjectPrefab == null)
            {
                Debug.LogError("[AbletonClipGridManager] clipObjectPrefab is not assigned");
                return;
            }
            if (_bridge == null)
            {
                Debug.LogError("[AbletonClipGridManager] Bridge not initialized");
                return;
            }

            Clear();

            _gridRoot = new GameObject("AbletonClipGrid");
            _gridRoot.transform.SetParent(transform, false);
            _gridRoot.transform.SetPositionAndRotation(origin, facing);

            var tracks = _bridge.Tracks;
            for (var t = 0; t < tracks.Length; t++)
            {
                var trackName = tracks[t].Name;
                var clips = tracks[t].Clips;
                for (var s = 0; s < clips.Length; s++)
                {
                    SpawnOne(t, s, clips[s], trackName);
                }
            }

            SubscribePlayingState();
            StartListenAll();
        }

        public ClipObject? GetClip(int track, int scene)
        {
            return _clips.TryGetValue((track, scene), out var clip) ? clip : null;
        }

        /// <summary>
        /// グリッドを完全に破棄する（Refresh等で再構築する際に呼ぶ）。
        /// 既存のlistenerもstop_listenで解除する。
        /// </summary>
        public void Clear()
        {
            StopListenAll();
            _stateSubscription?.Dispose();
            _stateSubscription = null;

            _clips.Clear();
            if (_gridRoot != null)
            {
                Destroy(_gridRoot);
                _gridRoot = null;
            }
        }

        private void SpawnOne(int track, int scene, AbletonClipMeta meta, string trackName)
        {
            if (_gridRoot == null || clipObjectPrefab == null) return;

            var localPos = new Vector3(
                track * horizontalSpacing,
                scene * verticalSpacing,
                0f);

            var instance = Instantiate(clipObjectPrefab, _gridRoot.transform);
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = Quaternion.identity;
            instance.name = $"Clip_T{track}_S{scene}";

            var clipObj = instance.GetComponent<ClipObject>();
            if (clipObj == null)
            {
                Debug.LogError($"[AbletonClipGridManager] Prefab missing ClipObject component on {instance.name}");
                Destroy(instance);
                return;
            }

            var style = new ClipObjectStyle { LabelMaxChars = labelMaxChars, EmissionScale = 1f, BaseColor = meta.Color };
            clipObj.Initialize(track, scene, meta, trackName, style);
            _clips[(track, scene)] = clipObj;
        }

        private void SubscribePlayingState()
        {
            var link = AbletonLink.Instance;
            if (link == null) return;

            _stateSubscription = link.GetAddressObservable("/live/clip_slot/get/is_playing")
                .Subscribe(msg =>
                {
                    if (msg.IntArgs.Length < 3) return;
                    var t = msg.IntArgs[0];
                    var s = msg.IntArgs[1];
                    var playing = msg.IntArgs[2] != 0;
                    if (_clips.TryGetValue((t, s), out var clip))
                        clip.SetPlayingState(playing);
                });
        }

        private void StartListenAll()
        {
            var link = AbletonLink.Instance;
            if (link == null) return;

            foreach (var key in _clips.Keys)
            {
                link.Send("/live/clip_slot/start_listen/is_playing", key.t, key.s);
            }
        }

        private void StopListenAll()
        {
            var link = AbletonLink.Instance;
            if (link == null) return;

            foreach (var key in _clips.Keys)
            {
                link.Send("/live/clip_slot/stop_listen/is_playing", key.t, key.s);
            }
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
