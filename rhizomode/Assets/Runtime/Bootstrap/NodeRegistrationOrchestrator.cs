#nullable enable

using System;
using System.Collections.Generic;
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
            // CinemachineModule 用 factory は未実装のため type 登録も skip (Phase 4F 注記参照)
        }

        /// <summary>
        /// [NodeType] 属性付きクラスを Scanner で発見し、type / factory の両方を一括登録する。
        /// </summary>
        /// <remarks>
        /// N2 fix (2026-05-16): 旧 RegisterStaticTypesFromScanner + RegisterStaticFactories の二段登録を統合。
        /// Scanner が <c>NodeTypeRegistration.Factory</c> (reflection ctor invoke) を構築済のため、
        /// それをそのまま <see cref="GraphState.RegisterNodeFactory(string, Func{string, NodeBase})"/> に流す。
        /// 新ノード追加は [NodeType] 属性付与のみで完結し、本 orchestrator は触らずに済む。
        /// </remarks>
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
        /// ModuleDefinition 配列から VFX_/Shader_ の動的 typeName とファクトリを登録。
        /// Prefab 注入は ModuleLifecycleProcessor が AfterSetup で実施するため、ここは factory のみ。
        /// </summary>
        private void RegisterModuleTypes()
        {
            if (_moduleDefinitions == null) return;

            foreach (var def in _moduleDefinitions)
            {
                if (def == null) continue;
                var capturedDef = def;

                var hasVfx = def.prefab != null && def.prefab.GetComponent<VFXModule>() != null;
                var hasShader = def.prefab != null && def.prefab.GetComponent<ShaderModule>() != null;
                if (!hasVfx && !hasShader) { hasVfx = true; hasShader = true; }

                if (hasVfx)
                {
                    var typeName = $"VFX_{def.moduleName}";
                    _typeRegistry.Register(new NodeTypeInfo(typeName, $"VFX: {def.moduleName}", NodeCategory.VFX));
                    _graphState.RegisterNodeFactory(typeName, id => new VFXModuleNode(id, capturedDef));
                }
                if (hasShader)
                {
                    var typeName = $"Shader_{def.moduleName}";
                    _typeRegistry.Register(new NodeTypeInfo(typeName, $"Shader: {def.moduleName}", NodeCategory.Shader));
                    _graphState.RegisterNodeFactory(typeName, id => new ShaderModuleNode(id, capturedDef));
                }
            }
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
