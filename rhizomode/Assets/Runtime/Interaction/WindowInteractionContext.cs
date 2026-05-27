#nullable enable

using Rhizomode.Input.Contracts;
using Rhizomode.UI;

namespace Rhizomode.Interaction
{
    /// <summary>
    /// <see cref="WindowGrabBootstrap"/> が必要とする XR / Window 依存を Bootstrap から
    /// push してもらう静的ホルダ。VContainer 直接参照を避けるための service locator。
    /// </summary>
    /// <remarks>
    /// <para>Plan v5.4 §15 +  BoundaryViolationValidator Rule 7「VContainer は
    /// <c>Rhizomode.Bootstrap</c> 専用」。Interaction が <c>[Inject]</c> や
    /// <c>LifetimeScope.Find</c> を使うと build 時に <c>BuildFailedException</c> で弾かれる。</para>
    ///
    /// <para>Bootstrap 側 wirer が container から resolve して本クラスに push する設計
    /// (UI.Presentation の <see cref="NdiPresentationContext"/> と同パターン)。</para>
    /// </remarks>
    public static class WindowInteractionContext
    {
        public static IControllerInput? ControllerInput { get; set; }
        public static IControllerPose? ControllerPose { get; set; }
        public static ILeftHandInput? LeftInput { get; set; }
        public static ILeftHandRay? LeftRay { get; set; }
        public static SharedRaycastService? SharedRaycast { get; set; }
        public static NdiWindowsRoot? WindowsRoot { get; set; }

        /// <summary>EditMode test 用 reset。</summary>
        public static void Reset()
        {
            ControllerInput = null;
            ControllerPose = null;
            LeftInput = null;
            LeftRay = null;
            SharedRaycast = null;
            WindowsRoot = null;
        }
    }
}
