#nullable enable

using UnityEngine;
using UnityEngine.Rendering;

namespace Rhizomode.Scene.Runtime
{
    /// <summary>
    /// 環境シーン load 中に Global Volume の post-FX profile を env-local の
    /// <see cref="VolumeProfile"/> に差替えるための marker component。
    /// </summary>
    /// <remarks>
    /// <para>仕組み: <see cref="Apply"/> 呼出時に同 GameObject に高優先度の
    /// <see cref="Volume"/> を動的生成して env profile を載せる。SampleScene の
    /// Global Volume (priority 0) はそのまま残し、上から覆い被せる方式。
    /// <see cref="Revert"/> で動的 <see cref="Volume"/> を破棄して元に戻す
    /// (toggle ではなく <see cref="Object.Destroy"/> = Codex v0.3 推奨)。</para>
    ///
    /// <para><b>本コンポーネントは完全 inert:</b> <c>OnEnable</c> / <c>OnDestroy</c>
    /// から Apply / Revert を呼ばない。state 変化はすべて
    /// <see cref="AdditiveSceneLoader"/> 経由。</para>
    ///
    /// <para><b>env <see cref="VolumeProfile"/> 作成契約:</b> 重要。base profile
    /// (SampleSceneProfile) が active にしている全 effect (Bloom / Vignette /
    /// Tonemapping / ColorAdjustments 等) を env profile にも <b>必ず override 済</b>
    /// で含めること。理由は URP の Volume system が同じ override property に対して
    /// priority で勝者を決める仕様のため。env profile に override が無ければ base が
    /// 漏れる (concrete セッションの bloom 漏れの根本原因)。詳細は
    /// <c>docs/SCENE_AUTHORING.md</c> 参照。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SceneVolumeOverride : MonoBehaviour
    {
        [SerializeField] private VolumeProfile? envProfile;
        [SerializeField] private int priority = 100;
        [SerializeField, Range(0f, 1f)] private float weight = 1f;

        private Volume? _runtimeVolume;

        /// <summary>
        /// 動的に <see cref="Volume"/> を生成して env profile を載せる。
        /// 既に <see cref="Apply"/> 済なら no-op (二重起動防止)。
        /// </summary>
        internal void Apply()
        {
            if (envProfile == null) return;
            if (_runtimeVolume != null) return;

            _runtimeVolume = gameObject.AddComponent<Volume>();
            _runtimeVolume.isGlobal = true;
            _runtimeVolume.sharedProfile = envProfile;
            _runtimeVolume.priority = priority;
            _runtimeVolume.weight = weight;
        }

        /// <summary>動的 <see cref="Volume"/> を破棄して base に戻す。</summary>
        /// <remarks>
        /// PlayMode は <see cref="Object.Destroy"/> (end-of-frame で破棄)、
        /// EditMode (editor preview や EditMode test) は <see cref="Object.DestroyImmediate"/>
        /// を使い分ける。<c>Destroy</c> は EditMode で deferred になり同フレームの
        /// 参照取得 (<c>GetComponent</c> 等) で stale な component が返されるため。
        /// </remarks>
        internal void Revert()
        {
            if (_runtimeVolume == null) return;
            if (Application.isPlaying) Destroy(_runtimeVolume);
            else                       DestroyImmediate(_runtimeVolume);
            _runtimeVolume = null;
        }

        /// <summary>env profile が設定されているか (validator 等で確認用)。</summary>
        internal bool HasProfile => envProfile != null;
    }
}
