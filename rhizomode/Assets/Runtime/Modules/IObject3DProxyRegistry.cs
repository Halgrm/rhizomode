#nullable enable

namespace Rhizomode.Modules
{
    /// <summary>
    /// <see cref="Object3DProxy"/> をグラブハンドラに登録・解除する contract。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 6: <see cref="ModuleLifecycleProcessor"/> は Interaction 層に直接依存せず、
    /// proxy 登録を本 interface に委譲する。Bootstrap (Phase 8 で VContainer Installer) が
    /// Object3DGrabHandler を wrap した adapter を提供する。
    /// </remarks>
    public interface IObject3DProxyRegistry
    {
        void Register(Object3DProxy proxy);
        void Unregister(Object3DProxy proxy);
    }
}
