#nullable enable

using System;
using R3;
using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// 巻物メニューのインタラクション制御（左手トリガー完結）。
    /// Idle: 左レイ→バーハイライト。左トリガー on バー → Dragging。
    /// Dragging: 手を上げてスクロール展開。左トリガー離し → Open/閉じ。
    /// Open: 左トリガー on スクロールボタン → ノード生成。
    ///        左トリガー on バー → 別カテゴリに切替。
    ///        左トリガー on 空 → メニュー閉じ。
    /// </summary>
    public class ScrollMenuInteractionHandler : MonoBehaviour
    {
        private enum MenuState
        {
            Idle,
            Dragging,
            Open
        }

        private const float DragSensitivity = 3f;
        private const float MinDragThreshold = 0.01f;
        private const float LeftRayMaxDistance = 2f;

        private ScrollMenuVisualController? _visualController;
        private IControllerInput? _controllerInput;
        private ILeftHandRay? _leftHandRay;
        private SharedRaycastService? _sharedRaycast;
        private EdgeDragHandler? _edgeDragHandler;
        private Action<bool>? _onMenuStateChanged;

        private MenuState _state = MenuState.Idle;
        private float _dragStartY;
        private float _currentScrollHeight;
        private bool _isDesktopMode;

        // 左手レイキャスト結果（毎フレーム更新）
        private bool _leftHasHit;
        private RaycastHit _leftHit;

        private IDisposable? _subscriptions;

        /// <summary>
        /// 初期化。入力とビジュアルコントローラーを接続する。
        /// </summary>
        public void Initialize(
            IControllerInput controllerInput,
            ILeftHandRay leftHandRay,
            ILeftHandInput leftHandInput,
            SharedRaycastService sharedRaycast,
            ScrollMenuVisualController visualController)
        {
            _controllerInput = controllerInput;
            _leftHandRay = leftHandRay;
            _sharedRaycast = sharedRaycast;
            _visualController = visualController;

            var d = Disposable.CreateBuilder();

            // Xボタン → メニュー閉じ（Open/Dragging時）
            controllerInput.OnOpenMenu
                .Subscribe(_ => OnCloseMenu())
                .AddTo(ref d);

            // 右トリガー → ボタンクリック（デスクトップではバー操作も兼用）
            controllerInput.OnSelect
                .Subscribe(pressed => OnRightSelect(pressed))
                .AddTo(ref d);

            // 左トリガー → 全操作統一
            leftHandInput.OnLeftSelect
                .Subscribe(pressed => OnLeftTrigger(pressed))
                .AddTo(ref d);

            _subscriptions = d.Build();
        }

        /// <summary>
        /// メニューオープン中にエッジ接続を無効化するためのハンドラ参照を設定する。
        /// </summary>
        public void SetEdgeDragHandler(EdgeDragHandler edgeDragHandler)
        {
            _edgeDragHandler = edgeDragHandler;
        }

        /// <summary>
        /// デスクトップモードを設定する。ドラッグ展開をスキップし、バークリックで即Open。
        /// </summary>
        public void SetDesktopMode(bool isDesktop)
        {
            _isDesktopMode = isDesktop;
        }

        /// <summary>
        /// メニュー状態変更コールバックを設定する。isIdle=trueで通常操作有効、falseで無効化。
        /// GameBootstrapからEdgeCutHandler/NodeDeleteHandlerのSetEnabledを橋渡しする用。
        /// </summary>
        public void SetMenuStateCallback(Action<bool> callback)
        {
            _onMenuStateChanged = callback;
        }

        /// <summary>
        /// 外部からメニューを閉じてIdle状態に戻す。ノード生成後などに使用。
        /// </summary>
        public void CloseMenu()
        {
            if (_state == MenuState.Idle) return;
            _visualController?.CloseScroll();
            SetState(MenuState.Idle);
        }

        private void Update()
        {
            if (_visualController == null || _controllerInput == null || _leftHandRay == null)
                return;

            // 左手レイキャスト（毎フレーム1回）
            var leftRay = new Ray(_leftHandRay.LeftRayOrigin, _leftHandRay.LeftRayDirection);
            _leftHasHit = Physics.Raycast(leftRay, out _leftHit, LeftRayMaxDistance);

            // 位置＋リグ回転追従
            _visualController.UpdateWaistFollow(
                _controllerInput.HeadPosition,
                _leftHandRay.RigRotation);

            switch (_state)
            {
                case MenuState.Idle:
                    UpdateBarHighlight();
                    break;

                case MenuState.Dragging:
                    _visualController.ClearBarHighlight();
                    UpdateDrag();
                    break;

                case MenuState.Open:
                    _visualController.ClearBarHighlight();
                    if (_isDesktopMode)
                        ForwardRightHoverToScroll();
                    else
                        ForwardLeftHoverToScroll();
                    break;
            }
        }

        /// <summary>
        /// 状態遷移時にEdgeDragHandlerの有効/無効を連動させる。
        /// </summary>
        private void SetState(MenuState newState)
        {
            _state = newState;
            var isIdle = newState == MenuState.Idle;
            // メニューが非Idleの間はエッジ接続・切断・削除操作を無効化
            _edgeDragHandler?.SetEnabled(isIdle);
            _onMenuStateChanged?.Invoke(isIdle);
        }

        /// <summary>左トリガー: 状態に応じてドラッグ開始 / ボタンクリック / 閉じる。</summary>
        private void OnLeftTrigger(bool pressed)
        {
            switch (_state)
            {
                case MenuState.Idle:
                    if (pressed)
                    {
                        if (_isDesktopMode)
                            TryOpenDirect();
                        else
                            TryStartDrag();
                    }
                    break;

                case MenuState.Dragging:
                    if (!pressed)
                    {
                        if (_currentScrollHeight > MinDragThreshold)
                        {
                            _currentScrollHeight = 1f;
                            _visualController?.SetScrollHeight(1f);
                            SetState(MenuState.Open);
                        }
                        else
                        {
                            _visualController?.CloseScroll();
                            SetState(MenuState.Idle);
                        }
                    }
                    break;

                case MenuState.Open:
                    if (pressed)
                        HandleOpenPress();
                    else
                        HandleOpenRelease();
                    break;
            }
        }

        /// <summary>Xボタンでメニュー閉じ。</summary>
        private void OnCloseMenu()
        {
            if (_state != MenuState.Open && _state != MenuState.Dragging) return;

            _visualController?.CloseScroll();
            SetState(MenuState.Idle);
        }

        /// <summary>Open状態で左トリガー押下。</summary>
        private void HandleOpenPress()
        {
            if (_visualController == null) return;

            if (_leftHasHit)
            {
                // バーに当たっていれば別カテゴリに切替
                var cat = _visualController.GetCategoryFromCollider(_leftHit.collider);
                if (cat != null)
                {
                    _visualController.CloseScroll();
                    SetState(MenuState.Idle);
                    TryStartDrag();
                    return;
                }

                // スクロールパネルに当たっていればクリック
                if (_visualController.IsScrollCollider(_leftHit.collider))
                {
                    var bridge = _visualController.GetScrollRayBridge();
                    bridge?.NotifyPointerDown(_leftHit);
                    return;
                }
            }

            // 何にも当たっていない → メニュー閉じ
            _visualController.CloseScroll();
            SetState(MenuState.Idle);
        }

        /// <summary>Open状態で左トリガーリリース。</summary>
        private void HandleOpenRelease()
        {
            var bridge = _visualController?.GetScrollRayBridge();
            bridge?.NotifyPointerUp();
        }

        /// <summary>右トリガーでスクロールパネルをクリック。デスクトップモードではバー操作も統合。</summary>
        private void OnRightSelect(bool pressed)
        {
            if (_visualController == null || _sharedRaycast == null) return;

            // デスクトップモード: Idle時に右クリック（左クリック）でバーを開く
            if (_isDesktopMode && _state == MenuState.Idle && pressed)
            {
                if (_sharedRaycast.HasHit)
                {
                    var cat = _visualController.GetCategoryFromCollider(_sharedRaycast.CurrentHit.collider);
                    if (cat != null)
                    {
                        _visualController.OpenScroll(cat.Value);
                        _visualController.SetScrollHeight(1f);
                        SetState(MenuState.Open);
                        return;
                    }
                }
            }

            // デスクトップモード: Open時に右クリックで別バー切替 or 空クリックで閉じ
            if (_isDesktopMode && _state == MenuState.Open && pressed)
            {
                if (_sharedRaycast.HasHit)
                {
                    var cat = _visualController.GetCategoryFromCollider(_sharedRaycast.CurrentHit.collider);
                    if (cat != null)
                    {
                        _visualController.CloseScroll();
                        _visualController.OpenScroll(cat.Value);
                        _visualController.SetScrollHeight(1f);
                        return;
                    }
                }
            }

            if (_state != MenuState.Open) return;
            if (!_sharedRaycast.HasHit) return;

            var bridge = _visualController.GetScrollRayBridge();
            if (bridge == null) return;
            if (!_visualController.IsScrollCollider(_sharedRaycast.CurrentHit.collider)) return;

            if (pressed)
                bridge.NotifyPointerDown(_sharedRaycast.CurrentHit);
            else
                bridge.NotifyPointerUp();
        }

        /// <summary>デスクトップモード: Open時にマウスレイ（SharedRaycast）でスクロールパネルにホバーを転送。</summary>
        private void ForwardRightHoverToScroll()
        {
            if (_visualController == null || _sharedRaycast == null) return;

            var bridge = _visualController.GetScrollRayBridge();
            if (bridge == null) return;

            if (_sharedRaycast.HasHit && _visualController.IsScrollCollider(_sharedRaycast.CurrentHit.collider))
                bridge.NotifyHover(_sharedRaycast.CurrentHit);
            else
                bridge.NotifyHoverExit();
        }

        /// <summary>Open時に左手レイでスクロールパネルにホバーを転送。</summary>
        private void ForwardLeftHoverToScroll()
        {
            if (_visualController == null) return;

            var bridge = _visualController.GetScrollRayBridge();
            if (bridge == null) return;

            if (_leftHasHit && _visualController.IsScrollCollider(_leftHit.collider))
                bridge.NotifyHover(_leftHit);
            else
                bridge.NotifyHoverExit();
        }

        private void UpdateBarHighlight()
        {
            if (_visualController == null) return;

            if (_leftHasHit)
            {
                var cat = _visualController.GetCategoryFromCollider(_leftHit.collider);
                _visualController.SetBarHighlight(cat != null ? _leftHit.collider : null);
            }
            else
            {
                _visualController.ClearBarHighlight();
            }
        }

        private void TryStartDrag()
        {
            if (!_leftHasHit) return;
            if (_visualController == null || _leftHandRay == null) return;

            var category = _visualController.GetCategoryFromCollider(_leftHit.collider);
            if (category == null) return;

            _dragStartY = _leftHandRay.LeftRayOrigin.y;
            _currentScrollHeight = 0f;

            _visualController.OpenScroll(category.Value);
            SetState(MenuState.Dragging);
        }

        /// <summary>デスクトップモード用: バークリックで即座にスクロール全開＋Open状態。</summary>
        private void TryOpenDirect()
        {
            if (!_leftHasHit) return;
            if (_visualController == null) return;

            var category = _visualController.GetCategoryFromCollider(_leftHit.collider);
            if (category == null) return;

            _visualController.OpenScroll(category.Value);
            _visualController.SetScrollHeight(1f);
            SetState(MenuState.Open);
        }

        private void UpdateDrag()
        {
            if (_leftHandRay == null || _visualController == null) return;

            float deltaY = (_leftHandRay.LeftRayOrigin.y - _dragStartY) * DragSensitivity;
            _currentScrollHeight = Mathf.Clamp01(deltaY);

            _visualController.SetScrollHeight(_currentScrollHeight);
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
        }
    }
}
