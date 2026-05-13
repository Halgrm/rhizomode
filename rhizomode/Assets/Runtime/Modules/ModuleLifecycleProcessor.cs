#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Nodes.Modules;
using UnityEngine;

namespace Rhizomode.Modules
{
    /// <summary>
    /// VFX / Shader / Object3D モジュールノードの Prefab instantiation + IPerformanceModule 注入を
    /// 統括する <see cref="INodeLifecycleProcessor"/>。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 6: GameBootstrap の InstantiateVFXModule / InstantiateShaderModule /
    /// InstantiateObject3D / InjectModuleIfNeeded / DestroyModuleInstance /
    /// CleanupModuleInstances を本クラスに集約。GameBootstrap は本クラスを保持して
    /// AfterSetup / DestroyInstance / CleanupAll を呼ぶだけ。
    ///
    /// Phase 8 で NodeRuntime + GraphEventBus.OnNodeRemoved 経由の自動駆動に切替予定
    /// (現状は GameBootstrap が手動で呼ぶ transitional 形)。
    ///
    /// 設計原則:
    /// - Module/Object3D の placement は <see cref="IModulePlacementService"/> に委譲。
    /// - Object3DProxy のグラブ登録は <see cref="IObject3DProxyRegistry"/> に委譲。
    /// - エラー時は Debug.LogError で報告し、Video は止めない (映像継続原則)。
    /// </remarks>
    public sealed class ModuleLifecycleProcessor : INodeLifecycleProcessor, IDisposable
    {
        private readonly IReadOnlyDictionary<string, GameObject> _object3DPrefabs;
        private readonly IModulePlacementService _placement;
        private readonly IObject3DProxyRegistry? _object3DRegistry;
        private readonly Dictionary<string, GameObject> _instances = new();

        /// <summary>
        /// 現在の module instance マップへの読み取り専用アクセス (Phase 8 で削除予定、
        /// GameBootstrap が DestroyModuleInstance から移行する際の検証用)。
        /// </summary>
        public IReadOnlyDictionary<string, GameObject> Instances => _instances;

        public ModuleLifecycleProcessor(
            IReadOnlyDictionary<string, GameObject> object3DPrefabs,
            IModulePlacementService placement,
            IObject3DProxyRegistry? object3DRegistry = null)
        {
            _object3DPrefabs = object3DPrefabs;
            _placement = placement;
            _object3DRegistry = object3DRegistry;
        }

        public void BeforeSetup(NodeBase node, NodeInitMode mode) { }

        public void AfterSetup(NodeBase node, NodeInitMode mode)
        {
            switch (node)
            {
                case VFXModuleNode vfx:
                    InstantiateVfx(vfx, mode);
                    break;
                case ShaderModuleNode shader:
                    InstantiateShader(shader);
                    break;
                case Object3DNode obj3d:
                    InstantiateObject3D(obj3d, mode);
                    break;
            }
        }

        public void AfterDeserialize(GraphState state) { }

        /// <summary>
        /// 指定ノードのモジュール Prefab インスタンスを破棄する。
        /// Phase 8 では GraphEventBus.OnNodeRemoved 購読に置換予定。
        /// </summary>
        public void DestroyInstance(string nodeId)
        {
            if (!_instances.TryGetValue(nodeId, out var instance)) return;
            _instances.Remove(nodeId);
            if (instance == null) return;

            var proxy = instance.GetComponent<Object3DProxy>();
            if (proxy != null) _object3DRegistry?.Unregister(proxy);
            UnityEngine.Object.Destroy(instance);
        }

        /// <summary>全モジュール Prefab インスタンスを破棄する (グラフ切替時のリーク防止)。</summary>
        public void CleanupAll()
        {
            foreach (var instance in _instances.Values)
            {
                if (instance == null) continue;
                var proxy = instance.GetComponent<Object3DProxy>();
                if (proxy != null) _object3DRegistry?.Unregister(proxy);
                UnityEngine.Object.Destroy(instance);
            }
            _instances.Clear();
        }

