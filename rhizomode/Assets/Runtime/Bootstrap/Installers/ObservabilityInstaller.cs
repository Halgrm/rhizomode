#nullable enable

using Rhizomode.Observability.Runtime;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Observability の pure-C# サービスを登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>ObservabilityInstaller</c>。V2a で GameBootstrap.InitializeHealthMonitoring が
    /// 直接 new していた <see cref="HealthAggregator"/> の構築をここへ移送。
    ///
    /// <see cref="Lifetime.Singleton"/> 登録のため container が生成・所有・Dispose する
    /// (LifetimeScope.OnDestroy 時)。GameBootstrap は resolve した aggregator に monitor を Register し、
    /// StatusPanel への購読を張るのみ — Dispose は担わない。各 system 固有の monitor 登録は
    /// V3 の Audio/OscMidi/Ableton Installer へ移送予定。
    /// </remarks>
    internal sealed class ObservabilityInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<HealthAggregator>(Lifetime.Singleton);
        }
    }
}
