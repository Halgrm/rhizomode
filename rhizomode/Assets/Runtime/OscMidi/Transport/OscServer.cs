#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using R3;
using Rhizomode.OscMidi.Contracts;
using UnityEngine;

#if OSC_JACK
using OscJack;
#endif

namespace Rhizomode.OscMidi.Transport
{
    /// <summary>
    /// OSC受信サーバー。OscJackパッケージが未インストールの場合はスタブとして動作。
    /// 複数のOscReceiverNodeが同一アドレスを購読可能。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 static <c>Instance</c> singleton を解消。GameBootstrap が
    /// SerializeField で参照を保持し、<c>OscMidiTransportLifecycleProcessor</c> 経由で
    /// node に <see cref="IOscSource"/> として注入する。
    /// </remarks>
    public class OscServer : MonoBehaviour, IOscSource
    {
        private const int DefaultPort = 9000;

        /// <summary>1 frame で消化する OSC message の上限 (flood 時の main thread 食い潰し防止)。</summary>
        private const int MaxDrainPerFrame = 256;

        /// <summary>キュー高水位線。これを超えると drop counter を増やし periodic warning を出す。</summary>
        private const int OverflowWaterMark = 4096;

        /// <summary>warning を出す間隔 (秒)。</summary>
        private const float OverflowWarningIntervalSec = 1.0f;

        [SerializeField] private int listenPort = DefaultPort;

        private readonly Dictionary<string, Subject<float>> _addressSubjects = new();
        private readonly ConcurrentQueue<(string address, float value)> _pendingMessages = new();

        private long _droppedMessageCount;
        private float _nextOverflowWarningTime;

        /// <summary>累計 drop / overflow message 数 (status panel 表示用)。</summary>
        public long DroppedMessageCount => System.Threading.Interlocked.Read(ref _droppedMessageCount);

        /// <summary>現在 pending な未消化 message 数 (status panel 表示用)。</summary>
        public int PendingMessageCount => _pendingMessages.Count;

#if OSC_JACK
        private OscJack.OscServer? _server;
#endif

        /// <summary>
        /// 指定OSCアドレスの値変化Observableを取得する。
        /// </summary>
        public Observable<float> GetAddressObservable(string address)
        {
            if (!_addressSubjects.TryGetValue(address, out var subject))
            {
                subject = new Subject<float>();
                _addressSubjects[address] = subject;
                RegisterAddress(address);
            }
            return subject.AsObservable();
        }

        private void Awake()
        {
#if OSC_JACK
            try
            {
                _server = new OscJack.OscServer(listenPort);
                Debug.Log($"[OscServer] Listening on port {listenPort}");
            }
            catch (Exception ex)
            {
                // fail-open: ポート競合 (別プロセスが port を占有) は warning 級。
                // OSC 入力が無効でも video / graph 駆動は継続する (memory: feedback_health_monitor)。
                Debug.LogWarning($"[OscServer] Failed to start on port {listenPort}: {ex.Message} — OSC input disabled.");
            }
#else
            Debug.LogWarning("[OscServer] OscJack package not installed. OSC input disabled.");
#endif
        }

        private void RegisterAddress(string address)
        {
#if OSC_JACK
            if (_server == null) return;

            _server.MessageDispatcher.AddCallback(
                address,
                (string addr, OscDataHandle data) =>
                {
                    try
                    {
                        // バックグラウンドスレッドで呼ばれるためキューに詰めてUpdate()で処理
                        var value = data.GetElementAsFloat(0);
                        _pendingMessages.Enqueue((addr, value));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[OscServer] Message parse failed: {addr} — {ex.Message}");
                    }
                });
#endif
        }

        /// <summary>
        /// メインスレッドでキューを排出し、Subject経由でObserverに通知する。
        /// </summary>
        /// <remarks>
        /// flood 時の main thread 食い潰し防止に 1 frame あたり <see cref="MaxDrainPerFrame"/> 件まで処理。
        /// 残ったメッセージは次フレームに持ち越す。キュー長が <see cref="OverflowWaterMark"/> を超えた
        /// 場合は drop counter を増やし periodic warning を出す (映像は止めない)。
        /// </remarks>
        private void Update()
        {
            int drained = 0;
            while (drained < MaxDrainPerFrame && _pendingMessages.TryDequeue(out var msg))
            {
                drained++;
                try
                {
                    if (_addressSubjects.TryGetValue(msg.address, out var subject))
                        subject.OnNext(msg.value);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[OscServer] Subject emit failed: {msg.address} — {ex.Message}");
                }
            }

            // overflow 監視: queue が高水位を越えていれば drop counter を増やす + periodic warning
            int remaining = _pendingMessages.Count;
            if (remaining > OverflowWaterMark)
            {
                System.Threading.Interlocked.Add(ref _droppedMessageCount, remaining - OverflowWaterMark);
                if (Time.unscaledTime >= _nextOverflowWarningTime)
                {
                    Debug.LogWarning(
                        $"[OscServer] Queue overflow: {remaining} pending (drained {drained}/frame). " +
                        $"Total dropped: {DroppedMessageCount}");
                    _nextOverflowWarningTime = Time.unscaledTime + OverflowWarningIntervalSec;
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var subject in _addressSubjects.Values)
                subject.Dispose();
            _addressSubjects.Clear();

#if OSC_JACK
            _server?.Dispose();
            _server = null;
#endif
        }
    }
}
