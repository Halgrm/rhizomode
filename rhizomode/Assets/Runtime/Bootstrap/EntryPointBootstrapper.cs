#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Bootstrap.Wiring;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Observability.Contracts;
using Rhizomode.Observability.Runtime;
using UnityEngine;
using VContainer;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// VContainer の <see cref="RootLifetimeScope"/> を起動し、全 wiring を Bootstrap 内で eager 駆動する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §19 hard rule: VContainer / VContainer.Unity を参照してよいのは Bootstrap asmdef
    /// のみ。GameBootstrap (XR asmdef) は VContainer 型に一切触れず、本 factory に
    /// <see cref="XrSceneReferences"/> を渡し、戻り値の <see cref="CompositionRoot"/> Dispose ハンドルだけを
    /// 受け取る。Vf-c で GameBootstrap が削除され RootLifetimeScope が直接シーン配置になったら、
    /// 本 factory も廃止される (一時的 transitional shim)。
    ///
    /// Vf-b: BootstrapModulePlacement / BootstrapObject3DRegistry の closure 依存を廃止し、両者を
    /// ModulesInstaller が <see cref="Lifetime.Singleton"/> 登録するようになったため、本 Launch 内の
    /// local closure は不要に。activeInput は <see cref="InteractionBootstrapWiring.Wire"/> 完了後に
    /// container 経由で resolve した <see cref="BootstrapModulePlacement"/> へ <c>SetActiveInput</c>。
    /// </remarks>
    public static class EntryPointBootstrapper
    {
        /// <summary>
        /// <paramref name="parent"/> の子として RootLifetimeScope を生成・起動し、全 wiring を eager 駆動する。
        /// </summary>
        /// <returns>scope GameObject の Dispose handle (GameBootstrap.OnDestroy で呼ぶ)。</returns>
        public static CompositionRoot Launch(Transform parent, XrSceneReferences sceneRefs)
        {
            var graphContext = sceneRefs.GraphContext;
            if (graphContext == null)
                throw new InvalidOperationException(
                    "[EntryPointBootstrapper] graphContext is required on XrSceneReferences.");

            // 子 GameObject を「非アクティブ生成 → AddComponent → SetHosts → アクティブ化」の順で構築。
            // SetActive(true) で VContainer Build が同期実行され、その後 container から resolve できる。
            var scopeGo = new GameObject("RootLifetimeScope");
            scopeGo.transform.SetParent(parent, false);
            scopeGo.SetActive(false);
            var rootScope = scopeGo.AddComponent<RootLifetimeScope>();
            rootScope.SetHosts(sceneRefs, graphContext.Context);
            scopeGo.SetActive(true);

            var container = rootScope.Container;

            // === Phase 1: GraphState 初期化系の eager step ===
            //
            // NodeRegistrationOrchestrator.RegisterAll が GraphState への factory 登録 + Object3D prefab
            // 逆引きマップを populate する (Install 内副作用を避けるため、Build 後に明示駆動)。
            container.Resolve<NodeRegistrationOrchestrator>().RegisterAll();

            // 各 transport Installer (Audio/OscMidi/Ableton) が IHealthMonitor として登録した
            // monitor を HealthAggregator へ集約する。
            var healthAggregator = container.Resolve<HealthAggregator>();
            foreach (var monitor in container.Resolve<IReadOnlyList<IHealthMonitor>>())
                healthAggregator.Register(monitor);

            // === Phase 2: scene-ref 依存の Initialize ===
            //
            // V-final (Vf-a): 旧 GameBootstrap.InitializeSystems の visualManager / audioDriver Initialize を移送。
            var typeRegistry = container.Resolve<NodeTypeRegistry>();
            if (sceneRefs.VisualManager != null)
                sceneRefs.VisualManager.Initialize(typeRegistry);
            if (sceneRefs.AudioDriver != null)
                sceneRefs.AudioDriver.Initialize(graphContext);

            // === Phase 3: AudioDeviceSelector wiring (依存なし) ===
            container.Resolve<AudioDeviceSelectorWiring>().Wire();

            // === Phase 4: Interaction wiring (activeInput 確定) ===
            //
            // MenuSpawnBootstrapWiring.HandleSelection を ScrollMenu.OnNodeTypeSelected callback として渡す。
            // InteractionBootstrapWiring.Wire 完了後に activeInput が確定する。
            var menuSpawnWiring = container.Resolve<MenuSpawnBootstrapWiring>();
            var interactionWiring = container.Resolve<InteractionBootstrapWiring>();
            interactionWiring.Wire(graphContext, menuSpawnWiring.HandleSelection);
            var activeInput = interactionWiring.ActiveInput;
            menuSpawnWiring.SetActiveInput(activeInput);

            // Vf-b: BootstrapModulePlacement に activeInput を後付け注入 (closure 依存解消)。
            // 以降の FreshSpawn 配置 (ModuleLifecycleProcessor 経由) が head pose を参照できる。
            container.Resolve<BootstrapModulePlacement>().SetActiveInput(activeInput);

            // === Phase 5: activeInput 依存の wiring 群 ===
            //
            // GraphSaveLoad: Initialize + Configure + OnGraphLoading/OnGraphLoaded 購読
            // SceneObjects: SceneObjectBridge スキャン + visual 生成
            // Ableton: Setup panel 表示 + macro listener
            // VerticalSlice: StatusPanel / CameraManagerPanel / MirrorOutput / health 購読
            container.Resolve<GraphSaveLoadBootstrapWiring>().Wire(activeInput);
            container.Resolve<SceneObjectsBootstrapWiring>().Wire(activeInput);
            container.Resolve<AbletonBootstrapWiring>().Wire(activeInput, sceneRefs.SharedRaycastService);
            container.Resolve<VerticalSliceBootstrapWiring>().Wire(graphContext);

            return new CompositionRoot(scopeGo);
        }
    }
}
