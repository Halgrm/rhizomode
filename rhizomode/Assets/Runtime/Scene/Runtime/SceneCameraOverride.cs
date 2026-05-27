#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Rhizomode.Scene.Runtime
{
    /// <summary>
    /// 環境シーン load 中に対象 <see cref="Camera"/> 群の <see cref="Camera.clearFlags"/> と
    /// <see cref="Camera.backgroundColor"/> を env-local に上書きするための marker component。
    /// </summary>
    /// <remarks>
    /// <para>本コンポーネントは <b>完全 inert</b> (Codex review v0.3 ルール):</para>
    /// <list type="bullet">
    ///   <item><c>OnEnable</c> / <c>Awake</c> / <c>OnDestroy</c> から Apply は呼ばない</item>
    ///   <item>state 変化はすべて <see cref="AdditiveSceneLoader"/> が <see cref="CameraOverrideSession"/>
    ///     経由で行う</item>
    /// </list>
    ///
    /// <para><b>Authoring 契約:</b></para>
    /// <list type="bullet">
    ///   <item><see cref="targets"/> に <b>明示参照</b>で HMD camera + Mirror output camera を入れる
    ///     こと。<c>Camera.allCameras</c> の自動列挙は disabled / Cinemachine virtual /
    ///     後 spawn される NDI sender 等を誤って捕まえる / 漏らすため使わない</item>
    ///   <item>env scene には 1 個まで。複数あっても session 側で挙動は安全だが authoring の
    ///     意図が不明瞭になる</item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SceneCameraOverride : MonoBehaviour
    {
        [SerializeField] private CameraClearFlags clearFlags = CameraClearFlags.Skybox;
        [SerializeField] private Color backgroundColor = Color.black;

        [Tooltip("env-local の clear flags / backgroundColor を上書きする対象 camera。" +
                 "HMD camera + Mirror output camera を明示参照で入れる。")]
        [SerializeField] private List<Camera> targets = new();

        /// <summary>session に渡される clear flags。</summary>
        internal CameraClearFlags ClearFlags => clearFlags;

        /// <summary>session に渡される background color (clear flag が SolidColor の時に有効)。</summary>
        internal Color BackgroundColor => backgroundColor;

        /// <summary>env-local override 対象の camera リスト (read-only view)。</summary>
        internal IReadOnlyList<Camera> Targets => targets;
    }
}
