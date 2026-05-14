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
    /// V3b で <see cref="OscMidiTransportLifecycleProcessor"/> の構築 (旧 GameBootstrap.Awake) を移送 —
    /// NodesInstaller が NodeRuntime の processor 配列へ組み込む。
    ///
    /// <see cref="OscServerHealth"/> / <see cref="MidiServerHealth"/> /
    /// <see cref="OscMidiTransportLifecycleProcessor"/> はいずれも transport 未配置でも安全に動作する
    /// fail-open 実装 — null 参照を渡してよい。
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

            // V3b: OSC/MIDI transport を IOscSourceConsumer / IMidiSourceConsumer ノードへ注入する
            // LifecycleProcessor。NodesInstaller が NodeRuntime の processor 配列へ明示順で組み込む。
            builder.RegisterInstance(new OscMidiTransportLifecycleProcessor(
                _sceneRefs.OscServer, _sceneRefs.MidiServer));
        }
    }
}
