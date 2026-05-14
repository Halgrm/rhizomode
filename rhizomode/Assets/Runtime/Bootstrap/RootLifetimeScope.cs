#nullable enable

using Rhizomode.Audio.GraphAdapter;
using Rhizomode.Bootstrap.Installers;
using Rhizomode.Graph.Model;
using Rhizomode.Modules;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// アプリ唯一の VContainer composition root。Plan v5.4 §15 — Bootstrap だけが VContainer を参照する。
    /// </summary>
    /// <remarks>
    /// V2b transitional shape: GameBootstrap が scene MonoBehaviour 由来の値 (GraphState /
    /// ModuleDefinition[] / Object3DPrefabList / AudioDriverBehaviour) を <see cref="SetHosts"/> で
    /// 渡す。scope GameObject は「非アクティブ生成 → AddComponent → SetHosts → アクティブ化」の順で
    /// 構築されるため、Awake (= VContainer Build) 時点で値は必ず揃う。
    ///
    /// <see cref="Configure"/> は per-bounded-context の Installer を順に Install する:
    /// <list type="bullet">
    ///   <item><see cref="GraphInstaller"/> — NodeFactory / GraphEventBus / IntentTranslator / CommandQueue</item>
    ///   <item><see cref="CatalogInstaller"/> — NodeTypeRegistry + NodeRegistrationOrchestrator</item>
    ///   <item><see cref="PersistenceInstaller"/> — GraphRepository / GraphHydrator / SavePathProvider</item>
    ///   <item><see cref="ObservabilityInstaller"/> — HealthAggregator</item>
    ///   <item><see cref="EntryPointsInstaller"/> — ITickable adapter 群</item>
    /// </list>
    /// GameBootstrap は Build 後に container から pure-C# サービスを resolve する (new を置換)。
    ///
    /// V2b 時点で NodeRuntime / GraphSaveLoad の Configure / HydrationPlanExecutor は引き続き
    /// GameBootstrap が構築・wiring する (scene-ref 依存のため)。それらの Installer 化と
    /// GameBootstrap 解体は V3/V-final。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RootLifetimeScope : LifetimeScope
    {
        private AudioDriverBehaviour? _audioDriver;
        private GraphState? _graphState;
        private ModuleDefinition[]? _moduleDefinitions;
        private Object3DPrefabList? _object3DPrefabs;

        /// <summary>
        /// VContainer Build 前 (= GameObject をアクティブ化する前) に呼ぶこと。
        /// 呼び出し元は同 asmdef の <see cref="EntryPointBootstrapper"/> のみ。
        /// </summary>
        internal void SetHosts(
            AudioDriverBehaviour? audioDriver,
            GraphState graphState,
            ModuleDefinition[]? moduleDefinitions,
            Object3DPrefabList? object3DPrefabs)
        {
            _audioDriver = audioDriver;
            _graphState = graphState;
            _moduleDefinitions = moduleDefinitions;
            _object3DPrefabs = object3DPrefabs;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // 防御: SetHosts が Build 前に呼ばれていれば _graphState は非 null
            // (EntryPointBootstrapper.Launch の構築プロトコルが保証する)。
            if (_graphState == null)
            {
                Debug.LogWarning(
                    "[RootLifetimeScope] Configure skipped — SetHosts が Build 前に呼ばれていない。");
                return;
            }

            if (_audioDriver != null)
                builder.RegisterInstance(_audioDriver);

            new GraphInstaller(_graphState).Install(builder);
            new CatalogInstaller(_graphState, _moduleDefinitions, _object3DPrefabs).Install(builder);
            new PersistenceInstaller().Install(builder);
            new ObservabilityInstaller().Install(builder);
            new EntryPointsInstaller(includeAudioDriver: _audioDriver != null).Install(builder);
        }
    }
}
