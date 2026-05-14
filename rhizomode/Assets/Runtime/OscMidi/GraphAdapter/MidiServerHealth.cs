#nullable enable

using Rhizomode.Observability.Contracts;
using Rhizomode.OscMidi.Contracts;

namespace Rhizomode.OscMidi.GraphAdapter
{
    /// <summary>
    /// MIDI transport の health を <see cref="IHealthMonitor"/> として公開する adapter。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 <c>MidiServer.Instance</c> singleton poll を解消し、
    /// 構築時に注入された <see cref="IMidiSource"/> 参照を直接保持する。
    /// </remarks>
    public sealed class MidiServerHealth : IHealthMonitor
    {
        public const string Id = "MIDI";

        private readonly IMidiSource? _source;

        public MidiServerHealth(IMidiSource? source)
        {
            _source = source;
        }

        public string SystemId => Id;

        public HealthSnapshot CurrentSnapshot()
        {
            // _source は interface 参照のため、backing の MonoBehaviour が Destroy されても
            // 通常の C# null チェックでは検知できない。`is UnityEngine.Object` パターンで
            // Unity の == null overload (破棄済み検知) を併用する (Codex Phase 12 review MAJOR)。
            if (_source == null ||
                (_source is UnityEngine.Object unityObj && unityObj == null))
                return new HealthSnapshot(Id, HealthStatus.Unknown, "MidiServer not running");

            return new HealthSnapshot(Id, HealthStatus.Healthy, "MidiServer active");
        }
    }
}
