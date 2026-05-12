#nullable enable

namespace Rhizomode.Interaction.Contracts
{
    /// <summary>
    /// Phase 1B placeholder for Rhizomode.Interaction.Contracts asmdef (Plan v5.3).
    ///
    /// Phase 5 で配置予定:
    ///   InteractionIntent (Spatial 系: Grab/Release/ConnectPorts/Disconnect/
    ///     SpawnNode/DeleteNode/MoveNode/RaySelect/MenuOpen)
    ///
    /// 制約: SharedKernel のみ依存。Interaction 本体 (state machine) や
    /// Interaction.GraphAdapter (command 発行) は Phase 1E で別 asmdef として追加。
    /// </summary>
    internal static class InteractionContractsAssemblyMarker
    {
    }
}
