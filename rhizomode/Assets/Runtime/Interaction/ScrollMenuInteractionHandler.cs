#nullable enable

using System;
using R3;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// 巻物メニューのインタラクション制御。左右どちらの手からでも操作可能。
    /// Idle: 左右いずれかのレイ→バーハイライト。該当ハンドのトリガー on バー → Dragging。
    /// Dragging: 開始ハンドのレイ Y で展開量を駆動。同ハンドのトリガー離し → Open / 閉じ。
    /// Open: いずれかのハンドのトリガー on スクロールボタン → ノード生成。
    ///        いずれかのハンドのトリガー on バー → 別カテゴリに切替。
    ///        いずれかのハンドのトリガー on 空 → メニュー閉じ。
    /// </summary>
    public class ScrollMenuInteractionHandler : MonoBehaviour
    {
        private enum MenuState
        {
            Idle,
            Dragging,
            Open
        }

        private enum Hand
        {
            Left,
            Right
        }

        private const float DragSensitivity = 3f;
        private const float MinDragThreshold = 0.01f;
        private const float LeftRayMaxDistance = 2f;

        private ScrollMenuVisualController? _visualController;
        private IControllerInput? _controllerInput;
        private IRayProvider? _rightRayProvider;
        private ILeftHandRay? _leftHandRay;
        private SharedRaycastService? _sharedRaycast;
        private EdgeDragHandler? _edgeDragHandler;
        private Action<bool>? _onMenuStateChanged;

        private MenuState _state = MenuState.Idle;
        private Hand _dragHand;
        private float _dragStartY;
        private float _currentScrollHeight;
        private bool _isDesktopMode;

        // 左手レイキャスト結果（毎フレーム更新）
        private bool _leftHasHit;
        private RaycastHit _leftHit;

        // 右手レイキャスト結果（SharedRaycastService 経由・VR/Desktop 共用）
        private bool _rightHasHit;
        private RaycastHit _rightHit;

        private IDisposable? _subscriptions;

        /// <summary>
        /// 初期化。入力とビジュアルコントローラーを接続する。
        /// </summary>
        public void Initialize(
            IControllerInput controllerInput,
            IRayProvider rightRayProvider,
            ILeftHandRay leftHandRay,
            ILeftHandInput leftHandInput,
            SharedRaycastService sharedRaycast,
            ScrollMenuVisualController visualController)
        {
            _controllerInput = controllerInput;
            _rightRayProvider = rightRayProvider;
            _leftHandRay = leftHandRay;
            _sharedRaycast = sharedRaycast;
            _visualController = visualController;

            var d = Disposable.CreateBuilder();

            // Xボタン → メニュー閉じ（Open/Dragging時）
            controllerInput.OnOpenMenu
                .Subscribe(_ => OnCloseMenu())
                .AddTo(ref d);

            // 右トリガー → 右ハンド扱い
            controllerInput.OnSelect
                .Subscribe(pressed => HandleTrigger(pressed, Hand.Right))
                .AddTo(ref d);

            // 左トリガー → 左ハンド扱い
            leftHandInput.OnLeftSelect
                .Subscribe(pressed => HandleTrigger(pressed, Hand.Left))
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

            // 右手レイキャスト結果（SharedRaycastService が右ray／desktopではマウスrayを毎フレーム実行済）
            if (_sharedRaycast != null)
            {
                _rightHasHit = _sharedRaycast.HasHit;
                _rightHit = _sharedRaycast.CurrentHit;
            }
            else
            {
                _rightHasHit = false;
            }

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
                    ForwardHoverToScroll();
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

        /// <summary>左右いずれのトリガーも同じパイプラインで処理する。</summary>
        private void HandleTrigger(bool pressed, Hand hand)
        {
            switch (_state)
            {
                case MenuState.Idle:
                    if (pressed)
                    {
                        if (_isDesktopMode)
                            TryOpenDirect(hand);
                        else
                            TryStartDrag(hand);
                    }
                    break;

                case MenuState.Dragging:
                    // ドラッグを開始した手の離しのみ確定。逆手のトリガーは無視。
                    if (!pressed && hand == _dragHand)
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
                        HandleOpenPress(hand);
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

        /// <summary>Open状態でトリガー押下。押下した手のレイで判定。</summary>
        private void HandleOpenPress(Hand hand)
        {
            if (_visualController == null) return;
            if (!TryGetHandHit(hand, out var hit))
            {
                // 何にも当たっていない → メニュー閉じ
                _visualController.CloseScroll();
                SetState(MenuState.Idle);
                return;
            }

            // バーに当たっていれば別カテゴリに切替
            var cat = _visualController.GetCategoryFromCollider(hit.collider);
            if (cat != null)
            {
                _visualController.CloseScroll();
                SetState(MenuState.Idle);
                if (_isDesktopMode)
                    TryOpenDirect(hand);
                else
                    TryStartDrag(hand);
                return;
            }

            // スクロールパネルに当たっていればクリック
            if (_visualController.IsScrollCollider(hit.collider))
            {
                var bridge = _visualController.GetScrollRayBridge();
                bridge?.NotifyPointerDown(hit);
                return;
            }

            // ノード以外・スクロール以外 → メニュー閉じ
            _visualController.CloseScroll();
            SetState(MenuState.Idle);
        }

        /// <summary>Open状態でトリガーリリース。左右どちらでも UI に release を通知する。</summary>
        private void HandleOpenRelease()
        {
            var bridge = _visualController?.GetScrollRayBridge();
            bridge?.NotifyPointerUp();
        }

        /// <summary>Open時に左右レイをスクロールパネルへのホバー通知に橋渡しする。左手優先。</summary>
        private void ForwardHoverToScroll()
        {
            if (_visualController == null) return;

            var bridge = _visualController.GetScrollRayBridge();
            if (bridge == null) return;

            if (_leftHasHit && _visualController.IsScrollCollider(_leftHit.collider))
            {
                bridge.NotifyHover(_leftHit);
                return;
            }
            if (_rightHasHit && _visualController.IsScrollCollider(_rightHit.collider))
            {
                bridge.NotifyHover(_rightHit);
                return;
            }
            bridge.NotifyHoverExit();
        }

        /// <summary>Idle時のバーハイライト。左手レイ優先、なければ右手レイ。</summary>
        private void UpdateBarHighlight()
        {
            if (_visualController == null) return;

            if (_leftHasHit)
            {
                var cat = _visualController.GetCategoryFromCollider(_leftHit.collider);
                if (cat != null)
                {
                    _visualController.SetBarHighlight(_leftHit.collider);
                    return;
                }
            }
            if (_rightHasHit)
            {
                var cat = _visualController.GetCategoryFromCollider(_rightHit.collider);
                if (cat != null)
                {
                    _visualController.SetBarHighlight(_rightHit.collider);
                    return;
                }
            }
            _visualController.ClearBarHighlight();
        }

        private void TryStartDrag(Hand hand)
        {
            if (!TryGetHandHit(hand, out var hit)) return;
            if (_visualController == null) return;

            var category = _visualController.GetCategoryFromCollider(hit.collider);
            if (category == null) return;

            _dragHand = hand;
            _dragStartY = GetHandRayOriginY(hand);
            _currentScrollHeight = 0f;

            _visualController.OpenScroll(category.Value);
            SetState(MenuState.Dragging);
        }

        /// <summary>デスクトップモード用: バークリックで即座にスクロール全開＋Open状態。</summary>
        private void TryOpenDirect(Hand hand)
        {
            if (!TryGetHandHit(hand, out var hit)) return;
            if (_visualController == null) return;

            var category = _visualController.GetCategoryFromCollider(hit.collider);
            if (category == null) return;

            _visualController.OpenScroll(category.Value);
            _visualController.SetScrollHeight(1f);
            SetState(MenuState.Open);
        }

        private void UpdateDrag()
        {
            if (_visualController == null) return;

            float deltaY = (GetHandRayOriginY(_dragHand) - _dragStartY) * DragSensitivity;
            _currentScrollHeight = Mathf.Clamp01(deltaY);

            _visualController.SetScrollHeight(_currentScrollHeight);
        }

        private bool TryGetHandHit(Hand hand, out RaycastHit hit)
        {
            if (hand == Hand.Left)
            {
                hit = _leftHit;
                return _leftHasHit;
            }
            hit = _rightHit;
            return _rightHasHit;
        }

        private float GetHandRayOriginY(Hand hand)
        {
            if (hand == Hand.Left)
                return _leftHandRay != null ? _leftHandRay.LeftRayOrigin.y : 0f;
            return _rightRayProvider != null ? _rightRayProvider.RayOrigin.y : 0f;
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
        }
    }
}
