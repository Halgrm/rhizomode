#nullable enable

using Rhizomode.Modules;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// Object3DProxy を grab handler に登録する <see cref="IObject3DProxyRegistry"/>。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 V-final (Vf-b): 旧 ctor の <c>Action&lt;Object3DProxy&gt;</c> 2 callback を廃止し、
    /// <see cref="XrSceneReferences"/> を直接 ctor 注入して <c>Object3DGrabHandler</c> に逐次転送する形に
    /// refactor。これにより本サービスは container 登録可能なプレーンな singleton になり、
    /// <see cref="EntryPointBootstrapper.Launch"/> 内で local closure を生成する必要がなくなる。
    ///
    /// XrSceneReferences は Bootstrap asmdef、Object3DGrabHandler は XR asmdef (Interaction 層) に
    /// 属するが、Bootstrap が XR を参照しないルール (§19) は XrSceneReferences の getter 経由で守られる:
    /// 本クラスは <c>_refs.Object3DGrabHandler?.Register(...)</c> として handler 具体型に依存しない getter で
    /// アクセスし、XrSceneReferences 側が Interaction 層への参照を集約する。
    /// </remarks>
    public sealed class BootstrapObject3DRegistry : IObject3DProxyRegistry
    {
        private readonly XrSceneReferences _refs;

        public BootstrapObject3DRegistry(XrSceneReferences refs)
        {
            _refs = refs;
        }

        public void Register(Object3DProxy proxy) => _refs.Object3DGrabHandler?.Register(proxy);

        public void Unregister(Object3DProxy proxy) => _refs.Object3DGrabHandler?.Unregister(proxy);
    }
}
