#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using R3;
using UnityEngine;

namespace Rhizomode.ExternalInput
{
    /// <summary>
    /// 1つのクリップスロットのメタデータ。
    /// </summary>
    public struct AbletonClipMeta
    {
        public bool HasClip;
        public string Name;
        public Color Color;
    }

    /// <summary>
    /// 1つのトラックのメタデータ（クリップ配列を含む）。
    /// </summary>
    public struct AbletonTrackMeta
    {
        public string Name;
        public AbletonClipMeta[] Clips;  // index = scene
    }

    /// <summary>
    /// 1つの Macro パラメータのメタデータ。Live の Rack の Macro Knob 1 個に対応。
    /// </summary>
    public struct AbletonMacroMeta
    {
        /// <summary>OSC で device パラメータを指定する index。0=Device On、1〜=Macro。</summary>
        public int ParamId;
        public string Name;
        public float Value;
        public float Min;
        public float Max;
    }

    /// <summary>
    /// AbletonLiveのレイアウト情報（track数/scene数/各クリップ名・色・存在）を
    /// 起動時に問い合わせてキャッシュする。AbletonClipGridManagerが消費する。
    /// </summary>
    public class AbletonClipGridQueryHandle
    {
        // QueryLayoutAsyncのスコープでだけ使う一時的な集計データ。
        public int NumTracks;
        public int NumScenes;
        public Dictionary<int, string> TrackNames = new();
        public Dictionary<(int t, int s), AbletonClipMeta> Clips = new();
    }

    public class AbletonOscBridge : MonoBehaviour
    {
        private const int DefaultTimeoutMs = 2000;
        private const int InterMessageDelayMs = 5;

        public AbletonTrackMeta[] Tracks { get; private set; } = Array.Empty<AbletonTrackMeta>();
        public int NumTracks { get; private set; }
        public int NumScenes { get; private set; }
        public bool IsReady { get; private set; }

        public event Action? OnLayoutReady;

        // ---- Macros ----
        public AbletonMacroMeta[] Macros { get; private set; } = Array.Empty<AbletonMacroMeta>();
        public int MacroTrackIndex { get; private set; }
        public int MacroDeviceIndex { get; private set; }
        public bool IsMacrosReady { get; private set; }

        public event Action? OnMacrosReady;

        /// <summary>
        /// AbletonLiveに対しレイアウト情報を問い合わせる。完了またはタイムアウトで返る。
        /// </summary>
        /// <returns>true: 全応答受信、false: タイムアウト（部分受信は反映済み）</returns>
        public async Task<bool> QueryLayoutAsync(int timeoutMs = DefaultTimeoutMs)
        {
            var link = AbletonLink.Instance;
            if (link == null)
            {
                Debug.LogWarning("[AbletonOscBridge] AbletonLink not available — empty layout");
                Tracks = Array.Empty<AbletonTrackMeta>();
                IsReady = true;
                OnLayoutReady?.Invoke();
                return true;
            }

            var handle = new AbletonClipGridQueryHandle();
            using var cts = new CancellationTokenSource(timeoutMs);

            try
            {
                // Phase A: track/scene count を先に取得
                var countsOk = await QueryCountsAsync(link, handle, cts.Token);
                if (!countsOk)
                {
                    Finalize(handle);
                    return false;
                }

                // Phase B: track names + clip metadata を並行的に取得
                await QueryTrackNamesAsync(link, handle, cts.Token);
                await QueryClipMetaAsync(link, handle, cts.Token);

                Finalize(handle);
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[AbletonOscBridge] QueryLayoutAsync timed out — using partial data");
                Finalize(handle);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AbletonOscBridge] QueryLayoutAsync failed: {ex.Message}");
                Finalize(handle);
                return false;
            }
        }

        private async Task<bool> QueryCountsAsync(
            AbletonLink link, AbletonClipGridQueryHandle handle, CancellationToken ct)
        {
            var tcsTracks = new TaskCompletionSource<int>();
            var tcsScenes = new TaskCompletionSource<int>();

            using var sub1 = link.GetAddressObservable("/live/song/get/num_tracks")
                .Take(1)
                .Subscribe(msg =>
                {
                    if (msg.IntArgs.Length > 0) tcsTracks.TrySetResult(msg.IntArgs[0]);
                });

            using var sub2 = link.GetAddressObservable("/live/song/get/num_scenes")
                .Take(1)
                .Subscribe(msg =>
                {
                    if (msg.IntArgs.Length > 0) tcsScenes.TrySetResult(msg.IntArgs[0]);
                });

            link.Send("/live/song/get/num_tracks");
            link.Send("/live/song/get/num_scenes");

            using (ct.Register(() =>
            {
                tcsTracks.TrySetCanceled();
                tcsScenes.TrySetCanceled();
            }))
            {
                handle.NumTracks = await tcsTracks.Task;
                handle.NumScenes = await tcsScenes.Task;
            }

            return handle.NumTracks > 0 && handle.NumScenes > 0;
        }

