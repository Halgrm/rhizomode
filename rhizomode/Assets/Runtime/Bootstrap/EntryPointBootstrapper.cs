#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Bootstrap.Wiring;
using Rhizomode.Input.Contracts;
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
    /// Vf-a 時点で BootstrapModulePlacement / BootstrapObject3DRegistry の closure は本 factory 内に閉じる:
    /// modulePlacement は IControllerInput? を local closure で遅延解決し、InteractionBootstrapWiring.Wire
    /// 完了後に activeInput が確定する。Object3D registry は XrSceneReferences.Object3DGrabHandler を
    /// 経由する。Vf-b で BootstrapModulePlacement 自体を IControllerInputAccessor 経由に refactor して
    /// closure 依存を解消する。
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

            // InteractionBootstrapWiring.Wire 完了後に activeInput が確定する。BootstrapModulePlacement は
            // local closure で遅延解決する (Vf-b で IControllerInputAccessor 経由に refactor 予定)。
            IControllerInput? activeInput = null;
            var modulePlacement = new BootstrapModulePlacement(() => activeInput);
            var object3DRegistry = new BootstrapObject3DRegistry(
                proxy => sceneRefs.Object3DGrabHandler?.Register(proxy),
                proxy => sceneRefs.Object3DGrabHandler?.Unregister(proxy));

            // 子 GameObject を「非アクティブ生成 → AddComponent → SetHosts → アクティブ化」の順で構築。
            // SetActive(true) で VContainer Build が同期実行され、その後 container から resolve できる。
            var scopeGo = new GameObject("RootLifetimeScope");
            scopeGo.transform.SetParent(parent, false);
            scopeGo.SetActive(false);
            var rootScope = scopeGo.AddComponent<RootLifetimeScope>();
            rootScope.SetHosts(sceneRefs, graphContext.Context, modulePlacement, object3DRegistry);
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
            activeInput = interactionWiring.ActiveInput;
            menuSpawnWiring.SetActiveInput(activeInput);

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
