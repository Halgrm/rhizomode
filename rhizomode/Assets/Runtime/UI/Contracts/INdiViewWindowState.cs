#nullable enable

using System;
using UnityEngine;

namespace Rhizomode.UI.Contracts
{
    /// <summary>
    /// NDI 受信 window の transform 永続化 side-channel。
    /// </summary>
    /// <remarks>
    /// <para>NDI 受信表示は <see cref="INdiReceiverNode"/> (source name 制御) と
    /// <see cref="INdiViewWindowState"/> (window transform 永続化) の 2 つの contract に
    /// 責務分割される。node は両方を実装するが presenter は <c>AsNdiViewWindowState()</c>
    /// 経由で window state だけを取得する。</para>
    ///
    /// <para>設計判断 (Plan v0.3 §Side-channel):</para>
    /// <list type="bullet">
    ///   <item><see cref="UnityEngine.Pose"/> ではなく <see cref="Vector3"/> × 2 + float に分解。
    ///     SharedKernel に Pose 同等型が無く、test 容易性も劣るため</item>
    ///   <item>memory <c>feedback_cue_load_invariants</c> の <c>INodeVisualRotationProvider</c>
    ///     と同じ side-channel 流儀。node の source 制御 contract を Unity 依存で汚さない</item>
    ///   <item>cue load で window が default 位置に出ないよう
    ///     <see cref="HasExplicitWindowTransform"/> flag で旧 cue (window 未保存) を区別</item>
    /// </list>
    /// </remarks>
    public interface INdiViewWindowState
    {
        /// <summary>window の world position。default = <see cref="Vector3.zero"/> (factory が HMD-forward fallback を適用)。</summary>
        Vector3 WindowPosition { get; }

        /// <summary>window の world rotation (Euler degrees)。</summary>
        Vector3 WindowEulerAngles { get; }

        /// <summary>window の uniform scale。clamp 範囲は presenter / grab handle 側で適用。</summary>
        float WindowScale { get; }

        /// <summary>
        /// true なら <see cref="WindowPosition"/> 等がユーザー操作 / cue load 由来。
        /// false なら paramsJson に window transform フィールドが無かった (旧 cue) →
        /// presenter は HMD-forward + cascade offset の default を採用する。
        /// </summary>
        bool HasExplicitWindowTransform { get; }

        /// <summary>true なら window を Mirror カメラから隠す (配信に映さない)。default false。</summary>
        bool HideFromMirror { get; }

        /// <summary><see cref="SetWindowTransform"/> 経由で値が変わったときに発火 (presenter が購読)。</summary>
        event Action? OnWindowTransformChanged;

        /// <summary>grab handle / cue load 経路から window transform を更新する。
        /// <see cref="HasExplicitWindowTransform"/> を true に flip。</summary>
        void SetWindowTransform(Vector3 position, Vector3 eulerAngles, float scale);

        /// <summary><see cref="HideFromMirror"/> を更新 (UI toggle 経路)。</summary>
        void SetHideFromMirror(bool hide);
    }
}
