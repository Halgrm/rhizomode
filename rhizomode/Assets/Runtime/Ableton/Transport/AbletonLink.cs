#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using R3;
using Rhizomode.Ableton.Contracts;
using UnityEngine;

#if OSC_JACK
using OscJack;
#endif

namespace Rhizomode.Ableton.Transport
{
    /// <summary>
    /// AbletonOSC双方向ブリッジ。
    /// Abletonへコマンド送信(port 11000)＋応答受信(port 11001)を管理する。
    /// Listenerの参照カウント付きstart/stop管理を提供。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 static <c>Instance</c> singleton を解消し
    /// <see cref="IAbletonLink"/> を実装。GameBootstrap が SerializeField で参照を保持し、
    /// <c>AbletonTransportLifecycleProcessor</c> 経由で node に注入する。
    /// 受信メッセージ型は <see cref="AbletonMessage"/> (Ableton.Contracts) に移送済。
    /// </remarks>
    public class AbletonLink : MonoBehaviour, IAbletonLink
    {
        private const int DefaultSendPort = 11000;
        private const int DefaultReceivePort = 11001;
        private const string DefaultHost = "127.0.0.1";

        [SerializeField] private string host = DefaultHost;
        [SerializeField] private int sendPort = DefaultSendPort;
        [SerializeField] private int receivePort = DefaultReceivePort;

        private readonly Dictionary<string, Subject<AbletonMessage>> _addressSubjects = new();
        private readonly ConcurrentQueue<AbletonMessage> _pendingMessages = new();
        private readonly Dictionary<string, int> _listenerRefCounts = new();

#if OSC_JACK
        private OscClient? _client;
        private OscJack.OscServer? _server;
#endif

        /// <summary>
        /// 指定OSCアドレスの応答Observableを取得する。
        /// </summary>
        public Observable<AbletonMessage> GetAddressObservable(string address)
        {
            if (!_addressSubjects.TryGetValue(address, out var subject))
            {
                subject = new Subject<AbletonMessage>();
                _addressSubjects[address] = subject;
            }
            return subject.AsObservable();
        }

        /// <summary>
        /// Abletonのプロパティlistenerを開始する。参照カウント管理。
        /// 例: StartListening("/live/song", "tempo") → "/live/song/start_listen/tempo"送信
        /// </summary>
        public void StartListening(string basePath, string property)
        {
            var key = $"{basePath}/{property}";
            if (!_listenerRefCounts.TryGetValue(key, out var count))
                count = 0;

            _listenerRefCounts[key] = count + 1;

            if (count == 0)
            {
                var listenAddr = $"{basePath}/start_listen/{property}";
                Send(listenAddr);
                Debug.Log($"[AbletonLink] start_listen: {listenAddr}");
            }
        }

        /// <summary>
        /// Abletonのプロパティlistenerを停止する。参照カウントが0になったときのみ送信。
        /// </summary>
        public void StopListening(string basePath, string property)
        {
            var key = $"{basePath}/{property}";
            if (!_listenerRefCounts.TryGetValue(key, out var count) || count <= 0)
                return;

            count--;
            _listenerRefCounts[key] = count;

            if (count == 0)
            {
                var stopAddr = $"{basePath}/stop_listen/{property}";
                Send(stopAddr);
                Debug.Log($"[AbletonLink] stop_listen: {stopAddr}");
            }
        }

        /// <summary>引数なしOSCメッセージ送信。</summary>
        public void Send(string address)
        {
#if OSC_JACK
            try { _client?.Send(address); }
            catch (Exception ex) { Debug.LogWarning($"[AbletonLink] Send failed: {address} — {ex.Message}"); }
#endif
        }

        /// <summary>int引数付きOSCメッセージ送信。</summary>
        public void Send(string address, int arg)
        {
#if OSC_JACK
            try { _client?.Send(address, arg); }
            catch (Exception ex) { Debug.LogWarning($"[AbletonLink] Send failed: {address} — {ex.Message}"); }
#endif
        }

        /// <summary>float引数付きOSCメッセージ送信。</summary>
        public void Send(string address, float arg)
        {
#if OSC_JACK
            try { _client?.Send(address, arg); }
            catch (Exception ex) { Debug.LogWarning($"[AbletonLink] Send failed: {address} — {ex.Message}"); }
#endif
        }

        /// <summary>int2引数付きOSCメッセージ送信（track+scene等）。</summary>
        public void Send(string address, int arg1, int arg2)
        {
#if OSC_JACK
            try { _client?.Send(address, arg1, arg2); }
            catch (Exception ex) { Debug.LogWarning($"[AbletonLink] Send failed: {address} — {ex.Message}"); }
#endif
        }

        /// <summary>int+float引数付きOSCメッセージ送信（track volume set等）。</summary>
        public void SendIntFloat(string address, int arg1, float arg2)
        {
            // OscJackにはint+float混合のSendオーバーロードがないため、
            // float2つで送信（AbletonOSCはfloatでもintでもtrack indexを受け付ける）
#if OSC_JACK
            try { _client?.Send(address, (float)arg1, arg2); }
            catch (Exception ex) { Debug.LogWarning($"[AbletonLink] Send failed: {address} — {ex.Message}"); }
#endif
        }

