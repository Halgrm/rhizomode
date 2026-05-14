#nullable enable

using Rhizomode.Bootstrap.EntryPoints;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — ITickable adapter 群を Plan v5.4 §15 の tick 順序で登録する。
    /// </summary>
    /// <remarks>
    /// tick 順序 (登録順 = VContainer の Tick 駆動順):
    /// <list type="number">
    ///   <item>MainThreadCommandTicker      — background queue → メインスレッド反映</item>
    ///   <item>AudioDriverHostTickAdapter   — audio frame → graph 駆動 (任意、未配置ならスキップ)</item>
    ///   <item>HealthAggregatorTickAdapter  — 各 system の health polling (低頻度)</item>
    /// </list>
    /// 理由は EntryPoints/TickOrder.md を参照。新規 ITickable 追加時は TickOrder.md も更新すること。
    /// </remarks>
    public sealed class EntryPointsInstaller : IInstaller
    {
        private readonly bool _includeAudioDriver;

        /// <param name="includeAudioDriver">
        /// AudioDriverBehaviour がシーンに配置されている場合のみ true。false なら
        /// AudioDriverHostTickAdapter を登録しない (依存解決失敗を避ける)。
        /// </param>
        public EntryPointsInstaller(bool includeAudioDriver)
        {
            _includeAudioDriver = includeAudioDriver;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<MainThreadCommandTicker>();
            if (_includeAudioDriver)
                builder.RegisterEntryPoint<AudioDriverHostTickAdapter>();
            builder.RegisterEntryPoint<HealthAggregatorTickAdapter>();
        }
    }
}
