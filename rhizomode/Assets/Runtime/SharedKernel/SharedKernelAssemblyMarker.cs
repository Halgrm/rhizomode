#nullable enable

namespace Rhizomode.SharedKernel
{
    /// <summary>
    /// Phase 1A placeholder for Rhizomode.SharedKernel asmdef (Plan v5.3).
    ///
    /// Phase 1A 後の配置予定:
    ///   ParamType (旧 Rhizomode.Core)
    ///   ParamDefaults (旧 Rhizomode.Core)
    ///   ParamValue (discriminated union、Phase 2 で実装)
    ///   NodeId, PortId (Phase 2)
    ///   Result&lt;T&gt; (Phase 2)
    ///   RzColor, RzVector3, RzVector2, RzQuaternion (Phase 2)
    ///   RzMath (Approximately + IsFinite のみ、Phase 2)
    ///
    /// 制約:
    ///   references = [] (UnityEngine.CoreModule / R3.Unity 参照禁止、CI で検証)
    ///   noEngineReferences = true (Unity Engine 全 module 非参照)
    /// </summary>
    internal static class SharedKernelAssemblyMarker
    {
    }
}
