#nullable enable

using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using UnityEngine;

namespace Rhizomode.Modules
{
    /// <summary>
    /// Module Prefab インスタンスの world 配置位置を解決する。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 6: <see cref="ModuleLifecycleProcessor"/> はノード位置と spawn 位置を
    /// 区別する必要がある (VFX/Object3D は FreshSpawn 時にプレイヤー前方の専用距離に出る)。
    /// 直接 IHeadPoseProvider / ControllerInput に依存させず、placement 決定を外部に委譲する。
    ///
    /// GameBootstrap の実装が FreshSpawn 時に head + forward * offset を返し、
    /// Deserialize 時は <see cref="NodeBase.Position"/> を返す。
    /// </remarks>
    public interface IModulePlacementService
    {
        /// <summary>
        /// 指定ノードの module instance を world に配置する位置を返す。
        /// </summary>
        Vector3 GetSpawnPosition(NodeBase node, NodeInitMode mode);
    }
}