        private async Task QueryTrackNamesAsync(
            AbletonLink link, AbletonClipGridQueryHandle handle, CancellationToken ct)
        {
            var pending = handle.NumTracks;
            var tcs = new TaskCompletionSource<bool>();

            using var sub = link.GetAddressObservable("/live/track/get/name")
                .Subscribe(msg =>
                {
                    if (msg.IntArgs.Length < 1) return;
                    var t = msg.IntArgs[0];
                    if (t < 0 || t >= handle.NumTracks) return;
                    if (handle.TrackNames.ContainsKey(t)) return;

                    var name = ExtractName(msg);
                    handle.TrackNames[t] = name;

                    if (Interlocked.Decrement(ref pending) == 0)
                        tcs.TrySetResult(true);
                });

            for (var t = 0; t < handle.NumTracks; t++)
            {
                link.Send("/live/track/get/name", t);
                await Task.Delay(InterMessageDelayMs, ct);
            }

            using (ct.Register(() => tcs.TrySetCanceled()))
            {
                try { await tcs.Task; }
                catch (OperationCanceledException) { /* 部分受信で継続 */ }
            }
        }

        private async Task QueryClipMetaAsync(
            AbletonLink link, AbletonClipGridQueryHandle handle, CancellationToken ct)
        {
            var hasClipExpected = handle.NumTracks * handle.NumScenes;
            var hasClipReceived = 0;
            var tcsHasClip = new TaskCompletionSource<bool>();

            using var subHas = link.GetAddressObservable("/live/clip_slot/get/has_clip")
                .Subscribe(msg =>
                {
                    if (msg.IntArgs.Length < 3) return;
                    var t = msg.IntArgs[0];
                    var s = msg.IntArgs[1];
                    var has = msg.IntArgs[2] != 0;

                    var key = (t, s);
                    if (!handle.Clips.TryGetValue(key, out var meta))
                        meta = new AbletonClipMeta { Color = Color.gray };
                    meta.HasClip = has;
                    handle.Clips[key] = meta;

                    if (has)
                    {
                        link.Send("/live/clip/get/name", t, s);
                        link.Send("/live/clip/get/color", t, s);
                    }

                    if (Interlocked.Increment(ref hasClipReceived) >= hasClipExpected)
                        tcsHasClip.TrySetResult(true);
                });

            using var subName = link.GetAddressObservable("/live/clip/get/name")
                .Subscribe(msg =>
                {
                    if (msg.IntArgs.Length < 2) return;
                    var t = msg.IntArgs[0];
                    var s = msg.IntArgs[1];
                    var name = ExtractName(msg);
                    var key = (t, s);

                    if (!handle.Clips.TryGetValue(key, out var meta))
                        meta = new AbletonClipMeta { Color = Color.gray };
                    meta.Name = name;
                    handle.Clips[key] = meta;
                });

            using var subColor = link.GetAddressObservable("/live/clip/get/color")
                .Subscribe(msg =>
                {
                    if (msg.IntArgs.Length < 3) return;
                    var t = msg.IntArgs[0];
                    var s = msg.IntArgs[1];
                    var rgb = msg.IntArgs[2];
                    var key = (t, s);

                    if (!handle.Clips.TryGetValue(key, out var meta))
                        meta = new AbletonClipMeta { Color = Color.gray };
                    meta.Color = IntToColor(rgb);
                    handle.Clips[key] = meta;
                });

            for (var t = 0; t < handle.NumTracks; t++)
            {
                for (var s = 0; s < handle.NumScenes; s++)
                {
                    link.Send("/live/clip_slot/get/has_clip", t, s);
                    await Task.Delay(InterMessageDelayMs, ct);
                }
            }

            using (ct.Register(() => tcsHasClip.TrySetCanceled()))
            {
                try { await tcsHasClip.Task; }
                catch (OperationCanceledException) { /* 部分受信で継続 */ }
            }

            // クリップ名・色は応答が遅れるため少し追加待機（ベストエフォート）
            try { await Task.Delay(200, ct); }
            catch (OperationCanceledException) { /* OK */ }
        }

