#nullable enable

using System;
using R3;
using Rhizomode.ExternalInput;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// VRレイがClipObjectにヒットしている状態でRight-Trigger（OnSelect）押下→
    /// /live/clip_slot/fireをAbletonに送信する。NodeDeleteHandlerと同パターン。
    /// </summary>
    public class ClipFireRayHandler : MonoBehaviour
    {
        private SharedRaycastService? _sharedRaycast;
        private AbletonLink? _link;
        private IDisposable? _selectSubscription;

        private ClipObject? _hoveredClip;
        private bool _isEnabled = true;

        /// <summary>
        /// 外部からfire操作を有効/無効にする（メニューオープン中は無効化）。
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        public void Initialize(
            IControllerInput controllerInput,
            SharedRaycastService sharedRaycast,
            AbletonLink link)
        {
            _sharedRaycast = sharedRaycast;
            _link = link;

            // OnSelectは押下/離しの両方を流すbool。立ち上がり（押下）でfire。
            _selectSubscription = controllerInput.OnSelect
                .DistinctUntilChanged()
                .Where(v => v)
                .Subscribe(_ => FireHoveredClip());
        }

        private void Update()
        {
            if (_sharedRaycast == null) return;

            if (_sharedRaycast.HasHit)
            {
                var collider = _sharedRaycast.CurrentHit.collider;
                _hoveredClip = collider != null ? collider.GetComponentInParent<ClipObject>() : null;
            }
            else
            {
                _hoveredClip = null;
            }
        }

        private void FireHoveredClip()
        {
            if (!_isEnabled) return;
            if (_hoveredClip == null) return;
            if (_link == null) return;

            try
            {
                _link.Send("/live/clip_slot/fire", _hoveredClip.TrackIndex, _hoveredClip.SceneIndex);
                _hoveredClip.OnTriggered();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ClipFireRayHandler] Fire failed: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            _selectSubscription?.Dispose();
        }
    }
}
