#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.Modules;
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
    /// <paramref name="graphState"/> が null の場合 (graphContext 未配置の degraded 起動) は
    /// 空の <see cref="NodeTypeRegistry"/> のみ登録し、orchestrator 登録はスキップする。
    /// </remarks>
    internal sealed class CatalogInstaller : IInstaller
    {
        private readonly GraphState? _graphState;
        private readonly ModuleDefinition[]? _moduleDefinitions;
        private readonly Object3DPrefabList? _object3DPrefabs;

        public CatalogInstaller(
            GraphState? graphState,
            ModuleDefinition[]? moduleDefinitions,
            Object3DPrefabList? object3DPrefabs)
        {
            _graphState = graphState;
            _moduleDefinitions = moduleDefinitions;
            _object3DPrefabs = object3DPrefabs;
        }

        public void Install(IContainerBuilder builder)
        {
            var registry = new NodeTypeRegistry();
            builder.RegisterInstance(registry);

            if (_graphState == null)
            {
                Debug.LogWarning(
                    "[CatalogInstaller] GraphState null — ノード type/factory 登録をスキップ (degraded 起動)。");
                return;
            }

            // ctor は純粋 (フィールド代入のみ)。RegisterAll() の副作用は Build 後に EntryPointBootstrapper が駆動。
            var orchestrator = new NodeRegistrationOrchestrator(
                registry, _graphState, _moduleDefinitions, _object3DPrefabs);
            builder.RegisterInstance(orchestrator);
        }
    }
}