        /// <summary>
        /// 受信メッセージから名前文字列を抽出する。track index後の最初の非空文字列。
        /// </summary>
        private static string ExtractName(AbletonLink.AbletonMessage msg)
        {
            // AbletonOSCの応答形式: [track, name] または [track, scene, name]
            for (var i = msg.IntArgs.Length; i < msg.StringArgs.Length; i++)
            {
                if (!string.IsNullOrEmpty(msg.StringArgs[i]))
                    return msg.StringArgs[i];
            }
            // フォールバック: 文字列引数を末尾から探す
            for (var i = msg.StringArgs.Length - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(msg.StringArgs[i]))
                    return msg.StringArgs[i];
            }
            return string.Empty;
        }

        private static Color IntToColor(int rgb)
        {
            var r = ((rgb >> 16) & 0xFF) / 255f;
            var g = ((rgb >> 8) & 0xFF) / 255f;
            var b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b, 1f);
        }

        private void Finalize(AbletonClipGridQueryHandle handle)
        {
            NumTracks = handle.NumTracks;
            NumScenes = handle.NumScenes;

            var tracks = new AbletonTrackMeta[handle.NumTracks];
            for (var t = 0; t < handle.NumTracks; t++)
            {
                var clips = new AbletonClipMeta[handle.NumScenes];
                for (var s = 0; s < handle.NumScenes; s++)
                {
                    if (handle.Clips.TryGetValue((t, s), out var meta))
                        clips[s] = meta;
                    else
                        clips[s] = new AbletonClipMeta { HasClip = false, Name = string.Empty, Color = Color.gray };
                }

                handle.TrackNames.TryGetValue(t, out var trackName);
                tracks[t] = new AbletonTrackMeta { Name = trackName ?? string.Empty, Clips = clips };
            }

            Tracks = tracks;
            IsReady = true;
            OnLayoutReady?.Invoke();
        }

        // ====================================================================
        // Macros
        // ====================================================================

        /// <summary>
        /// 指定 Track / Device の Macro メタデータ (名前 / 値 / min / max) を問い合わせる。
        /// AbletonOSC の **複数形 API** (`/live/device/get/parameters/...`) を使用 — 単数形 get は
        /// 公式に存在しないため一括取得が必要。応答配列の index 0 は "Device On" なのでスキップし、
        /// index 1..macroCount を Macro として採用する。
        /// </summary>
        /// <param name="trackIndex">通常 Track は 0..N-1、Master Track は -1。</param>
        /// <param name="deviceIndex">Track 内の Device index (0 始まり)。</param>
        /// <param name="macroCount">取得する Macro 数 (1〜16)。</param>
        public async Task<bool> QueryMacrosAsync(
            int trackIndex, int deviceIndex, int macroCount, int timeoutMs = DefaultTimeoutMs)
        {
            MacroTrackIndex = trackIndex;
            MacroDeviceIndex = deviceIndex;

            macroCount = Mathf.Clamp(macroCount, 1, 16);

            var link = AbletonLink.Instance;
            if (link == null)
            {
                Debug.LogWarning("[AbletonOscBridge] AbletonLink not available — empty macros");
                Macros = Array.Empty<AbletonMacroMeta>();
                IsMacrosReady = true;
                OnMacrosReady?.Invoke();
                return true;
            }

            // 複数形応答 ([track, device, item0, item1, ...]) をそのまま配列で保持
            string[]? receivedNamesPlural = null;
            float[]? receivedValuesPlural = null;
            float[]? receivedMinsPlural = null;
            float[]? receivedMaxsPlural = null;

            // 単数形応答のフォールバック (paramId → value)。AbletonOSC のバージョンが古く
            // 複数形 get を実装していない場合に使う
            var nameByParamSingular = new Dictionary<int, string>();
            var valueByParamSingular = new Dictionary<int, float>();
            var minByParamSingular = new Dictionary<int, float>();
            var maxByParamSingular = new Dictionary<int, float>();

            using var cts = new CancellationTokenSource(timeoutMs);

            try
            {
                // ---- 複数形ハンドラ ----
                using var subNamesPlural = link.GetAddressObservable("/live/device/get/parameters/name")
                    .Subscribe(msg => CollectStringList(msg, trackIndex, deviceIndex, ref receivedNamesPlural));
                using var subValuesPlural = link.GetAddressObservable("/live/device/get/parameters/value")
                    .Subscribe(msg => CollectFloatList(msg, trackIndex, deviceIndex, ref receivedValuesPlural));
                using var subMinsPlural = link.GetAddressObservable("/live/device/get/parameters/min")
                    .Subscribe(msg => CollectFloatList(msg, trackIndex, deviceIndex, ref receivedMinsPlural));
                using var subMaxsPlural = link.GetAddressObservable("/live/device/get/parameters/max")
                    .Subscribe(msg => CollectFloatList(msg, trackIndex, deviceIndex, ref receivedMaxsPlural));

                // ---- 単数形ハンドラ (フォールバック) ----
                using var subNameSingular = link.GetAddressObservable("/live/device/get/parameter/name")
                    .Subscribe(msg => CollectSingularString(msg, trackIndex, deviceIndex, nameByParamSingular));
                using var subValueSingular = link.GetAddressObservable("/live/device/get/parameter/value")
                    .Subscribe(msg => CollectSingularFloat(msg, trackIndex, deviceIndex, valueByParamSingular));
                using var subMinSingular = link.GetAddressObservable("/live/device/get/parameter/min")
                    .Subscribe(msg => CollectSingularFloat(msg, trackIndex, deviceIndex, minByParamSingular));
                using var subMaxSingular = link.GetAddressObservable("/live/device/get/parameter/max")
                    .Subscribe(msg => CollectSingularFloat(msg, trackIndex, deviceIndex, maxByParamSingular));

                // 複数形 query (1回ずつ)
                link.Send("/live/device/get/parameters/name", trackIndex, deviceIndex);
                await Task.Delay(InterMessageDelayMs, cts.Token);
                link.Send("/live/device/get/parameters/value", trackIndex, deviceIndex);
                await Task.Delay(InterMessageDelayMs, cts.Token);
                link.Send("/live/device/get/parameters/min", trackIndex, deviceIndex);
                await Task.Delay(InterMessageDelayMs, cts.Token);
                link.Send("/live/device/get/parameters/max", trackIndex, deviceIndex);
                await Task.Delay(InterMessageDelayMs, cts.Token);

                // 単数形 query (paramId 1..macroCount を個別に問い合わせ)
                for (var p = 1; p <= macroCount; p++)
                {
                    link.SendInt3("/live/device/get/parameter/name", trackIndex, deviceIndex, p);
                    link.SendInt3("/live/device/get/parameter/value", trackIndex, deviceIndex, p);
                    link.SendInt3("/live/device/get/parameter/min", trackIndex, deviceIndex, p);
                    link.SendInt3("/live/device/get/parameter/max", trackIndex, deviceIndex, p);
                    await Task.Delay(InterMessageDelayMs, cts.Token);
                }

                // 応答が揃うまで待つ
                try { await Task.Delay(timeoutMs, cts.Token); }
                catch (OperationCanceledException) { /* OK */ }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[AbletonOscBridge] QueryMacrosAsync timed out — using partial data");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AbletonOscBridge] QueryMacrosAsync failed: {ex.Message}");
            }

            // 診断: どのエンドポイントが応答したかをログ出力 (バージョン特定の手がかり)
            Debug.Log(
                $"[AbletonOscBridge] Macro query results — " +
                $"plural names: {(receivedNamesPlural?.Length ?? 0)}, " +
                $"plural values: {(receivedValuesPlural?.Length ?? 0)}, " +
                $"plural mins: {(receivedMinsPlural?.Length ?? 0)}, " +
                $"plural maxs: {(receivedMaxsPlural?.Length ?? 0)}, " +
                $"singular names: {nameByParamSingular.Count}, " +
                $"singular values: {valueByParamSingular.Count}");

            FinalizeMacros(
                macroCount,
                receivedNamesPlural, receivedValuesPlural, receivedMinsPlural, receivedMaxsPlural,
                nameByParamSingular, valueByParamSingular, minByParamSingular, maxByParamSingular);
            return true;
        }

        /// <summary>
        /// 単数形応答 [track, device, param, name] から param→name を集計。
        /// </summary>
        private static void CollectSingularString(
            AbletonLink.AbletonMessage msg, int track, int device, Dictionary<int, string> bucket)
        {
            if (msg.IntArgs.Length < 3) return;
            if (msg.IntArgs[0] != track || msg.IntArgs[1] != device) return;
            var paramId = msg.IntArgs[2];
            // index 3 以降の string を採用 (空でないもの)
            for (var i = 3; i < msg.StringArgs.Length; i++)
            {
                if (!string.IsNullOrEmpty(msg.StringArgs[i]))
                {
                    bucket[paramId] = msg.StringArgs[i];
                    return;
                }
            }
        }

        /// <summary>
        /// 単数形応答 [track, device, param, value] から param→value を集計。
        /// </summary>
        private static void CollectSingularFloat(
            AbletonLink.AbletonMessage msg, int track, int device, Dictionary<int, float> bucket)
        {
            if (msg.IntArgs.Length < 3) return;
            if (msg.IntArgs[0] != track || msg.IntArgs[1] != device) return;
            var paramId = msg.IntArgs[2];
            var value = msg.FloatArgs.Length > 3 ? msg.FloatArgs[3] : 0f;
            bucket[paramId] = value;
        }

        /// <summary>
        /// 複数形 API の応答 [track, device, val0, val1, ...] から float 配列を抽出。
        /// </summary>
        private static void CollectFloatList(
            AbletonLink.AbletonMessage msg, int track, int device, ref float[]? destination)
        {
            if (msg.IntArgs.Length < 2) return;
            if (msg.IntArgs[0] != track || msg.IntArgs[1] != device) return;

            // float 値は index 2 以降。AbletonOSC の応答は FloatArgs に並ぶ
            var len = Mathf.Max(0, msg.FloatArgs.Length - 2);
            var arr = new float[len];
            for (var i = 0; i < len; i++)
                arr[i] = msg.FloatArgs[i + 2];
            destination = arr;
        }

        /// <summary>
        /// 複数形 API の応答 [track, device, name0, name1, ...] から string 配列を抽出。
        /// </summary>
        private static void CollectStringList(
            AbletonLink.AbletonMessage msg, int track, int device, ref string[]? destination)
        {
            if (msg.IntArgs.Length < 2) return;
            if (msg.IntArgs[0] != track || msg.IntArgs[1] != device) return;

            // string 引数は index 2 以降
            var len = Mathf.Max(0, msg.StringArgs.Length - 2);
            var arr = new string[len];
            for (var i = 0; i < len; i++)
                arr[i] = msg.StringArgs[i + 2] ?? string.Empty;
            destination = arr;
        }

        private void FinalizeMacros(
            int macroCount,
            string[]? namesPlural,
            float[]? valuesPlural,
            float[]? minsPlural,
            float[]? maxsPlural,
            Dictionary<int, string> namesSingular,
            Dictionary<int, float> valuesSingular,
            Dictionary<int, float> minsSingular,
            Dictionary<int, float> maxsSingular)
        {
            var result = new AbletonMacroMeta[macroCount];
            // 配列 index 0 は "Device On"。Macro は index 1..
            const float DefaultMacroMin = 0f;
            const float DefaultMacroMax = 127f;

            for (var i = 0; i < macroCount; i++)
            {
                var paramId = i + 1;

                // 複数形配列を最優先、無ければ単数形辞書、それも無ければデフォルト
                var name = PickString(paramId, namesPlural, namesSingular);
                var v    = PickFloat (paramId, valuesPlural, valuesSingular, 0f);
                var min  = PickFloat (paramId, minsPlural,   minsSingular,   DefaultMacroMin);
                var max  = PickFloat (paramId, maxsPlural,   maxsSingular,   DefaultMacroMax);
                if (Mathf.Approximately(min, max)) max = min + 1f;

                result[i] = new AbletonMacroMeta
                {
                    ParamId = paramId,
                    Name = string.IsNullOrEmpty(name) ? $"M{paramId:D2}" : name,
                    Value = v,
                    Min = min,
                    Max = max
                };
            }

            Macros = result;
            IsMacrosReady = true;
            OnMacrosReady?.Invoke();
        }

        private static string? PickString(int paramId, string[]? plural, Dictionary<int, string> singular)
        {
            if (plural != null && paramId < plural.Length && !string.IsNullOrEmpty(plural[paramId]))
                return plural[paramId];
            if (singular.TryGetValue(paramId, out var s) && !string.IsNullOrEmpty(s))
                return s;
            return null;
        }

        private static float PickFloat(int paramId, float[]? plural, Dictionary<int, float> singular, float fallback)
        {
            if (plural != null && paramId < plural.Length)
                return plural[paramId];
            if (singular.TryGetValue(paramId, out var v))
                return v;
            return fallback;
        }
    }
}
