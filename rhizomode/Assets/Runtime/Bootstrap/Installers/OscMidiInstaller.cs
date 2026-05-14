#nullable enable

using Rhizomode.Observability.Contracts;
using Rhizomode.OscMidi.GraphAdapter;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — OSC / MIDI bounded context の health monitor を登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>OscMidiInstaller</c>。V3a で GameBootstrap.InitializeHealthMonitoring が
    /// 直接 <c>HealthAggregator.Register</c> していた OSC / MIDI monitor をここへ移送。
    ///
    /// <see cref="OscServerHealth"/> / <see cref="MidiServerHealth"/> は transport が未配置でも
    /// Unknown を返す fail-open 実装 — null 参照を渡しても安全。OscServer / MidiServer 自体の
    /// container 登録 (NodeRuntime 用 LifecycleProcessor の引数) は V3b の責務。
    /// </remarks>
    internal sealed class OscMidiInstaller : IInstaller
    {
        private readonly XrSceneReferences _sceneRefs;

        public OscMidiInstaller(XrSceneReferences sceneRefs)
        {
            _sceneRefs = sceneRefs;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance<IHealthMonitor>(new OscServerHealth(_sceneRefs.OscServer));
            builder.RegisterInstance<IHealthMonitor>(new MidiServerHealth(_sceneRefs.MidiServer));
        }
    }
}
