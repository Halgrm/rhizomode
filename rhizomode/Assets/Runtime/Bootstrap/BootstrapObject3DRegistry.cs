#nullable enable

using System;
using Rhizomode.Modules;

namespace Rhizomode.Bootstrap
{
    /// <summary>
    /// Object3DProxy を grab handler に登録する <see cref="IObject3DProxyRegistry"/>。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 F-8.2 / F-8.7 prerequisite: 旧 GameBootstrap の private nested class
    /// BootstrapObject3DRegistry を Bootstrap asmdef に移送 (composition-root 責務の純化)。
    ///
    /// grab handler は Interaction 層に属するため、Bootstrap 層では具体型を直接参照せず
    /// Action callback でラップする (caller が `proxy => handler?.Register(proxy)` を渡す形)。
    /// これにより Bootstrap は Interaction 実装に対する依存を持たない。
    /// </remarks>
    public sealed class BootstrapObject3DRegistry : IObject3DProxyRegistry
    {
        private readonly Action<Object3DProxy> _register;
        private readonly Action<Object3DProxy> _unregister;

        public BootstrapObject3DRegistry(
            Action<Object3DProxy> register,
            Action<Object3DProxy> unregister)
        {
            _register = register;
            _unregister = unregister;
        }

        public void Register(Object3DProxy proxy) => _register(proxy);
        public void Unregister(Object3DProxy proxy) => _unregister(proxy);
    }
}