        public void Dispose()
        {
            CleanupAll();
        }

        private void InstantiateVfx(VFXModuleNode node, NodeInitMode mode)
        {
            var def = node.Definition;
            if (def == null || def.prefab == null)
            {
                Debug.LogWarning($"[ModuleLifecycleProcessor] VFX module has no prefab assigned: {node.Id}");
                return;
            }

            try
            {
                var instance = UnityEngine.Object.Instantiate(def.prefab);
                instance.name = $"VFXModule_{def.moduleName}_{node.Id[..Math.Min(8, node.Id.Length)]}";
                instance.transform.position = _placement.GetSpawnPosition(node, mode);
                _instances[node.Id] = instance;

                var module = instance.GetComponent<VFXModule>();
                if (module != null)
                {
                    module.Initialize(def);
                    node.Module = module;
                }
                else
                {
                    Debug.LogError(
                        $"[ModuleLifecycleProcessor] VFX prefab '{def.prefab.name}' lacks VFXModule component");
                }
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[ModuleLifecycleProcessor] VFX instantiation failed for '{def.moduleName}': {e.Message}");
            }
        }

        private void InstantiateShader(ShaderModuleNode node)
        {
            var def = node.Definition;
            if (def == null || def.prefab == null)
            {
                Debug.LogWarning($"[ModuleLifecycleProcessor] Shader module has no prefab assigned: {node.Id}");
                return;
            }

            try
            {
                var instance = UnityEngine.Object.Instantiate(def.prefab);
                instance.name = $"ShaderModule_{def.moduleName}_{node.Id[..Math.Min(8, node.Id.Length)]}";
                _instances[node.Id] = instance;

                var module = instance.GetComponent<ShaderModule>();
                var renderer = instance.GetComponent<Renderer>();
                if (module != null && renderer != null)
                {
                    module.Initialize(def, renderer);
                    node.Module = module;
                }
                else
                {
                    Debug.LogError(
                        $"[ModuleLifecycleProcessor] Shader prefab '{def.prefab.name}' lacks ShaderModule or Renderer");
                }
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[ModuleLifecycleProcessor] Shader instantiation failed for '{def.moduleName}': {e.Message}");
            }
        }

        private void InstantiateObject3D(Object3DNode node, NodeInitMode mode)
        {
            if (!_object3DPrefabs.TryGetValue(node.PrefabName, out var prefab) || prefab == null)
            {
                Debug.LogWarning($"[ModuleLifecycleProcessor] Object3D prefab not found: {node.PrefabName}");
                return;
            }

            try
            {
                var instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = $"Object3D_{node.PrefabName}_{node.Id[..Math.Min(8, node.Id.Length)]}";
                instance.transform.position = _placement.GetSpawnPosition(node, mode);
                _instances[node.Id] = instance;

                var proxy = instance.GetComponent<Object3DProxy>();
                if (proxy == null) proxy = instance.AddComponent<Object3DProxy>();

                if (instance.GetComponent<Collider>() == null)
                {
                    if (instance.GetComponent<MeshFilter>() != null)
                        instance.AddComponent<MeshCollider>();
                    else
                        instance.AddComponent<BoxCollider>();
                }

                proxy.NodeId = node.Id;
                _object3DRegistry?.Register(proxy);

                // R3 Observable 購読は呼び出し側 (GraphState を持つ層) が
                // node.BindProxyObservables(state, proxy.Position, proxy.Scale) で行う。
                // 本 processor は GraphState への依存を持たない設計のため、
                // BindProxyObservables 呼び出しは GameBootstrap または Phase 8 の Installer で実施。
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[ModuleLifecycleProcessor] Object3D instantiation failed for '{node.PrefabName}': {e.Message}");
            }
        }
    }
}
