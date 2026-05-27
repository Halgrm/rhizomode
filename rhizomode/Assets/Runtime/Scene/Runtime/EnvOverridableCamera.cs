#nullable enable

using UnityEngine;

namespace Rhizomode.Scene.Runtime
{
    /// <summary>
    /// SampleScene (base) 上の <see cref="Camera"/> に attach する marker。env シーンの
    /// <see cref="SceneCameraOverride"/> から cross-scene 直接参照できない問題を解決する。
    /// </summary>
    /// <remarks>
    /// <para>仕組み: <see cref="AdditiveSceneLoader"/> が env load 時に全ロード済シーンから
    /// 本 marker を持つ camera を収集し、env の <see cref="SceneCameraOverride"/> の
    /// clear flags / background color で上書きする。env unload で
    /// <see cref="CameraOverrideSession"/> が元の値に revert する。</para>
    ///
    /// <para><b>運用:</b> SampleScene の Main HMD camera + Mirror output camera 等
    /// "env 切替えで clear 色を変えたい" camera に attach。env シーン側は
    /// <see cref="SceneCameraOverride.targets"/> を空のままで OK (marker が自動 wiring する)。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class EnvOverridableCamera : MonoBehaviour
    {
    }
}
