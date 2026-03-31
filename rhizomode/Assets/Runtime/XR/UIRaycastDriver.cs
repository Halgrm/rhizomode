#nullable enable

using System;
using R3;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// 毎フレームIRayProviderからレイを飛ばし、WorldPanelRayBridgeに
    /// ホバー・クリックイベントを伝達する。IControllerInputのOnSelectで
    /// PointerDown/Upを駆動する。
    /// </summary>
    public class UIRaycastDriver : MonoBehaviour
    {
        private const float RayMaxDistance = 5f;

        private IRayProvider? _rayProvider;
        private IControllerInput? _controllerInput;

        private WorldPanelRayBridge? _currentBridge;
        private bool _isSelectPressed;
        private IDisposable? _selectSubscription;

        /// <summary>
        /// 依存関係を設定する。
        /// </summary>
        public void Initialize(IRayProvider rayProvider, IControllerInput controllerInput)
        {
            _rayProvider = rayProvider;
            _controllerInput = controllerInput;

            _selectSubscription = controllerInput.OnSelect
                .Subscribe(OnSelectChanged);
        }

        private void Update()
        {
            if (_rayProvider == null) return;

            var ray = new Ray(_rayProvider.RayOrigin, _rayProvider.RayDirection);

            if (Physics.Raycast(ray, out var hit, RayMaxDistance))
            {
                var bridge = hit.collider.GetComponent<WorldPanelRayBridge>();
                if (bridge != null)
                {
                    HandleBridgeHit(bridge, hit);
                    return;
                }
            }

            // レイがどのパネルにも当たっていない
            ClearCurrentBridge();
        }

        private void HandleBridgeHit(WorldPanelRayBridge bridge, RaycastHit hit)
        {
            // 前フレームと違うパネルならホバー解除
            if (_currentBridge != null && _currentBridge != bridge)
            {
                _currentBridge.NotifyHoverExit();
            }

            _currentBridge = bridge;
            bridge.NotifyHover(hit);
        }

        private void OnSelectChanged(bool pressed)
        {
            _isSelectPressed = pressed;

            if (_currentBridge == null) return;

            if (pressed)
            {
                // 現在のレイ位置でPointerDown
                var ray = new Ray(_rayProvider!.RayOrigin, _rayProvider.RayDirection);
                if (Physics.Raycast(ray, out var hit, RayMaxDistance))
                {
                    var bridge = hit.collider.GetComponent<WorldPanelRayBridge>();
                    if (bridge == _currentBridge)
                    {
                        _currentBridge.NotifyPointerDown(hit);
                    }
                }
            }
            else
            {
                _currentBridge.NotifyPointerUp();
            }
        }

        private void ClearCurrentBridge()
        {
            if (_currentBridge != null)
            {
                _currentBridge.NotifyHoverExit();
                _currentBridge = null;
            }
        }

        private void OnDestroy()
        {
            _selectSubscription?.Dispose();
        }
    }
}
