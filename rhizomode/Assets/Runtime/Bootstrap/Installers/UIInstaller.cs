#nullable enable

using Rhizomode.Bootstrap.Wiring;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — UI / Cameras vertical-slice の wiring を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>UIInstaller</c>。V3d で旧 <c>GameBootstrap.InitializeVerticalSliceSystems</c>
    /// の cleanly movable 部 + 旧 <c>InitializeHealthMonitoring</c> の StatusPanel 購読を移送した
    /// <see cref="VerticalSliceBootstrapWiring"/> を <see cref="Lifetime.Singleton"/> 登録する
    /// (container が生成・所有・Dispose)。ctor の XrSceneReferences / HealthAggregator は他 Installer が
    /// 登録済 — VContainer が ctor injection で解決する。
    ///
    /// <see cref="VerticalSliceBootstrapWiring.Wire"/> は GraphContextBehaviour を transitional 引数で
    /// 要するため Build 後即時には駆動できない。GameBootstrap が CompositionRoot 経由で駆動する
    /// (一時的 Plan v5.4 違反 — V-final で解消)。
    ///
    /// §15 の <c>UIGraphAdapterInstaller</c> / <c>CamerasInstaller</c> / <c>XRInstaller</c> は
    /// V3d 時点で対応する scene-ref が既に XrSceneReferences に集約済 (UIGraphAdapter の
    /// StatusPanelController / Cameras の PathControlPointVisualManager 等) で、それらの consumer は
    /// 既存の wiring (InteractionBootstrapWiring / VerticalSliceBootstrapWiring) が ctor 注入で
    /// 直接解決するため、独立した Installer の concrete 登録内容がない。GameBootstrap 解体時
    /// (V-final) に context 専用サービスが現れる予定 — それまで本 Installer に集約する。
    /// </remarks>
    internal sealed class UIInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<VerticalSliceBootstrapWiring>(Lifetime.Singleton);
        }
    }
}
