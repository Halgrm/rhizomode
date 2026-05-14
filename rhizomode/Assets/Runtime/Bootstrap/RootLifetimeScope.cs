#nullable enable

using Rhizomode.Audio.GraphAdapter;
using Rhizomode.Bootstrap.Installers;
using Rhizomode.Graph.Mutation;
using Rhizomode.Observability.Runtime;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// アプリ唯一の VContainer composition root。Plan v5.4 §15 — Bootstrap だけが VContainer を参照する。
    /// </summary>
    /// <remarks>
    /// V1 transitional shape: GameBootstrap が pure-C# host (MainThreadGraphCommandQueue /
    /// AudioDriverHost / HealthAggregator) を構築し、<see cref="EntryPointBootstrapper"/> 経由で
    /// 本 scope に <see cref="SetHosts"/> で渡す。scope GameObject は「非アクティブ生成 →
    /// AddComponent → SetHosts → アクティブ化」の順で構築されるため、Awake (= VContainer Build)
    /// 時点で host は必ず揃っている。実行順序への依存はない。
    ///
    /// host の所有権は引き続き GameBootstrap 側にある (RegisterInstance された外部インスタンスは
    /// VContainer が Dispose しない)。本 scope は ITickable adapter の生成と駆動のみを担う。
    ///
    /// V2 以降で per-bounded-context の Installer を追加し、最終的に GameBootstrap を解体して
    /// 本 scope をシーン配置の唯一の composition root にする。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RootLifetimeScope : LifetimeScope
    {
        private MainThreadGraphCommandQueue? _commandQueue;
        private AudioDriverBehaviour? _audioDriver;
        private HealthAggregator? _healthAggregator;

        /// <summary>
        /// VContainer Build 前 (= GameObject をアクティブ化する前) に呼ぶこと。
        /// </summary>
        public void SetHosts(
            MainThreadGraphCommandQueue commandQueue,
            AudioDriverBehaviour? audioDriver,
            HealthAggregator healthAggregator)
        {
            _commandQueue = commandQueue;
            _audioDriver = audioDriver;
            _healthAggregator = healthAggregator;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (_commandQueue == null || _healthAggregator == null)
            {
                Debug.LogWarning(
                    "[RootLifetimeScope] Configure skipped — SetHosts が Build 前に呼ばれていない。");
                return;
            }

            builder.RegisterInstance(_commandQueue);
            builder.RegisterInstance(_healthAggregator);
            if (_audioDriver != null)
                builder.RegisterInstance(_audioDriver);

            new EntryPointsInstaller(includeAudioDriver: _audioDriver != null).Install(builder);
        }
    }
}
