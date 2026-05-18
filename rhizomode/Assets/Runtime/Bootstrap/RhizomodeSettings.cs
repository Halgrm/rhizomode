#nullable enable

using UnityEngine;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// アプリ全体のグローバル設定 ScriptableObject。Plan v5.4 §15 / §18 で挙げられた
    /// <c>Assets/Data/Config/RhizomodeSettings.asset</c> の実体。
    /// </summary>
    /// <remarks>
    /// Vf-d で新設 (§15 リスト 19 個目 = 最後の Installer)。後続 phase で field を追加していく。
    ///
    /// <see cref="Installers.RhizomodeSettingsInstaller"/> が <see cref="VContainer.Lifetime.Singleton"/>
    /// で container 登録し、consumer (将来の wiring / service) が ctor 注入で受け取る。
    /// </remarks>
    [CreateAssetMenu(fileName = "RhizomodeSettings", menuName = "Rhizomode/Settings", order = 0)]
    public sealed class RhizomodeSettings : ScriptableObject
    {
        [Header("Mirror Output")]
        [Tooltip("起動時に Mirror カメラへ UI (MirrorHidden layer) を含めるかの既定値。"
               + "false = clean show output (UI 非表示)、true = リハ用に UI 込み配信。"
               + "ライブ中は CameraManagerPanel の \"Show UI in Mirror\" toggle で動的切替可。")]
        [SerializeField] private bool mirrorShowUiDefault = false;

        /// <summary>
        /// Mirror カメラの起動時 UI 可視性既定値。
        /// VerticalSliceBootstrapWiring が Activate 前に MirrorOutputController に注入する。
        /// </summary>
        public bool MirrorShowUiDefault => mirrorShowUiDefault;
    }
}
