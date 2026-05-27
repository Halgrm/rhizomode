#nullable enable

using Rhizomode.Input.Contracts;
using Rhizomode.UI;
using UnityEngine;
using VContainer;

namespace Rhizomode.Interaction
{
    /// <summary>
    /// <see cref="NdiWindowsRoot"/> の <c>OnWindowSpawned</c> を subscribe し、
    /// 新規 window に <see cref="WindowGrabHandle"/> を attach + Initialize する binder。
    /// </summary>
    /// <remarks>
    /// <para>UI.Presentation → Interaction の参照を作らないための間接層。SampleScene に
    /// 1 個配置し、VContainer から XR 系依存を [Inject] で受け取って各 window に push する。</para>
    ///
    /// <para>本 component は 1 シーン 1 instance を想定。多重配置すると同 window に handle が
    /// 重複 attach されるが <c>WindowGrabHandle</c> は <c>[DisallowMultipleComponent]</c> で
    /// 2 度目以降は Unity 警告 → no-op になる (defensive)。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WindowGrabBootstrap : MonoBehaviour
    {
        private IControllerInput? _input;
        private IControllerPose? _pose;
        private ILeftHandInput? _leftInput;
        private ILeftHandRay? _leftRay;
        private SharedRaycastService? _sharedRaycast;
        private NdiWindowsRoot? _windowsRoot;

        [Inject]
        public void Construct(
            IControllerInput input,
            IControllerPose pose,
            ILeftHandInput leftInput,
            ILeftHandRay leftRay,
            SharedRaycastService sharedRaycast,
            NdiWindowsRoot windowsRoot)
        {
            _input = input;
            _pose = pose;
            _leftInput = leftInput;
            _leftRay = leftRay;
            _sharedRaycast = sharedRaycast;
            _windowsRoot = windowsRoot;
        }

        private void OnEnable()
        {
            if (_windowsRoot != null)
                _windowsRoot.OnWindowSpawned += AttachHandle;
        }

        private void OnDisable()
        {
            if (_windowsRoot != null)
                _windowsRoot.OnWindowSpawned -= AttachHandle;
        }

        private void AttachHandle(NdiViewWindow window)
        {
            if (window == null) return;
            if (_input == null || _pose == null || _leftInput == null ||
                _leftRay == null || _sharedRaycast == null)
            {
                Debug.LogWarning("[WindowGrabBootstrap] 依存未注入。grab handle attach をスキップ。");
                return;
            }
            var handle = window.GetComponent<WindowGrabHandle>();
            if (handle == null) handle = window.gameObject.AddComponent<WindowGrabHandle>();
            handle.Initialize(_input, _pose, _leftInput, _leftRay, _sharedRaycast);
        }
    }
}
