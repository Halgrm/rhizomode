#nullable enable

using Rhizomode.Ableton.GraphAdapter;
using Rhizomode.Graph.Events;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Modules;
using Rhizomode.OscMidi.GraphAdapter;
using Rhizomode.Scene.GraphAdapter;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — <see cref="NodeRuntime"/> を組み立てて登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>NodesInstaller</c>。V3b で GameBootstrap.Awake が直接 new していた
    /// <see cref="NodeRuntime"/> の構築をここへ移送。
    ///
    /// processor の実行順序 (旧 GameBootstrap.Awake と同一) は重要なため、collection 解決の
    /// 登録順依存を避け、factory delegate で明示順に組み立てる:
    /// <list type="number">
    ///   <item><see cref="SceneLoaderLifecycleProcessor"/> — BeforeSetup で ISceneLoader 注入</item>
    ///   <item><see cref="OscMidiTransportLifecycleProcessor"/> — BeforeSetup で OSC/MIDI 注入</item>
    ///   <item><see cref="AbletonTransportLifecycleProcessor"/> — BeforeSetup で AbletonLink 注入</item>
    ///   <item><see cref="ModuleLifecycleProcessor"/> — AfterSetup で Prefab + IPerformanceModule 注入</item>
    /// </list>
    /// 各 processor は Scene/OscMidi/Ableton/Modules の各 Installer が登録済。
    /// <see cref="GraphState"/> / <see cref="GraphEventBus"/> は GraphInstaller が登録済。
    /// <see cref="NodeRuntime"/> は IDisposable ではないが、所有を container へ集約するため
    /// <see cref="Lifetime.Singleton"/> 登録とする。
    /// </remarks>
    internal sealed class NodesInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register(resolver => new NodeRuntime(
                resolver.Resolve<GraphState>(),
                resolver.Resolve<GraphEventBus>(),
                new INodeLifecycleProcessor[]
                {
                    resolver.Resolve<SceneLoaderLifecycleProcessor>(),
                    resolver.Resolve<OscMidiTransportLifecycleProcessor>(),
                    resolver.Resolve<AbletonTransportLifecycleProcessor>(),
                    resolver.Resolve<ModuleLifecycleProcessor>(),
                }), Lifetime.Singleton);
        }
    }
}
