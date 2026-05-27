#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rhizomode.Scene.Runtime
{
    /// <summary>
    /// 環境シーン load 期間中の camera 上書き state を所有する loader-owned session。
    /// </summary>
    /// <remarks>
    /// <para>設計判断 (Codex review v0.3):</para>
    /// <list type="bullet">
    ///   <item>Dict key を <see cref="Camera"/> の strong reference にする。InstanceID
    ///     経由の lookup は <c>EditorUtility.InstanceIDToObject</c> / <c>Resources.InstanceIDToObject</c>
    ///     の Editor/Runtime API 二重性を生むため避ける。strong ref なら Unity の
    ///     <c>== null</c> operator で destroyed camera を自然に弾ける。</item>
    ///   <item>Snapshot は <b>最初の Apply 時に 1 度だけ</b>取る (multi-override が同一
    ///     camera を重ねた時に「base 状態」を保持する)。</item>
    ///   <item>Session 自体は loader が field で 1 個保持し、env load 毎に作り直す →
    ///     リーク無し / multi-env 同時 active への将来拡張可能。</item>
    ///   <item><see cref="Dispose"/> = <see cref="Revert"/>。loader は env unload 時に必ず
    ///     <c>using</c> か明示 <c>Dispose</c> を呼ぶ責任を持つ。</item>
    /// </list>
    /// </remarks>
    internal sealed class CameraOverrideSession : IDisposable
    {
        private readonly Dictionary<Camera, CameraSnapshot> _snapshot = new();

        /// <summary>
        /// 与えられた <see cref="SceneCameraOverride"/> リストの設定を camera に適用し、
        /// 元の <see cref="Camera.clearFlags"/> / <see cref="Camera.backgroundColor"/> を snapshot する。
        /// </summary>
        public void Apply(IReadOnlyList<SceneCameraOverride> overrides)
            => Apply(overrides, markerCameras: null);

        /// <summary>
        /// override の設定を 「override.targets + marker cameras」 の和集合に適用する。
        /// marker cameras は <see cref="EnvOverridableCamera"/> 経由で base scene から
        /// 収集された camera 群 (cross-scene wiring の解決)。
        /// </summary>
        public void Apply(
            IReadOnlyList<SceneCameraOverride> overrides,
            IReadOnlyList<Camera>? markerCameras)
        {
            for (int i = 0; i < overrides.Count; i++)
            {
                var ovr = overrides[i];
                if (ovr == null) continue;
                ApplyOne(ovr, markerCameras);
            }
        }

        /// <summary>snapshot 済の camera 全ての clear flags / background color を元に戻す。</summary>
        public void Revert()
        {
            foreach (var pair in _snapshot)
            {
                var cam = pair.Key;
                if (cam == null) continue; // Apply と Revert の間で destroyed
                cam.clearFlags = pair.Value.ClearFlags;
                cam.backgroundColor = pair.Value.BackgroundColor;
            }
            _snapshot.Clear();
        }

        /// <inheritdoc />
        public void Dispose() => Revert();

        private void ApplyOne(SceneCameraOverride ovr, IReadOnlyList<Camera>? markerCameras)
        {
            // 1) Explicit targets (env-side で明示 ref されたもの)
            var targets = ovr.Targets;
            for (int idx = 0; idx < targets.Count; idx++)
            {
                var cam = targets[idx];
                if (cam == null)
                {
                    Debug.LogWarning(
                        $"[CameraOverrideSession] '{ovr.gameObject.scene.name}/{ovr.gameObject.name}' " +
                        $"targets[{idx}] is null (missing reference)。Inspector で再アサインすること。");
                    continue;
                }
                ApplyToCamera(cam, ovr);
            }
            // 2) Marker cameras (base scene の EnvOverridableCamera 経由)
            if (markerCameras == null) return;
            for (int i = 0; i < markerCameras.Count; i++)
            {
                var cam = markerCameras[i];
                if (cam == null) continue;
                ApplyToCamera(cam, ovr);
            }
        }

        private void ApplyToCamera(Camera cam, SceneCameraOverride ovr)
        {
            if (!_snapshot.ContainsKey(cam))
                _snapshot[cam] = new CameraSnapshot(cam.clearFlags, cam.backgroundColor);
            cam.clearFlags = ovr.ClearFlags;
            cam.backgroundColor = ovr.BackgroundColor;
        }

        /// <summary>1 camera の Apply 前 state。</summary>
        private readonly struct CameraSnapshot
        {
            public readonly CameraClearFlags ClearFlags;
            public readonly Color BackgroundColor;

            public CameraSnapshot(CameraClearFlags flags, Color bg)
            {
                ClearFlags = flags;
                BackgroundColor = bg;
            }
        }
    }
}
