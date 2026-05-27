#nullable enable

using UnityEngine;

namespace Rhizomode.Scene.Runtime
{
    /// <summary>
    /// SampleScene (base) 上の Directional Light に attach する marker。屋内 env
    /// (concrete 等) で太陽光を切るために、env の <see cref="SceneEnvironment"/> から
    /// 一時的に disable できるよう cross-scene wiring を marker 経由で行う。
    /// </summary>
    /// <remarks>
    /// <para>仕組み: <see cref="SceneEnvironment.disableBaseDirectionalLight"/> = true の env が
    /// load された時、<see cref="AdditiveSceneLoader"/> が本 marker を持つ <see cref="Light"/>
    /// (kind = Directional) を全て一時 disable する。env unload で元の <c>enabled</c> 状態に戻す。</para>
    ///
    /// <para><b>運用:</b> SampleScene の Directional Light root に attach するだけ。env シーンは
    /// <see cref="SceneEnvironment.disableBaseDirectionalLight"/> をチェックボックスで指定する。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class EnvOverridableDirectionalLight : MonoBehaviour
    {
    }
}
