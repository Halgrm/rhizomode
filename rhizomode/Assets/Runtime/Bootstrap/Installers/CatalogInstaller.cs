#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Runtime;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — NodeCatalog の pure-C# サービスを構築・登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>CatalogInstaller</c>。V2a で GameBootstrap.Awake が直接 new していた
    /// <see cref="NodeTypeRegistry"/> + <see cref="NodeRegistrationOrchestrator"/> の構築をここへ移送。
    ///
    /// <see cref="Install"/> は登録のみを行う pure な処理 — orchestrator の ctor は副作用を持たない。
    /// <see cref="NodeRegistrationOrchestrator.RegisterAll"/> は GraphState を mutate する副作用を
    /// 伴うため Install 内では呼ばず、Build 後に <c>EntryPointBootstrapper</c> が明示的に駆動する。
    ///
    /// V3b: ModuleDefinition[] / Object3DPrefabList を <see cref="XrSceneReferences"/> から取得する形に変更。
    /// あわせて orchestrator が populate する Object3D prefab 逆引きマップを
    /// <see cref="IReadOnlyDictionary{TKey,TValue}"/> として登録し、ModulesInstaller の
    /// <c>ModuleLifecycleProcessor</c> が ctor 注入で受け取れるようにする。
    /// graphState は非 null 必須 — composition root は有効な graph を前提とし、degraded 起動の
    /// 判定は GameBootstrap が行う。
    /// </remarks>
    internal sealed class CatalogInstaller : IInstaller
    {
        private readonly XrSceneReferences _sceneRefs;
        private readonly GraphState _graphState;

        public CatalogInstaller(XrSceneReferences sceneRefs, GraphState graphState)
        {
            _sceneRefs = sceneRefs;
            _graphState = graphState;
        }

        public void Install(IContainerBuilder builder)
        {
            var registry = new NodeTypeRegistry();
            builder.RegisterInstance(registry);

            // ctor は純粋 (フィールド代入のみ)。RegisterAll() の副作用は Build 後に EntryPointBootstrapper が駆動。
            var orchestrator = new NodeRegistrationOrchestrator(
                registry, _graphState, _sceneRefs.ModuleDefinitions, _sceneRefs.Object3DPrefabs);
            builder.RegisterInstance(orchestrator);

            // V3b: Object3D prefab 逆引きマップ (RegisterAll で populate される dict 参照) を登録。
            // ModulesInstaller の ModuleLifecycleProcessor が IReadOnlyDictionary として ctor 注入で受け取る。
            builder.RegisterInstance<IReadOnlyDictionary<string, GameObject>>(orchestrator.Object3DPrefabMap);
        }
    }
}
