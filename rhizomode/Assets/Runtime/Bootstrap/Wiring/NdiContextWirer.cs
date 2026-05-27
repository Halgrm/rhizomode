#nullable enable

using System;
using Rhizomode.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// VContainer Build 完了直後に <see cref="NdiPresentationContext"/> に
    /// 依存を push する wirer (BoundaryValidator Rule 7 対応)。
    /// </summary>
    /// <remarks>
    /// <para>UI.Presentation は VContainer を参照しないため <c>[Inject]</c> が使えない。
    /// 本 wirer (Bootstrap 側、VContainer 参照可) が container から resolve した依存を static
    /// context に書き込む service locator パターン。</para>
    ///
    /// <para>Ctor injection で必須にすると登録漏れで container build が壊れ
    /// UI 全消失する致命的 regression を起こすため、<see cref="IObjectResolver"/> 経由で
    /// 個別 <see cref="TryResolve{T}"/> し、見つからないものは null で context に書く
    /// defensive 設計。</para>
    /// </remarks>
    internal sealed class NdiContextWirer : IInitializable
    {
        private readonly IObjectResolver _resolver;

        public NdiContextWirer(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public void Initialize()
        {
            NdiPresentationContext.Health = TryResolve<NdiReceiverHealth>();
            NdiPresentationContext.WindowsRoot = TryResolve<NdiWindowsRoot>();

            if (NdiPresentationContext.Health == null)
                Debug.LogWarning("[NdiContextWirer] NdiReceiverHealth not registered.");
            if (NdiPresentationContext.WindowsRoot == null)
                Debug.LogWarning("[NdiContextWirer] NdiWindowsRoot not registered (SampleScene に root が無い)。");
        }

        private T? TryResolve<T>() where T : class
        {
            try { return _resolver.Resolve<T>(); }
            catch (Exception) { return null; }
        }
    }
}
