#nullable enable

using Rhizomode.Input.Contracts;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.Interaction
{
    /// <summary>
    /// <see cref="NdiWindowsRoot"/> の <c>OnWindowSpawned</c> を subscribe し、
    /// 新規 window に <see cref="WindowGrabHandle"/> を attach + Initialize する binder。
    /// </summary>
    /// <remarks>
    /// <para>UI.Presentation → Interaction の参照を作らないための間接層。SampleScene に
    /// 1 個配置し、Bootstrap 側 wirer が <see cref="WindowInteractionContext"/> static に
    /// XR / Window 依存を push したのを read する (Plan v5.4 §15 「VContainer は Bootstrap 専用」
    /// 境界違反を避けるため、本クラスは [Inject] を使わない)。</para>
    ///
    /// <para>本 component は 1 シーン 1 instance を想定。多重配置すると同 window に handle が
    /// 重複 attach されるが <c>WindowGrabHandle</c> は <c>[DisallowMultipleComponent]</c> で
    /// 2 度目以降は Unity 警告 → no-op になる (defensive)。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WindowGrabBootstrap : MonoBehaviour
    {
        private NdiWindowsRoot? _windowsRoot;

        private void OnEnable()
        {
            _windowsRoot = WindowInteractionContext.WindowsRoot;
            if (_windowsRoot != null)
                _windowsRoot.OnWindowSpawned += AttachHandle;
        }

        private void OnDisable()
        {
            if (_windowsRoot != null)
                _windowsRoot.OnWindowSpawned -= AttachHandle;
            _windowsRoot = null;
        }

        private void AttachHandle(NdiViewWindow window)
        {
            if (window == null) return;
            var input = WindowInteractionContext.ControllerInput;
            var pose = WindowInteractionContext.ControllerPose;
            var leftInput = WindowInteractionContext.LeftInput;
            var leftRay = WindowInteractionContext.LeftRay;
            var sharedRaycast = WindowInteractionContext.SharedRaycast;
            if (input == null || pose == null || leftInput == null ||
                leftRay == null || sharedRaycast == null)
            {
                Debug.LogWarning("[WindowGrabBootstrap] WindowInteractionContext 未注入。" +
                                 "grab handle attach をスキップ (Bootstrap wirer が context を埋めるまで待機)。");
                return;
            }
            var handle = window.GetComponent<WindowGrabHandle>();
            if (handle == null) handle = window.gameObject.AddComponent<WindowGrabHandle>();
            handle.Initialize(input, pose, leftInput, leftRay, sharedRaycast);
        }
    }
}
