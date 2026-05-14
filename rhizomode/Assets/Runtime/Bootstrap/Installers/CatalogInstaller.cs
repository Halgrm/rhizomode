#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.Modules;
using Rhizomode.NodeCatalog.Runtime;
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
    /// graphState は非 null 必須 — composition root は有効な graph を前提とし、degraded 起動の
    /// 判定は GameBootstrap が行う (V2b で null 許容を撤廃)。
    /// </remarks>
    internal sealed class CatalogInstaller : IInstaller
    {
        private readonly GraphState _graphState;
        private readonly ModuleDefinition[]? _moduleDefinitions;
        private readonly Object3DPrefabList? _object3DPrefabs;

        public CatalogInstaller(
            GraphState graphState,
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

            // ctor は純粋 (フィールド代入のみ)。RegisterAll() の副作用は Build 後に EntryPointBootstrapper が駆動。
            var orchestrator = new NodeRegistrationOrchestrator(
                registry, _graphState, _moduleDefinitions, _object3DPrefabs);
            builder.RegisterInstance(orchestrator);
        }
    }
}
