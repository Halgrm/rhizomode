#nullable enable

using Rhizomode.Graph.Model;

namespace Rhizomode.Graph.Runtime
{
    /// <summary>
    /// ノードのライフサイクル各フェーズに介入する contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 6: 各 system (Modules / Scene / OscMidi / Ableton / Nodes.Defaults) が実装し、
    /// <see cref="NodeRuntime"/> から呼び出される。これにより GameBootstrap の InjectModuleIfNeeded /
    /// ReinjectModulesAfterLoad / Module instance 管理を完全に分離する (Phase 6 で削除)。
    ///
    /// 順序: BeforeSetup → (NodeBase.Setup) → AfterSetup → AfterDeserialize (全 node 完了後 1 回)。
    /// </remarks>
    public interface INodeLifecycleProcessor
    {
        /// <summary>ノードの <c>Setup</c> 前に呼ばれる。default 値適用・前提条件チェックなど。</summary>
        void BeforeSetup(NodeBase node, NodeInitMode mode);

        /// <summary>ノードの <c>Setup</c> 後に呼ばれる。Module インスタンス注入など。</summary>
        void AfterSetup(NodeBase node, NodeInitMode mode);

        /// <summary>Deserialize/Preset Import 完了後、全ノード・全エッジ復元後に 1 回呼ばれる。</summary>
        void AfterDeserialize(GraphState state);
    }
}
