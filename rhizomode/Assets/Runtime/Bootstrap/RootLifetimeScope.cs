#nullable enable

using Rhizomode.Bootstrap.Installers;
using Rhizomode.Graph.Model;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// アプリ唯一の VContainer composition root。Plan v5.4 §15 — Bootstrap だけが VContainer を参照する。
    /// </summary>
    /// <remarks>
    /// V-final (Vf-b) transitional shape: GameBootstrap (XR asmdef の薄い shim) が
    /// <see cref="XrSceneReferences"/> + <see cref="GraphState"/> を <see cref="SetHosts"/> 経由で渡す。
    /// scope GameObject は「非アクティブ生成 → AddComponent → SetHosts → アクティブ化」の順で構築されるため、
    /// Awake (= VContainer Build) 時点で値は必ず揃う。
    ///
    /// Vf-b で <c>IModulePlacementService</c> / <c>IObject3DProxyRegistry</c> の closure 依存を解消したため、
    /// 旧 SetHosts の 4 引数 (sceneRefs / graphState / modulePlacement / object3DRegistry) が 2 引数に縮小。
    /// BootstrapModulePlacement / BootstrapObject3DRegistry は ModulesInstaller が
    /// <see cref="Lifetime.Singleton"/> で構築する。
    ///
    /// <see cref="Configure"/> は per-bounded-context の Installer を順に Install する。Installer 間に
    /// 構築順依存はない (各 Installer は登録のみ、依存解決は Build 後)。
    /// <see cref="EntryPointBootstrapper.Launch"/> が Build 後に全 wiring を eager 駆動する。
    ///
    /// Vf-c で GameBootstrap.cs が削除されたら RootLifetimeScope をシーン直接配置にし、
    /// EntryPointBootstrapper 自体も廃止する。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RootLifetimeScope : LifetimeScope
    {
        private XrSceneReferences? _sceneRefs;
        private GraphState? _graphState;

        /// <summary>
        /// VContainer Build 前 (= GameObject をアクティブ化する前) に呼ぶこと。
        /// 呼び出し元は同 asmdef の <see cref="EntryPointBootstrapper"/> のみ。
        /// </summary>
        internal void SetHosts(XrSceneReferences sceneRefs, GraphState graphState)
        {
            _sceneRefs = sceneRefs;
            _graphState = graphState;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // 防御: SetHosts が Build 前に呼ばれていれば全フィールドは非 null
            // (EntryPointBootstrapper.Launch の構築プロトコルが保証する)。
            if (_graphState == null || _sceneRefs == null)
            {
                Debug.LogWarning(
                    "[RootLifetimeScope] Configure skipped — SetHosts が Build 前に呼ばれていない。");
                return;
            }

            // XrSceneReferences は wiring クラスの ctor injection に使うため container に登録する。
            builder.RegisterInstance(_sceneRefs);

            // V-final (Vf-a): Object3DProxyBindService が GraphContextBehaviour を ctor 注入で要するため
            // container に登録する (sceneRefs.GraphContext から取得)。
            var graphContext = _sceneRefs.GraphContext;
            if (graphContext != null)
                builder.RegisterInstance(graphContext);

            // V-final (Vf-a): MenuNodeSpawnCoordinator / GraphLoadCoordinator が NodeVisualManager /
            // EdgeVisualManager を ctor 注入で要するため container に登録する。
            if (_sceneRefs.VisualManager != null)
                builder.RegisterInstance(_sceneRefs.VisualManager);
            if (_sceneRefs.EdgeVisualManager != null)
                builder.RegisterInstance(_sceneRefs.EdgeVisualManager);

            new GraphInstaller(_graphState).Install(builder);
            new CatalogInstaller(_sceneRefs, _graphState).Install(builder);
            new PersistenceInstaller().Install(builder);
            new ObservabilityInstaller().Install(builder);
            new AudioInstaller(_sceneRefs).Install(builder);
            new SceneInstaller(_sceneRefs).Install(builder);
            new OscMidiInstaller(_sceneRefs).Install(builder);
            new AbletonInstaller(_sceneRefs).Install(builder);
            new ModulesInstaller().Install(builder);
            new NodesInstaller().Install(builder);
            new InputInstaller(_sceneRefs).Install(builder);
            new InteractionGraphAdapterInstaller().Install(builder);
            new InteractionInstaller().Install(builder);
            new UIInstaller().Install(builder);
            new UIGraphAdapterInstaller().Install(builder);
            new XRInstaller().Install(builder);
            new EntryPointsInstaller(includeAudioDriver: _sceneRefs.AudioDriver != null).Install(builder);
        }
    }
}
