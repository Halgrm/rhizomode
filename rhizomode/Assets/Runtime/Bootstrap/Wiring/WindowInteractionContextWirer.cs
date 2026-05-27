#nullable enable

using System;
using Rhizomode.Input.Contracts;
using Rhizomode.Input.XR;
using Rhizomode.Input.Desktop;
using Rhizomode.Interaction;
using Rhizomode.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// VContainer Build 完了直後に <see cref="WindowInteractionContext"/> に
    /// 依存を push する wirer (BoundaryValidator Rule 7 対応)。
    /// </summary>
    /// <remarks>
    /// <para>Interaction asmdef は VContainer を参照しないため <c>[Inject]</c> が使えない。
    /// 本 wirer (Bootstrap 側、VContainer 参照可) が XR / SharedRaycastService /
    /// NdiWindowsRoot を container から resolve して static context に書き込む。</para>
    ///
    /// <para>Ctor injection で全 dep 必須にすると、登録漏れ (例:
    /// <c>InputInstaller</c> が concrete <see cref="ControllerInputRouter"/> のみ登録、
    /// <see cref="IControllerInput"/> 経由の resolve が失敗するパターン) で
    /// container 全体が build 失敗 → 全 UI が出なくなる致命的 regression が起きる。
    /// そのため <see cref="IObjectResolver"/> 経由の <see cref="TryResolve{T}"/> で
    /// 個別に解決し、見つからないものは null で context に書く defensive 設計。</para>
    /// </remarks>
    internal sealed class WindowInteractionContextWirer : IInitializable
    {
        private readonly IObjectResolver _resolver;

        public WindowInteractionContextWirer(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public void Initialize()
        {
            // XR の 4 interface (IControllerInput / IControllerPose / ILeftHandInput / ILeftHandRay)
            // は scene 上の ControllerInputRouter / DesktopInputRouter が concrete として登録される。
            // 各 interface 名で直接 resolve が落ちるため、concrete を resolve してから cast する。
            var xr = TryResolve<ControllerInputRouter>();
            var desktop = TryResolve<DesktopInputRouter>();
            IControllerInput? input = (IControllerInput?)xr ?? desktop;
            IControllerPose? pose = (IControllerPose?)xr ?? desktop;
            ILeftHandInput? leftInput = (ILeftHandInput?)xr ?? desktop;
            ILeftHandRay? leftRay = (ILeftHandRay?)xr ?? desktop;

            WindowInteractionContext.ControllerInput = input;
            WindowInteractionContext.ControllerPose = pose;
            WindowInteractionContext.LeftInput = leftInput;
            WindowInteractionContext.LeftRay = leftRay;
            WindowInteractionContext.SharedRaycast = TryResolve<SharedRaycastService>();
            WindowInteractionContext.WindowsRoot = TryResolve<NdiWindowsRoot>();

            if (input == null || pose == null || leftInput == null || leftRay == null)
                Debug.LogWarning("[WindowInteractionContextWirer] XR/Desktop input not registered. " +
                                 "Window grab handle will be disabled until input router is wired.");
            if (WindowInteractionContext.SharedRaycast == null)
                Debug.LogWarning("[WindowInteractionContextWirer] SharedRaycastService not registered.");
            if (WindowInteractionContext.WindowsRoot == null)
                Debug.LogWarning("[WindowInteractionContextWirer] NdiWindowsRoot not registered.");
        }

        private T? TryResolve<T>() where T : class
        {
            try { return _resolver.Resolve<T>(); }
            catch (Exception) { return null; }
        }
    }
}
