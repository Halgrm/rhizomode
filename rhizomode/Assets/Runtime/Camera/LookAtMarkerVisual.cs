#nullable enable

using System;
using UnityEngine;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// VR で空中配置する LookAt 用ハンドル (球メッシュ + SphereCollider + <see cref="LookAtTargetMarker"/>)。
    /// Right-Grip で grab、移動量は <see cref="UpdateWorldPosition"/> 経由で <see cref="OnPositionChanged"/> に流れる。
    /// </summary>
    /// <remarks>
    /// Phase 2-A (2026-05-18): PathControlPointVisual と同構造だが、
    /// Spline knot ではなく LookAt target を表すので <see cref="LookAtTargetMarker"/> を bundle する。
    /// 生成は <see cref="LookAtMarkerVisualManager"/> が担当し、本クラスは visual + grab event のみを提供する。
    /// </remarks>
    public class LookAtMarkerVisual : MonoBehaviour
    {
        /// <summary>位置が変わった時に発火する。Manager 側で list 更新等に使う (現状 transform 追従のみ)。</summary>
        public event Action<Vector3>? OnPositionChanged;

        /// <summary>付属している <see cref="LookAtTargetMarker"/> (生成時に同一 GO に attach)。</summary>
        public LookAtTargetMarker? Marker { get; private set; }

        /// <summary>
        /// Manager から呼び出される初期化。同一 GO に <see cref="LookAtTargetMarker"/> を attach し、
        /// displayName をセットする。
        /// </summary>
        public void Initialize(string displayName)
        {
            Marker = gameObject.GetComponent<LookAtTargetMarker>();
            if (Marker == null) Marker = gameObject.AddComponent<LookAtTargetMarker>();
            Marker.SetDisplayName(displayName);
        }

        /// <summary>grab 中に位置を更新する。Cinemachine LookAt は Transform 参照なので変更は自動反映される。</summary>
        public void UpdateWorldPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            OnPositionChanged?.Invoke(worldPosition);
        }
    }
}
