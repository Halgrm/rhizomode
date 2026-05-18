#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Cameras;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Nodes.Modules;
using UnityEngine;

namespace Rhizomode.Modules
{
    /// <summary>
    /// すべての <see cref="Nodes.Modules.ModuleNodeBase"/> 派生 (VFX / Shader / InstancedCubes / future GPU instancing 等)
    /// および Object3D ノードの Prefab instantiation + <see cref="IPerformanceModule"/> 注入を統括する
    /// <see cref="INodeLifecycleProcessor"/>。
    ///
    /// 新 module 種別の追加は data-driven: prefab に IPerformanceModule 実装を貼るだけで本クラスは触らずに済む。
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
    public sealed class ModuleLifecycleProcessor : INodeLifecycleProcessor, INodeRemovalAware, IDisposable
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
            // 一時的な診断ログ: AfterSetup が ModuleNode に対して呼ばれているか / どの switch 分岐に入るか確認
            Debug.Log(
                $"[ModuleLifecycleProcessor] AfterSetup: node.Id={node.Id} type={node.GetType().Name} nodeType={node.NodeType} mode={mode}");
            switch (node)
            {
                case ModuleNodeBase moduleNode:
                    InstantiateModule(moduleNode, mode);
                    break;
                case Object3DNode obj3d:
                    InstantiateObject3D(obj3d, mode);
                    break;
            }
        }

        public void AfterDeserialize(GraphState state) { }

        /// <summary>
        /// F5 (2026-05-18): <see cref="INodeRemovalAware"/> 実装。GraphState.Clear / RestoreFromSnapshot の
        /// 直前に呼ばれ、個別 module instance を破棄する。Cue 経由 (OnGraphLoading subscriber 経由の
        /// <see cref="CleanupAll"/>) では _instances が既に空のため no-op になる安全 path。
        /// </summary>
        public void BeforeRemove(NodeBase node) => DestroyInstance(node.Id);

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

        /// <summary>
        /// <see cref="ModuleNodeBase"/> 全種 (VFX / Shader / InstancedCubes / future GPU instancing 等) 共通の
        /// prefab instantiation + <see cref="IPerformanceModule"/> 注入を行う。
        /// </summary>
        /// <remarks>
        /// 新 module 種別が追加されても本メソッドを触る必要はない:
        /// prefab に <c>IPerformanceModule</c> 実装が貼ってあれば <c>GetComponent</c> 経由で自動的に拾われる。
        /// H2 fix: 取得失敗時は <c>_instances</c> 登録前に rollback して zombie GameObject を残さない。
        /// </remarks>
        private void InstantiateModule(ModuleNodeBase node, NodeInitMode mode)
        {
            var def = node.Definition;
            if (def == null || def.prefab == null)
            {
                Debug.LogWarning($"[ModuleLifecycleProcessor] Module has no prefab assigned: {node.Id}");
                return;
            }

            GameObject? instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(def.prefab);
                instance.name = $"Module_{def.moduleName}_{node.Id[..Math.Min(8, node.Id.Length)]}";
                instance.transform.position = _placement.GetSpawnPosition(node, mode);

                var module = instance.GetComponent<IPerformanceModule>();
                if (module == null)
                {
                    // H2: instance を _instances に書く前に rollback、scene から確実に消す
                    Debug.LogError(
                        $"[ModuleLifecycleProcessor] Module prefab '{def.prefab.name}' lacks IPerformanceModule implementation");
                    UnityEngine.Object.Destroy(instance);
                    return;
                }

                module.Initialize(def);
                // Codex re-review fix (FAIL 3): Module setter は内部で Activate を呼ぶ。
                // ビルトイン実装は Activate 内 try-catch を持つが interface 契約としては保証されないため、
                // ここで catch して _instances 登録前に rollback する。
                try
                {
                    node.Module = module;
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[ModuleLifecycleProcessor] Module.Activate threw for '{def.moduleName}': {e.Message}");
                    UnityEngine.Object.Destroy(instance);
                    return;
                }
                _instances[node.Id] = instance;
                AttachLookAtMarker(instance, $"{def.moduleName} #{ShortId(node.Id)}");
                Debug.Log(
                    $"[ModuleLifecycleProcessor] Module attached: nodeId={node.Id} typeName={node.NodeType} moduleType={module.GetType().Name} instance.name={instance.name} pos={instance.transform.position}");
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[ModuleLifecycleProcessor] Module instantiation failed for '{def.moduleName}': {e.Message}");
                // 途中で例外 → scene に孤立 GameObject を残さない
                if (instance != null) UnityEngine.Object.Destroy(instance);
            }
        }

        private void InstantiateObject3D(Object3DNode node, NodeInitMode mode)
        {
            if (!_object3DPrefabs.TryGetValue(node.PrefabName, out var prefab) || prefab == null)
            {
                Debug.LogWarning($"[ModuleLifecycleProcessor] Object3D prefab not found: {node.PrefabName}");
                return;
            }

            GameObject? instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = $"Object3D_{node.PrefabName}_{node.Id[..Math.Min(8, node.Id.Length)]}";
                instance.transform.position = _placement.GetSpawnPosition(node, mode);

                // L6: VR グラブ操作前提なので prefab 側で必ず Collider を貼ること。
                // ランタイムに非 convex MeshCollider を AddComponent する暗黙挙動は廃止 (UX / 物理コスト両面で危険)。
                if (instance.GetComponent<Collider>() == null)
                {
                    Debug.LogError(
                        $"[ModuleLifecycleProcessor] Object3D prefab '{prefab.name}' has no Collider. Add one in the prefab to enable VR grab.");
                    UnityEngine.Object.Destroy(instance);
                    return;
                }

                var proxy = instance.GetComponent<Object3DProxy>();
                if (proxy == null) proxy = instance.AddComponent<Object3DProxy>();

                proxy.NodeId = node.Id;
                _instances[node.Id] = instance;
                _object3DRegistry?.Register(proxy);
                AttachLookAtMarker(instance, $"{node.PrefabName} #{ShortId(node.Id)}");

                // R3 Observable 購読は GraphState を持つ層 (Object3DProxyBindService) が
                // node.BindProxyObservables(state, proxy.Position, proxy.Scale) で別途行う。
                // 本 processor は GraphState 非依存。
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[ModuleLifecycleProcessor] Object3D instantiation failed for '{node.PrefabName}': {e.Message}");
                if (instance != null) UnityEngine.Object.Destroy(instance);
            }
        }

        /// <summary>
        /// Phase 1-B (2026-05-18): Module/Object3D instance に <see cref="LookAtTargetMarker"/> を attach し、
        /// <c>CameraManagerPanel</c> の LookAt dropdown に自動で列挙させる。
        /// </summary>
        /// <remarks>
        /// 既に attach 済 (prefab 側で貼ってあった) なら upsert 的に displayName だけ更新する。
        /// Destroy は Unity が GameObject 破棄時に Component も同時破棄するため明示処理不要。
        /// </remarks>
        private static void AttachLookAtMarker(GameObject instance, string displayName)
        {
            var marker = instance.GetComponent<LookAtTargetMarker>();
            if (marker == null) marker = instance.AddComponent<LookAtTargetMarker>();
            // F3 fix (Codex review): prefab で displayName を明示設定済なら上書きせず保持する。
            marker.SetDisplayNameIfEmpty(displayName);
        }

        private static string ShortId(string id) =>
            string.IsNullOrEmpty(id) ? "?" : id.Substring(0, Math.Min(8, id.Length));
    }
}
