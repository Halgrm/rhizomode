#nullable enable

using Rhizomode.Audio.GraphAdapter;
using Rhizomode.Bootstrap.Installers;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Mutation;
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
    /// V2a transitional shape: GameBootstrap が scene MonoBehaviour 由来の値 (GraphState /
    /// ModuleDefinition[] / Object3DPrefabList / MainThreadGraphCommandQueue / AudioDriverBehaviour) を
    /// <see cref="SetHosts"/> で渡す。scope GameObject は「非アクティブ生成 → AddComponent →
    /// SetHosts → アクティブ化」の順で構築されるため、Awake (= VContainer Build) 時点で値は必ず揃う。
    ///
    /// <see cref="Configure"/> は per-bounded-context の Installer を順に Install する:
    /// <list type="bullet">
    ///   <item><see cref="CatalogInstaller"/> — NodeTypeRegistry + NodeRegistrationOrchestrator</item>
    ///   <item><see cref="ObservabilityInstaller"/> — HealthAggregator</item>
    ///   <item><see cref="EntryPointsInstaller"/> — ITickable adapter 群</item>
    /// </list>
    /// GameBootstrap は Build 後に container から pure-C# サービスを resolve する (new を置換)。
    ///
    /// V2a 時点で GraphAdapterWiring / NodeRuntime / GraphSaveLoad 系は引き続き GameBootstrap が
    /// new する。MainThreadGraphCommandQueue は GraphAdapterWiring 産だが、EntryPointsInstaller の
    /// 依存のため RegisterInstance で container に渡す。GraphInstaller / PersistenceInstaller への
    /// 移送は V2b、scene-ref 依存 Installer + GameBootstrap 解体は V3/V-final。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RootLifetimeScope : LifetimeScope
    {
        private MainThreadGraphCommandQueue? _commandQueue;
        private AudioDriverBehaviour? _audioDriver;
        private GraphState? _graphState;
        private ModuleDefinition[]? _moduleDefinitions;
        private Object3DPrefabList? _object3DPrefabs;

        /// <summary>
        /// VContainer Build 前 (= GameObject をアクティブ化する前) に呼ぶこと。
        /// 呼び出し元は同 asmdef の <see cref="EntryPointBootstrapper"/> のみ。
        /// </summary>
        internal void SetHosts(
            MainThreadGraphCommandQueue commandQueue,
            AudioDriverBehaviour? audioDriver,
            GraphState? graphState,
            ModuleDefinition[]? moduleDefinitions,
            Object3DPrefabList? object3DPrefabs)
        {
            _commandQueue = commandQueue;
            _audioDriver = audioDriver;
            _graphState = graphState;
            _moduleDefinitions = moduleDefinitions;
            _object3DPrefabs = object3DPrefabs;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (_commandQueue == null)
            {
                Debug.LogWarning(
                    "[RootLifetimeScope] Configure skipped — SetHosts が Build 前に呼ばれていない。");
                return;
            }

            builder.RegisterInstance(_commandQueue);
            if (_audioDriver != null)
                builder.RegisterInstance(_audioDriver);

            new CatalogInstaller(_graphState, _moduleDefinitions, _object3DPrefabs).Install(builder);
            new ObservabilityInstaller().Install(builder);
            new EntryPointsInstaller(includeAudioDriver: _audioDriver != null).Install(builder);
        }
    }
}
