#nullable enable

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

        /// <summary>表示名。displayName が空なら GameObject 名にフォールバック。</summary>
        public string DisplayName =>
            string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;

        private void OnEnable()
        {
            if (!_all.Contains(this)) _all.Add(this);
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }
    }
}
