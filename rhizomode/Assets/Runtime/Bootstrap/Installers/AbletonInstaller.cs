#nullable enable

using Rhizomode.Ableton.GraphAdapter;
using Rhizomode.Bootstrap.Wiring;
using Rhizomode.Observability.Contracts;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Ableton bounded context の wiring と health monitor を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>AbletonInstaller</c>。V3a で GameBootstrap.Ableton.cs (~380 行) +
    /// InitializeAbletonOsc + Ableton health monitor をここへ移送。
    ///
    /// 登録するもの:
    /// <list type="bullet">
    ///   <item><see cref="AbletonBootstrapWiring"/> — Lifetime.Singleton (container が Dispose)</item>
    ///   <item><see cref="AbletonLinkHealth"/> — IHealthMonitor として登録</item>
    ///   <item><see cref="AbletonTransportLifecycleProcessor"/> — V3b: NodeRuntime processor (旧 GameBootstrap.Awake)</item>
    /// </list>
    /// <c>AbletonBootstrapWiring.Wire()</c> は VR/Desktop の入力ルーターと SharedRaycastService を
    /// 要するため Build 後即時には駆動できない。GameBootstrap が InteractionHandlers 初期化後に
    /// CompositionRoot 経由で駆動する (一時的 Plan v5.4 違反 — V3c で解消)。
    /// </remarks>
    internal sealed class AbletonInstaller : IInstaller
    {
        private readonly XrSceneReferences _sceneRefs;

        public AbletonInstaller(XrSceneReferences sceneRefs)
        {
            _sceneRefs = sceneRefs;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<AbletonBootstrapWiring>(Lifetime.Singleton);
            builder.RegisterInstance<IHealthMonitor>(new AbletonLinkHealth(_sceneRefs.AbletonLink));

            // V3b: AbletonLink を IAbletonLinkConsumer ノードへ注入する LifecycleProcessor。
            // NodesInstaller が NodeRuntime の processor 配列へ明示順で組み込む。
            builder.RegisterInstance(new AbletonTransportLifecycleProcessor(_sceneRefs.AbletonLink));
        }
    }
}
