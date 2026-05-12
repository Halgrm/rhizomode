#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using R3;
using UnityEngine;

#if OSC_JACK
using OscJack;
#endif

namespace Rhizomode.ExternalInput
{
    /// <summary>
    /// OSC受信サーバーのシングルトン。OscJackパッケージが未インストールの場合はスタブとして動作。
    /// 複数のOscReceiverNodeが同一アドレスを購読可能。
    /// </summary>
    public class OscServer : MonoBehaviour
    {
        private const int DefaultPort = 9000;

        [SerializeField] private int listenPort = DefaultPort;

        private static OscServer? _instance;
        public static OscServer? Instance => _instance;

        private readonly Dictionary<string, Subject<float>> _addressSubjects = new();
        private readonly ConcurrentQueue<(string address, float value)> _pendingMessages = new();

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
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

#if OSC_JACK
            try
            {
                _server = new OscJack.OscServer(listenPort);
                Debug.Log($"[OscServer] Listening on port {listenPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OscServer] Failed to start: {ex.Message}");
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
        private void Update()
        {
            while (_pendingMessages.TryDequeue(out var msg))
            {
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

            if (_instance == this)
                _instance = null;
        }
    }
}
