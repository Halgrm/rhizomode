#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using R3;
using Rhizomode.Ableton.Contracts;
using UnityEngine;

namespace Rhizomode.Ableton.Session
{
    /// <summary>
    /// AbletonLive の Device パラメータ (Macro) メタデータ (名前 / 値 / min / max) を
    /// OSC 経由で問い合わせる pure C# query helper。AbletonOscBridge が facade として消費する。
    /// </summary>
    public sealed class AbletonMacroQuery
    {
        private const int InterMessageDelayMs = 5;

        /// <summary>
        /// 指定 Track / Device の Macro メタデータを問い合わせ、AbletonMacroMeta 配列を返す。
        /// AbletonOSC の **複数形 API** (`/live/device/get/parameters/...`) を優先使用し、
        /// 古いバージョン向けに単数形 API へフォールバックする。応答配列の index 0 は
        /// "Device On" なのでスキップし、index 1..macroCount を Macro として採用する。
        /// </summary>
        /// <param name="link">接続済み AbletonLink (呼び出し側で null チェック済み)。</param>
        /// <param name="trackIndex">通常 Track は 0..N-1、Master Track は -1。</param>
        /// <param name="deviceIndex">Track 内の Device index (0 始まり)。</param>
        /// <param name="macroCount">取得する Macro 数 (呼び出し側で 1〜16 に clamp 済み)。</param>
        public async Task<AbletonMacroMeta[]> RunAsync(
            IAbletonLink link, int trackIndex, int deviceIndex, int macroCount, int timeoutMs)
        {
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

            return FinalizeMacros(
                macroCount,
                receivedNamesPlural, receivedValuesPlural, receivedMinsPlural, receivedMaxsPlural,
                nameByParamSingular, valueByParamSingular, minByParamSingular, maxByParamSingular);
        }

        /// <summary>
        /// 単数形応答 [track, device, param, name] から param→name を集計。
        /// </summary>
        private static void CollectSingularString(
            AbletonMessage msg, int track, int device, Dictionary<int, string> bucket)
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
            AbletonMessage msg, int track, int device, Dictionary<int, float> bucket)
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
            AbletonMessage msg, int track, int device, ref float[]? destination)
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
            AbletonMessage msg, int track, int device, ref string[]? destination)
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

        private static AbletonMacroMeta[] FinalizeMacros(
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

            return result;
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
