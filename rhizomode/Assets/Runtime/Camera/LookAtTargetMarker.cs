#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// カメラからの「Look At ターゲット」として登録するマーカー。
    /// シーン内の対象 GO に貼ると CameraManagerPanel の Look At dropdown に表示され、
    /// 選択するとカメラがその位置を見るようになる。
    /// </summary>
    public class LookAtTargetMarker : MonoBehaviour
    {
        [SerializeField, Tooltip("Look At dropdown に表示される名前。空なら GameObject 名")]
        private string displayName = "";

        private static readonly List<LookAtTargetMarker> _all = new();

        /// <summary>現在シーン内でアクティブな全マーカー (read-only)。</summary>
        public static IReadOnlyList<LookAtTargetMarker> AllTargets => _all;

        /// <summary>
        /// マーカー一覧が変化した時に発火する static event (Add/Remove 双方)。
        /// 購読者は <see cref="AllTargets"/> を読み直して UI 等を更新する。
        /// </summary>
        /// <remarks>
        /// Phase 2-A re-fix (2026-05-18): CameraManagerPanel の LookAt dropdown が ShowDetails 経路でしか
        /// 再列挙されず、ランタイム生成 (Module 自動 marker / VR 配置 marker) で増えた marker が
        /// dropdown に反映されない問題があった。本 event を Subscribe する側で都度 dropdown 再構築する。
        /// </remarks>
        public static event Action? OnRegistryChanged;

        /// <summary>表示名。displayName が空なら GameObject 名にフォールバック。</summary>
        public string DisplayName =>
            string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;

        /// <summary>
        /// ランタイム生成時に表示名をセットする (Module/Object3D 自動 marker / VR 配置 marker 用)。
        /// </summary>
        /// <remarks>
        /// 既存の <c>displayName</c> SerializeField はそのまま保持し、scene 配置時の Inspector 設定を
        /// 邪魔しない。本 method はランタイム AddComponent からの命名専用。
        /// </remarks>
        public void SetDisplayName(string name)
        {
            var next = name ?? "";
            if (displayName == next) return;
            displayName = next;
            OnRegistryChanged?.Invoke();
        }

        /// <summary>
        /// F3 fix (Codex review): prefab 側で displayName を明示設定済の場合、ランタイム命名で
        /// 上書きしないバージョン。displayName SerializeField が空のときだけ <paramref name="name"/> を採用する。
        /// </summary>
        public void SetDisplayNameIfEmpty(string name)
        {
            if (!string.IsNullOrEmpty(displayName)) return;
            var next = name ?? "";
            if (displayName == next) return;
            displayName = next;
            OnRegistryChanged?.Invoke();
        }

        private void OnEnable()
        {
            if (!_all.Contains(this))
            {
                _all.Add(this);
                OnRegistryChanged?.Invoke();
            }
        }

        private void OnDisable()
        {
            if (_all.Remove(this))
                OnRegistryChanged?.Invoke();
        }
    }
}
