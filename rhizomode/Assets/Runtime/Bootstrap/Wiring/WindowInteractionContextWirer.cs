#nullable enable

using Rhizomode.Input.Contracts;
using Rhizomode.Interaction;
using Rhizomode.UI;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Wiring
{
    /// <summary>
    /// VContainer Build 完了直後に <see cref="WindowInteractionContext"/> に
    /// 依存を push する wirer (BoundaryValidator Rule 7 対応)。
    /// </summary>
    /// <remarks>
    /// Interaction asmdef は VContainer を参照しないため <c>[Inject]</c> が使えない。
    /// 本 wirer (Bootstrap 側、VContainer 参照可) が XR / SharedRaycastService /
    /// NdiWindowsRoot を container から resolve して static context に書き込む。
    /// </remarks>
    internal sealed class WindowInteractionContextWirer : IInitializable
    {
        private readonly IControllerInput _controllerInput;
        private readonly IControllerPose _controllerPose;
        private readonly ILeftHandInput _leftInput;
        private readonly ILeftHandRay _leftRay;
        private readonly SharedRaycastService _sharedRaycast;
        private readonly NdiWindowsRoot _windowsRoot;

        public WindowInteractionContextWirer(
            IControllerInput controllerInput,
            IControllerPose controllerPose,
            ILeftHandInput leftInput,
            ILeftHandRay leftRay,
            SharedRaycastService sharedRaycast,
            NdiWindowsRoot windowsRoot)
        {
            _controllerInput = controllerInput;
            _controllerPose = controllerPose;
            _leftInput = leftInput;
            _leftRay = leftRay;
            _sharedRaycast = sharedRaycast;
            _windowsRoot = windowsRoot;
        }

        public void Initialize()
        {
            WindowInteractionContext.ControllerInput = _controllerInput;
            WindowInteractionContext.ControllerPose = _controllerPose;
            WindowInteractionContext.LeftInput = _leftInput;
            WindowInteractionContext.LeftRay = _leftRay;
            WindowInteractionContext.SharedRaycast = _sharedRaycast;
            WindowInteractionContext.WindowsRoot = _windowsRoot;
        }
    }
}
