#nullable enable

using System;
using R3;
using Rhizomode.UI;
using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// SharedRaycastServiceの結果を使い、WorldPanelRayBridgeに
    /// ホバー・クリックイベントを伝達する。独自レイキャストは行わない。
    /// </summary>
    [DefaultExecutionOrder(10)] // SharedRaycastService(-10)の後に実行
    public class UIRaycastDriver : MonoBehaviour
    {
        private SharedRaycastService? _sharedRaycast;
        private IControllerInput? _controllerInput;

        private WorldPanelRayBridge? _currentBridge;
        private IDisposable? _selectSubscription;

        /// <summary>
        /// 依存関係を設定する。
        /// </summary>
        public void Initialize(IControllerInput controllerInput,
            SharedRaycastService sharedRaycast)
        {
            _sharedRaycast = sharedRaycast;
            _controllerInput = controllerInput;

            _selectSubscription = controllerInput.OnSelect
                .Subscribe(OnSelectChanged);
        }

        private void Update()
        {
            if (_sharedRaycast == null) return;

            if (_sharedRaycast.HasHit)
            {
                var hit = _sharedRaycast.CurrentHit;
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
            if (_currentBridge == null) return;

            if (pressed)
            {
                // SharedRaycastServiceの現在の結果を使ってPointerDown
                if (_sharedRaycast != null && _sharedRaycast.HasHit)
                {
                    var hit = _sharedRaycast.CurrentHit;
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
