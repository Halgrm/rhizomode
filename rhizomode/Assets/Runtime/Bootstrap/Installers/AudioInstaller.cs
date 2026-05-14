#nullable enable

using Rhizomode.Audio.GraphAdapter;
using Rhizomode.Bootstrap.Wiring;
using Rhizomode.Observability.Contracts;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Audio bounded context の scene 参照とサービスを登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>AudioInstaller</c>。V3a で GameBootstrap が直接握っていた
    /// <see cref="AudioDriverBehaviour"/> 参照、AudioDeviceSelector の wiring、Audio health monitor を
    /// ここへ移送。
    ///
    /// 登録するもの:
    /// <list type="bullet">
    ///   <item><see cref="AudioDriverBehaviour"/> — <c>AudioDriverHostTickAdapter</c> が resolve</item>
    ///   <item><see cref="AudioDeviceSelectorWiring"/> — Lifetime.Singleton (container が Dispose)</item>
    ///   <item><see cref="AudioAnalyzerHealth"/> — IHealthMonitor として登録、Build 後に HealthAggregator へ</item>
    /// </list>
    /// <c>AudioDeviceSelectorWiring.Wire()</c> の副作用駆動は Build 後の eager step
    /// (<c>EntryPointBootstrapper</c>) が行う。
    /// </remarks>
    internal sealed class AudioInstaller : IInstaller
    {
        private readonly XrSceneReferences _sceneRefs;

        public AudioInstaller(XrSceneReferences sceneRefs)
        {
            _sceneRefs = sceneRefs;
        }

        public void Install(IContainerBuilder builder)
        {
            if (_sceneRefs.AudioDriver != null)
                builder.RegisterInstance(_sceneRefs.AudioDriver);

            builder.Register<AudioDeviceSelectorWiring>(Lifetime.Singleton);

            var analyzer = _sceneRefs.AudioDriver != null ? _sceneRefs.AudioDriver.Analyzer : null;
            if (analyzer != null)
                builder.RegisterInstance<IHealthMonitor>(new AudioAnalyzerHealth(analyzer));
        }
    }
}