        /// <summary>int 3個の引数付きOSCメッセージ送信（device parameter query: track+device+param等）。</summary>
        public void SendInt3(string address, int arg1, int arg2, int arg3)
        {
#if OSC_JACK
            try { _client?.Send(address, arg1, arg2, arg3); }
            catch (Exception ex) { Debug.LogWarning($"[AbletonLink] Send failed: {address} — {ex.Message}"); }
#endif
        }

        /// <summary>int 3個 + float 1個の引数付きOSCメッセージ送信（device parameter set: track+device+param+value）。</summary>
        public void SendInt3Float(string address, int arg1, int arg2, int arg3, float arg4)
        {
#if OSC_JACK
            try { _client?.Send(address, (float)arg1, (float)arg2, (float)arg3, arg4); }
            catch (Exception ex) { Debug.LogWarning($"[AbletonLink] Send failed: {address} — {ex.Message}"); }
#endif
        }

        /// <summary>
        /// 設定UI等から接続先を変更する。既存のclient/serverを破棄して新規構築。
        /// 既存の参照カウント付きlistenerはstart_listenを再送して復元する。
        /// </summary>
        public void Reconnect(string newHost, int newSendPort, int newReceivePort)
        {
            host = newHost;
            sendPort = newSendPort;
            receivePort = newReceivePort;

#if OSC_JACK
            try
            {
                _client?.Dispose();
                _client = null;
                _server?.Dispose();
                _server = null;

                _client = new OscClient(host, sendPort);
                _server = new OscJack.OscServer(receivePort);

                _server.MessageDispatcher.AddCallback(
                    string.Empty,
                    (string addr, OscDataHandle data) =>
                    {
                        try
                        {
                            var count = data.GetElementCount();
                            var floats = new float[count];
                            var ints = new int[count];
                            var strings = new string[count];
                            for (var i = 0; i < count; i++)
                            {
                                try { floats[i] = data.GetElementAsFloat(i); } catch { floats[i] = 0f; }
                                try { ints[i] = data.GetElementAsInt(i); } catch { ints[i] = 0; }
                                try { strings[i] = data.GetElementAsString(i) ?? string.Empty; } catch { strings[i] = string.Empty; }
                            }
                            _pendingMessages.Enqueue(new AbletonMessage(addr, floats, ints, strings));
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AbletonLink] Message parse failed: {addr} — {ex.Message}");
                        }
                    });

                // 既存listenerを再送信（Live再起動時にも有効）
                foreach (var key in _listenerRefCounts.Keys)
                {
                    var lastSlash = key.LastIndexOf('/');
                    if (lastSlash <= 0) continue;
                    var basePath = key.Substring(0, lastSlash);
                    var property = key.Substring(lastSlash + 1);
                    Send($"{basePath}/start_listen/{property}");
                }

                Debug.Log($"[AbletonLink] Reconnected — send={host}:{sendPort}, receive=:{receivePort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AbletonLink] Reconnect failed: {ex.Message}");
            }
#endif
        }

        private void Awake()
        {
#if OSC_JACK
            try
            {
                _client = new OscClient(host, sendPort);
                _server = new OscJack.OscServer(receivePort);

                // 空文字キーでワイルドカード受信（全メッセージをキャッチ）
                _server.MessageDispatcher.AddCallback(
                    string.Empty,
                    (string addr, OscDataHandle data) =>
                    {
                        try
                        {
                            var count = data.GetElementCount();
                            var floats = new float[count];
                            var ints = new int[count];
                            var strings = new string[count];
                            for (var i = 0; i < count; i++)
                            {
                                // 各引数を3つ全てに格納試行（型不一致は0/空文字フォールバック）。
                                // 受信側ノードが期待する型のArrayから読めばよい設計。
                                try { floats[i] = data.GetElementAsFloat(i); } catch { floats[i] = 0f; }
                                try { ints[i] = data.GetElementAsInt(i); } catch { ints[i] = 0; }
                                try { strings[i] = data.GetElementAsString(i) ?? string.Empty; } catch { strings[i] = string.Empty; }
                            }
                            _pendingMessages.Enqueue(new AbletonMessage(addr, floats, ints, strings));
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AbletonLink] Message parse failed: {addr} — {ex.Message}");
                        }
                    });

                Debug.Log($"[AbletonLink] Connected — send={host}:{sendPort}, receive=:{receivePort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AbletonLink] Failed to start: {ex.Message}");
            }
#else
            Debug.LogWarning("[AbletonLink] OscJack package not installed. Ableton OSC disabled.");
#endif
        }

        private void Update()
        {
            while (_pendingMessages.TryDequeue(out var msg))
            {
                try
                {
                    if (_addressSubjects.TryGetValue(msg.Address, out var subject))
                        subject.OnNext(msg);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AbletonLink] Subject emit failed: {msg.Address} — {ex.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var subject in _addressSubjects.Values)
                subject.Dispose();
            _addressSubjects.Clear();
            _listenerRefCounts.Clear();

#if OSC_JACK
            _client?.Dispose();
            _client = null;
            _server?.Dispose();
            _server = null;
#endif
        }
    }
}
