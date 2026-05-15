#nullable enable

using UnityEngine;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// アプリ全体のグローバル設定 ScriptableObject。Plan v5.4 §15 / §18 で挙げられた
    /// <c>Assets/Data/Config/RhizomodeSettings.asset</c> の実体。
    /// </summary>
    /// <remarks>
    /// Vf-d で新設 (§15 リスト 19 個目 = 最後の Installer)。現状の Plan v5.4 ではグローバルに
    /// 必要な設定値の具体 field は未指定なため、空の ScriptableObject として placeholder 化する。
    /// 後続 phase (Phase 12+ の PanelBudget / Audio sample rate 等) で field を追加していく。
    ///
    /// <see cref="Installers.RhizomodeSettingsInstaller"/> が <see cref="VContainer.Lifetime.Singleton"/>
    /// で container 登録し、consumer (将来の wiring / service) が ctor 注入で受け取る。
    /// </remarks>
    [CreateAssetMenu(fileName = "RhizomodeSettings", menuName = "Rhizomode/Settings", order = 0)]
    public sealed class RhizomodeSettings : ScriptableObject
    {
    }
}
