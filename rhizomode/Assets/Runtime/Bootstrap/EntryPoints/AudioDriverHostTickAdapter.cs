#nullable enable

using Rhizomode.Audio.GraphAdapter;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.EntryPoints
{
    /// <summary>
    /// VContainer ITickable adapter — 毎フレーム <see cref="AudioDriverHost"/> の Tick を駆動する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 EntryPoints tick order #2。MainThreadCommandTicker の後、HealthAggregator の前。
    /// 詳細は EntryPoints/TickOrder.md を参照。
    ///
    /// Vf-d: 旧 AudioDriverBehaviour wrap から AudioDriverHost 直 wrap に refactor。
    /// AudioDriverHost は AudioInstaller が <see cref="VContainer.Lifetime.Singleton"/> で構築 +
    /// AudioAnalyzer / GraphContextBehaviour を ctor 注入で受け取る。lazy 構築や late-binding は
    /// container build 時に固定されるため不要 (XrSceneReferences が両者を確定的に保持する前提)。
    /// </remarks>
    public sealed class AudioDriverHostTickAdapter : ITickable
    {
        private readonly AudioDriverHost _host;

        public AudioDriverHostTickAdapter(AudioDriverHost host)
        {
            _host = host;
        }

        public void Tick() => _host.Tick();
    }
}
