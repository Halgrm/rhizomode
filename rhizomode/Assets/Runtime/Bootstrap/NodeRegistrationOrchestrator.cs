#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Rhizomode.Graph.Model;
using Rhizomode.Modules;
using Rhizomode.Nodes.Scene;
using Rhizomode.Nodes.Modules;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// 起動時のノード type / factory 登録を集約する orchestrator。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 F-8.2 抽出 3/N (Codex Round C advisory への対応): GameBootstrap.NodeFactoryMap +
    /// RegisterNodeTypes + RegisterFactories + RegisterModuleTypes + RegisterObject3DTypes を集約。
    /// 起動時に 1 度だけ <see cref="RegisterAll"/> を呼び、Object3D prefab map を
    /// <c>ModuleLifecycleProcessor</c> に渡す。
    ///
    /// 配置: Rhizomode.Bootstrap asmdef (Plan v5.4 §15)。V2a で XR asmdef から移送 (internal 化)。
    /// <c>CatalogInstaller</c> が VContainer Configure 時に構築し、Build 後に
    /// <c>EntryPointBootstrapper</c> が <see cref="RegisterAll"/> を明示的に駆動する。
    ///
    /// <para>
    /// M4 (data-driven): module 種別の category / legacy alias / 専用 ModuleNode 派生型は
    /// <see cref="PerformanceModuleAttribute"/> から reflection で読み取る。本クラスは concrete
    /// type に依存せず、新 module 追加で本ファイルを触る必要はない。
    /// </para>
    /// <para>
    /// M3 (canonical typeName): legacy alias で load されても、ノードの <c>NodeType</c> は新 typeName
    /// (<c>Module_X</c>) で生成される。再保存しても旧 alias は永続化されない。
    /// </para>
    /// </remarks>
    internal sealed class NodeRegistrationOrchestrator
    {
        /// <summary>
        /// 動的 ctor 引数を持つノードの手動 factory 辞書。Scanner では [NodeType] と 1:1 で対応しない
        /// (1 クラス→複数 typeName) ノードを別管理する。
        /// </summary>
        /// <remarks>
        /// N2 fix (2026-05-16): 旧 StaticFactoryMap (35+ entries) を <see cref="NodeTypeAttributeScanner"/>
        /// 出力に一本化。新ノード追加時に本 orchestrator を触らずに済むようになった。
        /// 残置: SceneTriggerNode は 1 クラスを 3 typeName (Dark/White/Nature) に展開するため、
        /// 別 ctor 引数が必要 → 個別 factory のままにする。
        /// </remarks>
        private static readonly Dictionary<string, Func<string, NodeBase>> DynamicCtorFactoryMap = new()
        {
            ["SceneDark"] = id => new SceneTriggerNode(id, "SceneDark", 0),
            ["SceneWhite"] = id => new SceneTriggerNode(id, "SceneWhite", 1),
            ["SceneNature"] = id => new SceneTriggerNode(id, "SceneNature", 2),
        };

        private readonly NodeTypeRegistry _typeRegistry;
        private readonly GraphState _graphState;
        private readonly ModuleDefinition[]? _moduleDefinitions;
        private readonly Object3DPrefabList? _object3DPrefabs;

        /// <summary>登録した Object3D prefab の逆引きマップ (instantiate 用)。</summary>
        public Dictionary<string, GameObject> Object3DPrefabMap { get; } = new();

        public NodeRegistrationOrchestrator(
            NodeTypeRegistry typeRegistry,
            GraphState graphState,
            ModuleDefinition[]? moduleDefinitions,
            Object3DPrefabList? object3DPrefabs)
        {
            _typeRegistry = typeRegistry;
            _graphState = graphState;
            _moduleDefinitions = moduleDefinitions;
            _object3DPrefabs = object3DPrefabs;
        }

        /// <summary>
        /// 全 type / factory を順番に登録する。
        /// </summary>
        public void RegisterAll()
        {
            RegisterStaticTypesAndFactoriesFromScanner();
            RegisterDynamicCtorTypes();
            RegisterModuleTypes();
            RegisterObject3DTypes();
        }

        /// <summary>
        /// [NodeType] 属性付きクラスを Scanner で発見し、type / factory の両方を一括登録する。
        /// </summary>
        private void RegisterStaticTypesAndFactoriesFromScanner()
        {
            var scanner = new NodeTypeAttributeScanner();
            foreach (var registration in scanner.Scan())
            {
                var d = registration.Display;
                _typeRegistry.Register(new NodeTypeInfo(d.TypeName, d.Label, d.Category));
                _graphState.RegisterNodeFactory(d.TypeName, registration.Factory);
            }
        }

        /// <summary>
        /// 動的 ctor 引数を持つノード (SceneTrigger 3 件) を type + factory ともに登録する。
        /// </summary>
        private void RegisterDynamicCtorTypes()
        {
            _typeRegistry.Register(new NodeTypeInfo("SceneDark", "Dark", NodeCategory.Scene));
            _typeRegistry.Register(new NodeTypeInfo("SceneWhite", "White", NodeCategory.Scene));
            _typeRegistry.Register(new NodeTypeInfo("SceneNature", "Nature", NodeCategory.Scene));

            foreach (var kvp in DynamicCtorFactoryMap)
            {
                _graphState.RegisterNodeFactory(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// ModuleDefinition 配列から module 用の動的 typeName とファクトリを登録。
        /// Prefab 注入は <see cref="ModuleLifecycleProcessor"/> が AfterSetup で実施するため、ここは factory のみ。
        /// </summary>
        /// <remarks>
        /// 完全 data-driven: <see cref="PerformanceModuleAttribute"/> から category / legacy alias prefix /
        /// 専用 ModuleNode 派生型を読む。属性未付与の実装は category=VFX + alias なし + 汎用 ModuleNode で
        /// degraded 登録される。
        ///
        /// Legacy alias: 旧 saved graph (<c>VFX_X</c> / <c>Shader_X</c> / <c>InstancedCubes_X</c>) の
        /// load 互換のため、prefix が指定されていれば旧 typeName でも同一ファクトリを二重登録する。
        /// ファクトリは canonical typeName (<c>Module_X</c>) を渡すため、ノードの <c>NodeType</c> は新典に統一される。
        /// 旧 alias は <see cref="NodeTypeRegistry"/> には登録しないため、ScrollMenu の新規 spawn 候補としては出ない
        /// (load-only)。
        /// </remarks>
        private void RegisterModuleTypes()
        {
            if (_moduleDefinitions == null) return;

            foreach (var def in _moduleDefinitions)
            {
                if (def == null) continue;
                if (def.prefab == null) continue;

                var module = FindPerformanceModule(def.prefab);
                if (module == null)
                {
                    Debug.LogWarning(
                        $"[NodeRegistrationOrchestrator] ModuleDefinition '{def.moduleName}' の prefab " +
                        $"'{def.prefab.name}' に IPerformanceModule 実装が見つからない。skip");
                    continue;
                }

                RegisterSingleModule(def, module);
            }
        }

        /// <summary>
        /// prefab GameObject 上の MonoBehaviour を走査し、最初に見つかった <see cref="IPerformanceModule"/>
        /// 実装を返す。
        /// </summary>
        /// <remarks>
        /// <c>GetComponent&lt;IPerformanceModule&gt;()</c> を直接呼ばないのは、Unity の interface 型 GetComponent が
        /// prefab asset (未 instantiate) に対して 一部バージョンで null を返すケースがあるため。
        /// <c>GetComponents&lt;MonoBehaviour&gt;()</c> + <c>is</c> filter は全 Unity 版で安定動作する。
        /// </remarks>
        private static IPerformanceModule? FindPerformanceModule(GameObject prefab)
        {
            var components = prefab.GetComponents<MonoBehaviour>();
            foreach (var c in components)
            {
                if (c is IPerformanceModule module) return module;
            }
            return null;
        }

        /// <summary>
        /// 1 つの ModuleDefinition に対し、prefab の <see cref="IPerformanceModule"/> 実装型から得た
        /// <see cref="PerformanceModuleAttribute"/> を読んで、新 typeName (<c>Module_X</c>) +
        /// 旧 alias を登録する。
        /// </summary>
        private void RegisterSingleModule(ModuleDefinition def, IPerformanceModule module)
        {
            var concreteType = module.GetType();
            var attr = concreteType.GetCustomAttribute<PerformanceModuleAttribute>(inherit: false);

            var category = attr?.Category ?? NodeCategory.VFX;
            var legacyPrefix = attr?.LegacyTypeNamePrefix;
            var customNodeType = attr?.CustomNodeType;

            var moduleName = def.moduleName;
            var primaryTypeName = $"Module_{moduleName}";
            var capturedDef = def;
            var nodeFactory = ResolveNodeFactory(customNodeType);

            // Primary: 新 typeName "Module_X" (menu に表示)
            _typeRegistry.Register(new NodeTypeInfo(primaryTypeName, moduleName, category));
            _graphState.RegisterNodeFactory(
                primaryTypeName,
                id => nodeFactory(id, primaryTypeName, capturedDef));

            // Legacy alias: load 専用。生成ノードの NodeType は canonical (M3) → 再保存しても旧 alias は永続化されない。
            if (string.IsNullOrEmpty(legacyPrefix)) return;

            var legacyTypeName = $"{legacyPrefix}{moduleName}";
            if (legacyTypeName == primaryTypeName) return;

            _graphState.RegisterNodeFactory(
                legacyTypeName,
                id => nodeFactory(id, primaryTypeName, capturedDef));
        }

        /// <summary>
        /// 属性から指定された <see cref="ModuleNodeBase"/> 派生型のファクトリを構築する。
        /// 派生型が無効な場合は汎用 <see cref="ModuleNode"/> にフォールバックして映像を止めない。
        /// </summary>
        private static Func<string, string, ModuleDefinition, ModuleNodeBase> ResolveNodeFactory(Type? customNodeType)
        {
            if (customNodeType == null)
                return (id, typeName, def) => new ModuleNode(id, typeName, def);

            if (!typeof(ModuleNodeBase).IsAssignableFrom(customNodeType))
            {
                Debug.LogError(
                    $"[NodeRegistrationOrchestrator] Custom node type '{customNodeType.FullName}' must derive " +
                    "from ModuleNodeBase. Falling back to generic ModuleNode.");
                return (id, typeName, def) => new ModuleNode(id, typeName, def);
            }

            var ctor = customNodeType.GetConstructor(
                new[] { typeof(string), typeof(string), typeof(ModuleDefinition) });
            if (ctor == null)
            {
                Debug.LogError(
                    $"[NodeRegistrationOrchestrator] Custom node type '{customNodeType.FullName}' must define " +
                    "ctor (string id, string typeName, ModuleDefinition def). Falling back to generic ModuleNode.");
                return (id, typeName, def) => new ModuleNode(id, typeName, def);
            }

            return (id, typeName, def) => (ModuleNodeBase)ctor.Invoke(new object[] { id, typeName, def });
        }

        /// <summary>
        /// Object3DPrefabList から Object3D_ の動的 typeName とファクトリを登録、prefab 逆引きマップも populate。
        /// </summary>
        private void RegisterObject3DTypes()
        {
            if (_object3DPrefabs == null) return;

            foreach (var prefab in _object3DPrefabs.Prefabs)
            {
                if (prefab == null) continue;
                var prefabName = prefab.name;
                var typeName = $"Object3D_{prefabName}";
                var capturedName = prefabName;

                Object3DPrefabMap[prefabName] = prefab;
                _typeRegistry.Register(new NodeTypeInfo(typeName, $"3D: {prefabName}", NodeCategory.Scene));
                _graphState.RegisterNodeFactory(typeName, id => new Object3DNode(id, capturedName));
            }
        }
    }
}
