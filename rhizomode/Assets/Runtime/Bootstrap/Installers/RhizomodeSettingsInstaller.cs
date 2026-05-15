#nullable enable

using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — グローバル設定 <see cref="RhizomodeSettings"/> を container に登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>RhizomodeSettingsInstaller</c> (V-final Vf-d で新設、§15 リスト 19/19 完備)。
    /// <see cref="XrSceneReferences.RhizomodeSettings"/> から取得した SO 実体を
    /// <see cref="Lifetime.Singleton"/> として <see cref="ContainerBuilderExtensions.RegisterInstance"/>
    /// で登録する (SO は scene-bound ではなく asset reference のため container Dispose 時の解放は no-op)。
    /// settings が未配線の場合は登録をスキップする (consumer が optional 受け取りすれば boot は続行)。
    /// </remarks>
    internal sealed class RhizomodeSettingsInstaller : IInstaller
    {
        private readonly XrSceneReferences _sceneRefs;

        public RhizomodeSettingsInstaller(XrSceneReferences sceneRefs)
        {
            _sceneRefs = sceneRefs;
        }

        public void Install(IContainerBuilder builder)
        {
            var settings = _sceneRefs.RhizomodeSettings;
            if (settings == null) return;
            builder.RegisterInstance(settings);
        }
    }
}
