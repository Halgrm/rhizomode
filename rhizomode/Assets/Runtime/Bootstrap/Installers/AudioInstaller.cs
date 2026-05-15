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
    /// Plan v5.4 §15 の <c>AudioInstaller</c>。
    ///
    /// 登録するもの (Vf-d):
    /// <list type="bullet">
    ///   <item><see cref="AudioDriverHost"/> — Lifetime.Singleton で構築。
    ///     AudioAnalyzer + GraphContextBehaviour を ctor 注入で受け取る (両者は
    ///     RootLifetimeScope.Configure が container に RegisterInstance 済)。
    ///     AudioDriverHostTickAdapter が ITickable から resolve する。</item>
    ///   <item><see cref="AudioDeviceSelectorWiring"/> — Lifetime.Singleton (container が Dispose)</item>
    ///   <item><see cref="AudioAnalyzerHealth"/> — IHealthMonitor として登録、Build 後に HealthAggregator へ</item>
    /// </list>
    ///
    /// Vf-d: 旧 <c>AudioDriverBehaviour</c> (MonoBehaviour wrapper) を廃止し、AudioDriverHost を直接
    /// container 化。XrSceneReferences が AudioAnalyzer を直接 [SerializeField] で持つ。
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
            var analyzer = _sceneRefs.AudioAnalyzer;
            if (analyzer == null)
                return;

            builder.RegisterInstance(analyzer);
            builder.Register<AudioDriverHost>(Lifetime.Singleton);
            builder.Register<AudioDeviceSelectorWiring>(Lifetime.Singleton);
            builder.RegisterInstance<IHealthMonitor>(new AudioAnalyzerHealth(analyzer));
        }
    }
}
