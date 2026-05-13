#nullable enable

namespace Rhizomode.Graph.Runtime
{
    /// <summary>
    /// <c>HydrationPlan</c> を受けて Deserialize 順序を駆動する executor。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 6: Graph.Serialization の <c>GraphHydrator</c> は plan builder のみで実行しない。
    /// 本クラスが plan を受け取り <see cref="NodeRuntime"/> 経由で
    /// BeforeSetup → Setup → AfterSetup → AfterDeserialize → OnGraphChanged を駆動する。
    ///
    /// Phase 2 では skeleton (実装は Phase 6/7)。Plan 自体の DTO は Graph.Serialization に置く。
    /// </remarks>
    public sealed class HydrationPlanExecutor
    {
        private readonly NodeRuntime _runtime;

        public HydrationPlanExecutor(NodeRuntime runtime)
        {
            _runtime = runtime;
        }

        // 実装は Phase 6/7 で本格化:
        //   public void Execute(HydrationPlan plan) { ... }
        //   全 node を Create → RestoreParamsFromSnapshot → runtime.RegisterNode(node, Deserialize)
        //   全 edge を復元 → 1 回 AfterDeserialize
    }
}
