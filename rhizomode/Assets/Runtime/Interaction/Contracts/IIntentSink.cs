#nullable enable

namespace Rhizomode.Interaction.Contracts
{
    /// <summary>
    /// 空間操作 <see cref="IInteractionIntent"/> の sink (intent 発行先) 契約。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 5: <c>Interaction</c> asmdef 配下の handler は本 sink にのみ依存し、
    /// <c>Graph.Mutation</c> / <c>GraphCommandDispatcher</c> / 具象 <c>SpatialIntentToCommandTranslator</c>
    /// を直接知らない。これにより Interaction asmdef を Graph.* 非依存にできる
    /// (Phase 5 完了条件)。
    ///
    /// 実装: <c>Rhizomode.Interaction.GraphAdapter.SpatialIntentToCommandTranslator</c>。
    /// </remarks>
    public interface IIntentSink
    {
        /// <summary>intent を発行する。受領されて適切な command が発火された場合 true。</summary>
        bool Emit(IInteractionIntent intent);
    }
}
