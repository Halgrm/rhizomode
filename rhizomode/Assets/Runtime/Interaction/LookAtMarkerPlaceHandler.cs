#nullable enable

using System;
using R3;
using Rhizomode.Cameras;
using Rhizomode.UI;
using UnityEngine;

using Rhizomode.Input.Contracts;

namespace Rhizomode.XR
{
    /// <summary>
    /// Phase 2-A (2026-05-18): place mode 中、Right-Select (右トリガー) で空中に
    /// <see cref="LookAtMarkerVisual"/> を生成する handler。1 回押下で 1 個生成し、自動的に place mode を抜ける。
    /// </summary>
    /// <remarks>
    /// 配置位置の決定:
    /// - <see cref="SharedRaycastService.HasHit"/> なら hit.point に配置
    /// - hit が無ければコントローラー前方 <see cref="FallbackPlaceDistance"/> m
    /// </remarks>
    public class LookAtMarkerPlaceHandler : MonoBehaviour
    {
        // 視界内に必ず収まるよう、コントローラ前方の手元距離に配置する。
        // 1.5m 等遠方だと HMD 視野角 (~100°) の外側に行きやすく、初回配置で球が見つからない UX 事故になる。
        // 配置後 grab で遠方に動かす運用前提。
        private const float FallbackPlaceDistance = 0.6f;
        private const float FallbackMinDistance = 0.3f;

        private IControllerPose? _controllerPose;
        private SharedRaycastService? _sharedRaycast;
        private LookAtMarkerVisualManager? _manager;
        private Func<bool>? _isMenuActive;
        private IDisposable? _subscriptions;

        public void Initialize(
            IControllerInput controllerInput,
            IControllerPose controllerPose,
            SharedRaycastService sharedRaycast,
            LookAtMarkerVisualManager manager,
            Func<bool>? isMenuActive = null)
        {
            _controllerPose = controllerPose;
            _sharedRaycast = sharedRaycast;
            _manager = manager;
            _isMenuActive = isMenuActive;

            var d = Disposable.CreateBuilder();
            // 右トリガー (Select) 押下の rising edge で 1 個生成。
            controllerInput.OnSelect
                .Subscribe(pressed => { if (pressed) TryPlace(); })
                .AddTo(ref d);
            _subscriptions = d.Build();
        }

        private void TryPlace()
        {
            if (_manager == null || !_manager.IsPlacing) return;
            if (_controllerPose == null) return;
            // F4 fix (Codex review): ScrollMenu が開いている / drag 中ならノード生成 trigger と
            // 競合するので Place trigger を抑止する。
            if (_isMenuActive != null && _isMenuActive()) return;

            Vector3 worldPos;
            if (_sharedRaycast != null && _sharedRaycast.HasHit)
            {
                worldPos = _sharedRaycast.CurrentHit.point;
            }
            else
            {
                // F5 fix (Codex review): RayDirection が極端 (体の方を向く等) でも最低距離を確保する。
                var origin = _controllerPose.RayOrigin;
                var direction = _controllerPose.RayDirection;
                worldPos = origin + direction * Mathf.Max(FallbackPlaceDistance, FallbackMinDistance);
            }

            _manager.Spawn(worldPos);
            _manager.EndPlacing();
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
        }
    }
}
