#nullable enable

using Rhizomode.Input.Desktop;
using Rhizomode.Input.XR;
using Rhizomode.Interaction;
using VContainer;
using VContainer.Unity;

namespace Rhizomode.Bootstrap.Installers
{
    /// <summary>
    /// VContainer Installer — Input bounded context の入力ルーターを登録する。
    /// </summary>
    /// <remarks>
    /// Plan v5.4 §15 の <c>InputInstaller</c>。VR (<see cref="ControllerInputRouter"/>) と Desktop
    /// (<see cref="DesktopInputRouter"/>) の入力ルーターを <see cref="XrSceneReferences"/> から取得し、
    /// 配置済のものを concrete 型で container に登録する。
    ///
    /// V3c 時点で VR/Desktop の active 選択は <see cref="Wiring.InteractionBootstrapWiring"/> が
    /// scene の activeInHierarchy から行う (両ルーターを直接参照するため)。本 Installer の登録は
    /// V-final で active <c>IControllerInput</c> を container 解決へ移行する際の足場。
    /// </remarks>
    internal sealed class InputInstaller : IInstaller
    {
        private readonly XrSceneReferences _sceneRefs;

        public InputInstaller(XrSceneReferences sceneRefs)
        {
            _sceneRefs = sceneRefs;
        }

        public void Install(IContainerBuilder builder)
        {
            if (_sceneRefs.ControllerInput != null)
                builder.RegisterInstance(_sceneRefs.ControllerInput);
            if (_sceneRefs.DesktopInput != null)
                builder.RegisterInstance(_sceneRefs.DesktopInput);
            if (_sceneRefs.SharedRaycastService != null)
                builder.RegisterInstance(_sceneRefs.SharedRaycastService);
        }
    }
}
