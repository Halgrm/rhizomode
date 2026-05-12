#nullable enable

using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// 左手コントローラーのレイ情報とリグ回転を提供する。
    /// 巻物メニューなど左手操作のUI向け。
    /// </summary>
    public interface ILeftHandRay
    {
        /// <summary>左手コントローラーのレイ始点。</summary>
        Vector3 LeftRayOrigin { get; }

        /// <summary>左手コントローラーのレイ方向。</summary>
        Vector3 LeftRayDirection { get; }

        /// <summary>XR Rigの回転（スナップターン・リセンターで変化、頭の自然回転は含まない）。</summary>
        Quaternion RigRotation { get; }
    }
}
