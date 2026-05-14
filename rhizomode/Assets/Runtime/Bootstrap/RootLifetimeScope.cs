#nullable enable

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
    /// V3 transitional shape: GameBootstrap が scene 由来の値 (<see cref="XrSceneReferences"/> /
    /// GraphState / IModulePlacementService / IObject3DProxyRegistry) を <see cref="SetHosts"/> で
    /// 渡す。scope GameObject は「非アクティブ生成 → AddComponent → SetHosts → アクティブ化」の順で
    /// 構築されるため、Awake (= VContainer Build) 時点で値は必ず揃う。
    ///
    /// <see cref="Configure"/> は per-bounded-context の Installer を順に Install する。Installer 間に
    /// 構築順依存はない (各 Installer は登録のみ、依存解決は Build 後)。GameBootstrap は Build 後に
    /// container から pure-C# サービスを resolve する。
    ///
    /// V3 時点で GraphSaveLoad の Configure / HydrationPlanExecutor は引き続き GameBootstrap が
    /// wiring する (scene MonoBehaviour 操作のため)。GameBootstrap の完全解体は V-final。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RootLifetimeScope : LifetimeScope
    {
        private XrSceneReferences? _sceneRefs;
        private GraphState? _graphState;
        private IModulePlacementService? _modulePlacement;
        private IObject3DProxyRegistry? _object3DRegistry;

        /// <summary>
        /// VContainer Build 前 (= GameObject をアクティブ化する前) に呼ぶこと。
        /// 呼び出し元は同 asmdef の <see cref="EntryPointBootstrapper"/> のみ。
        /// </summary>
        internal void SetHosts(
            XrSceneReferences sceneRefs,
            GraphState graphState,
            IModulePlacementService modulePlacement,
            IObject3DProxyRegistry object3DRegistry)
        {
            _sceneRefs = sceneRefs;
            _graphState = graphState;
            _modulePlacement = modulePlacement;
            _object3DRegistry = object3DRegistry;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // 防御: SetHosts が Build 前に呼ばれていれば全フィールドは非 null
            // (EntryPointBootstrapper.Launch の構築プロトコルが保証する)。
            if (_graphState == null || _sceneRefs == null ||
                _modulePlacement == null || _object3DRegistry == null)
            {
                Debug.LogWarning(
                    "[RootLifetimeScope] Configure skipped — SetHosts が Build 前に呼ばれていない。");
                return;
            }

            // XrSceneReferences は wiring クラスの ctor injection に使うため container に登録する。
            builder.RegisterInstance(_sceneRefs);

            new GraphInstaller(_graphState).Install(builder);
            new CatalogInstaller(_sceneRefs, _graphState).Install(builder);
            new PersistenceInstaller().Install(builder);
            new ObservabilityInstaller().Install(builder);
            new AudioInstaller(_sceneRefs).Install(builder);
            new SceneInstaller(_sceneRefs).Install(builder);
            new OscMidiInstaller(_sceneRefs).Install(builder);
            new AbletonInstaller(_sceneRefs).Install(builder);
            new ModulesInstaller(_modulePlacement, _object3DRegistry).Install(builder);
            new NodesInstaller().Install(builder);
            new EntryPointsInstaller(includeAudioDriver: _sceneRefs.AudioDriver != null).Install(builder);
        }
    }
}
