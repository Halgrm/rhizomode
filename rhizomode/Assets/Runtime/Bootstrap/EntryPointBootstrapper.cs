#nullable enable

using System.Collections.Generic;
using Rhizomode.Bootstrap.Wiring;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Graph.Serialization;
using Rhizomode.Interaction.GraphAdapter;
using Rhizomode.Modules;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Observability.Contracts;
using Rhizomode.Observability.Runtime;
using Rhizomode.Persistence.Contracts;
using UnityEngine;
using VContainer;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// VContainer の <see cref="RootLifetimeScope"/> を起動し、pure-C# サービスを resolve する factory。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §19 hard rule: VContainer / VContainer.Unity を参照してよいのは Bootstrap asmdef
    /// のみ。GameBootstrap (XR asmdef) は VContainer 型に一切触れず、本 factory に scene 由来の値を
    /// 渡し、戻り値の <see cref="CompositionRoot"/> から型付きでサービスを受け取るだけ。
    ///
    /// V3 transitional shape: GameBootstrap が <see cref="XrSceneReferences"/> と、XR asmdef の
    /// closure を要する 2 サービス (IModulePlacementService / IObject3DProxyRegistry) を集めて
    /// <see cref="Launch"/> を呼ぶ。子 GameObject を「非アクティブ生成 → AddComponent → SetHosts →
    /// アクティブ化」の順で構築するため、<c>SetActive(true)</c> 直後 (= VContainer Build 完了後) に
    /// container から resolve できる。V-final で GameBootstrap を解体したら、本 factory も
    /// RootLifetimeScope のシーン直接配置に置き換える。
    /// </remarks>
    public static class EntryPointBootstrapper
    {
        /// <summary>
        /// <paramref name="parent"/> の子として RootLifetimeScope を生成・起動し、Installer が構築した
        /// pure-C# サービスを <see cref="CompositionRoot"/> に束ねて返す。
        /// </summary>
        /// <remarks>
        /// <c>SetActive(true)</c> で VContainer Build が同期実行された後、container から pure-C#
        /// サービスを resolve する。<see cref="NodeRegistrationOrchestrator.RegisterAll"/> は
        /// GraphState を mutate する副作用を持つため Installer.Install 内ではなく、Build 完了後の
        /// 本メソッドで明示的に駆動する (= composition root の eager initialization step)。
        /// RegisterAll が Object3D prefab 逆引きマップを populate した後で NodeRuntime /
        /// ModuleLifecycleProcessor を resolve する (resolve 時点で dict 参照は populate 済)。
        ///
        /// <paramref name="graphState"/> は非 null 必須 — composition root は有効な graph を前提と
        /// する。degraded 起動 (graphContext 未配置) の判定は呼び出し元 (GameBootstrap) が行い、
        /// その場合は本メソッドを呼ばない。
        /// </remarks>
        public static CompositionRoot Launch(
            Transform parent,
            XrSceneReferences sceneRefs,
            GraphState graphState,
            IModulePlacementService modulePlacement,
            IObject3DProxyRegistry object3DRegistry)
        {
            var scopeGo = new GameObject("RootLifetimeScope");
            scopeGo.transform.SetParent(parent, false);
            scopeGo.SetActive(false);
            var rootScope = scopeGo.AddComponent<RootLifetimeScope>();
            rootScope.SetHosts(sceneRefs, graphState, modulePlacement, object3DRegistry);
            scopeGo.SetActive(true);

            var container = rootScope.Container;

            // Build 後の明示的な eager registration step (GraphState への factory 登録 +
            // Object3D prefab 逆引きマップの populate)。
            var orchestrator = container.Resolve<NodeRegistrationOrchestrator>();
            orchestrator.RegisterAll();

            // V3a: 各 transport Installer (Audio/OscMidi/Ableton) が IHealthMonitor として登録した
            // monitor を HealthAggregator へ集約する。OscMidiInstaller が 2 件を無条件登録するため常に 1 件以上。
            var healthAggregator = container.Resolve<HealthAggregator>();
            foreach (var monitor in container.Resolve<IReadOnlyList<IHealthMonitor>>())
                healthAggregator.Register(monitor);

            // V3a: AudioDeviceSelector wiring は scene-runtime 値 (入力ルーター等) に依存しないため
            // Build 後即時に駆動する。Ableton wiring は入力ルーター / SharedRaycastService を要する
            // ため即時駆動できず、CompositionRoot 経由で GameBootstrap が後段で駆動する。
            container.Resolve<AudioDeviceSelectorWiring>().Wire();
            var abletonWiring = container.Resolve<AbletonBootstrapWiring>();

            return new CompositionRoot(
                scopeGo,
                container.Resolve<NodeTypeRegistry>(),
                healthAggregator,
                container.Resolve<INodeFactory>(),
                container.Resolve<SpatialIntentToCommandTranslator>(),
                container.Resolve<IGraphRepository>(),
                container.Resolve<GraphHydrator>(),
                container.Resolve<ISavePathProvider>(),
                abletonWiring,
                container.Resolve<NodeRuntime>(),
                container.Resolve<ModuleLifecycleProcessor>());
        }
    }
}
