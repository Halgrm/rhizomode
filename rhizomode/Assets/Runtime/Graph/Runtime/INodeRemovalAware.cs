#nullable enable

using Rhizomode.Graph.Model;

namespace Rhizomode.Graph.Runtime
{
    /// <summary>
    /// ノード削除 (個別 RemoveNode / GraphState.Clear 一括) の直前に呼ばれる lifecycle hook。
    /// </summary>
    /// <remarks>
    /// F5 (2026-05-18): <see cref="INodeLifecycleProcessor"/> は signature 変更が Breaking Change
    /// 規約で禁止されているため、削除路の hook は別 interface として opt-in する。
    /// <see cref="NodeRuntime.BeforeClear"/> から各 processor を <c>is INodeRemovalAware</c> で cast し、
    /// 実装している processor だけ通知される。
    ///
    /// 主用途は <see cref="GraphMutationApplier.RestoreFromSnapshot"/> 経路の安全網:
    /// dispatcher 直呼び Undo/Redo では <c>GraphSaveLoadManager.OnGraphLoading</c> が raise
    /// されず <c>ModuleLifecycleProcessor.CleanupAll</c> も走らないため、Module の
    /// 個別破棄をこの hook で担う。CueLibraryService 経由は CleanupAll 完了後に Restore に至るため
    /// この hook は _instances 空に対する no-op になる (二重防御で副作用なし)。
    /// </remarks>
    public interface INodeRemovalAware
    {
        /// <summary>node が GraphState から取り除かれる直前に呼ばれる。例外は NodeRuntime が握り潰す。</summary>
        void BeforeRemove(NodeBase node);
    }
}
