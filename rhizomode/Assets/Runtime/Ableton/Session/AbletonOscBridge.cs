#nullable enable

using System;
using System.Threading.Tasks;
using Rhizomode.Ableton.Transport;
using UnityEngine;

namespace Rhizomode.Ableton.Session
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
    /// AbletonLive のレイアウト情報 (track 数/scene 数/各クリップ名・色・存在) と
    /// Device の Macro メタデータを起動時に問い合わせてキャッシュする facade。
    /// 実 query ロジックは <see cref="AbletonLayoutQuery"/> / <see cref="AbletonMacroQuery"/>
    /// (pure C#) に委譲し、本クラスは AbletonLink 取得・結果保持・イベント発火のみを担う。
    /// AbletonClipGridManager / AbletonControlPanel が結果を消費する。
    /// </summary>
    public class AbletonOscBridge : MonoBehaviour
    {
        private const int DefaultTimeoutMs = 2000;

        private readonly AbletonLayoutQuery _layoutQuery = new();
        private readonly AbletonMacroQuery _macroQuery = new();

        // ---- Layout ----
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
        /// AbletonLive に対しレイアウト情報を問い合わせる。完了またはタイムアウトで返る。
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

            var result = await _layoutQuery.RunAsync(link, timeoutMs);
            Tracks = result.Tracks;
            NumTracks = result.NumTracks;
            NumScenes = result.NumScenes;
            IsReady = true;
            OnLayoutReady?.Invoke();
            return result.Success;
        }

        /// <summary>
        /// 指定 Track / Device の Macro メタデータ (名前 / 値 / min / max) を問い合わせる。
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

            Macros = await _macroQuery.RunAsync(link, trackIndex, deviceIndex, macroCount, timeoutMs);
            IsMacrosReady = true;
            OnMacrosReady?.Invoke();
            return true;
        }
    }
}
