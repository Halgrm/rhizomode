#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using R3;
using Rhizomode.Ableton.Transport;
using UnityEngine;

namespace Rhizomode.Ableton.Session
{
    /// <summary>
    /// AbletonLive のレイアウト情報 (track 数 / scene 数 / 各クリップ名・色・存在) を
    /// OSC 経由で問い合わせる pure C# query helper。AbletonOscBridge が facade として消費する。
    /// </summary>
    public sealed class AbletonLayoutQuery
    {
        private const int InterMessageDelayMs = 5;

        /// <summary>レイアウト問い合わせの結果。AbletonOscBridge が public プロパティに反映する。</summary>
        public readonly struct Result
        {
            public readonly AbletonTrackMeta[] Tracks;
            public readonly int NumTracks;
            public readonly int NumScenes;
            public readonly bool Success;

            public Result(AbletonTrackMeta[] tracks, int numTracks, int numScenes, bool success)
            {
                Tracks = tracks;
                NumTracks = numTracks;
                NumScenes = numScenes;
                Success = success;
            }
        }

        /// <summary>
        /// AbletonLive に対しレイアウト情報を問い合わせる。完了またはタイムアウトで返る。
        /// </summary>
        /// <returns>Success=true: 全応答受信、false: タイムアウト (部分受信は反映済み)</returns>
        public async Task<Result> RunAsync(AbletonLink link, int timeoutMs)
        {
            var handle = new AbletonClipGridQueryHandle();
            using var cts = new CancellationTokenSource(timeoutMs);

            try
            {
                // Phase A: track/scene count を先に取得
                var countsOk = await QueryCountsAsync(link, handle, cts.Token);
                if (!countsOk)
                    return Finalize(handle, false);

                // Phase B: track names + clip metadata を並行的に取得
                await QueryTrackNamesAsync(link, handle, cts.Token);
                await QueryClipMetaAsync(link, handle, cts.Token);

                return Finalize(handle, true);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[AbletonOscBridge] QueryLayoutAsync timed out — using partial data");
                return Finalize(handle, false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AbletonOscBridge] QueryLayoutAsync failed: {ex.Message}");
                return Finalize(handle, false);
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

            // クリップ名・色は応答が遅れるため少し追加待機 (ベストエフォート)
            try { await Task.Delay(200, ct); }
            catch (OperationCanceledException) { /* OK */ }
        }

        /// <summary>
        /// 受信メッセージから名前文字列を抽出する。track index 後の最初の非空文字列。
        /// </summary>
        private static string ExtractName(AbletonLink.AbletonMessage msg)
        {
            // AbletonOSC の応答形式: [track, name] または [track, scene, name]
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

        private static Result Finalize(AbletonClipGridQueryHandle handle, bool success)
        {
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

            return new Result(tracks, handle.NumTracks, handle.NumScenes, success);
        }
    }

    /// <summary>
    /// AbletonLive のレイアウト情報を起動時に問い合わせる際の一時的な集計データ。
    /// QueryLayoutAsync のスコープでだけ使う。
    /// </summary>
    public class AbletonClipGridQueryHandle
    {
        public int NumTracks;
        public int NumScenes;
        public Dictionary<int, string> TrackNames = new();
        public Dictionary<(int t, int s), AbletonClipMeta> Clips = new();
    }
}
