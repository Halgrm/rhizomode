#nullable enable

using System;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.Input.XR
{
    /// <summary>
    /// コントローラー入力を読み取り、R3 Observableとして公開する。
    /// IControllerInputを実装し、UI層への入力配信を担う。
    /// </summary>
    public class ControllerInputRouter : MonoBehaviour, IControllerInput, IControllerPose, ILeftHandRay, ILeftHandInput, IPlayerMoveInput
    {
        [SerializeField] private InputActionAsset? inputActions;
        [SerializeField] private Transform? headTransform;
        [SerializeField] private Transform? rightControllerTransform;
        [SerializeField] private Transform? leftControllerTransform;

        private InputActionAsset? _originalActions;
        private InputActionAsset? _clonedActions;

        private InputAction? _selectAction;
        private InputAction? _grabAction;
        private InputAction? _deleteAction;
        private InputAction? _cutEdgeAction;
        private InputAction? _openMenuAction;
        private InputAction? _closeMenuAction;
        private InputAction? _moveAction;
        private InputAction? _turnAction;
        private InputAction? _leftSelectAction;
        private InputAction? _leftGrabAction;

        private readonly Subject<Unit> _onOpenMenu = new();
        private readonly Subject<bool> _onMenuHold = new();
        private readonly Subject<Unit> _onCloseMenu = new();
        private readonly Subject<Unit> _onDeleteNode = new();
        private readonly Subject<Unit> _onCutEdge = new();
        private readonly Subject<bool> _onSelect = new();
        private readonly Subject<bool> _onGrab = new();
        private readonly Subject<bool> _onLeftSelect = new();
        private readonly Subject<bool> _onLeftGrab = new();
        private readonly Subject<Vector2> _onMove = new();
        private readonly Subject<Vector2> _onTurn = new();
        private bool _isDisposed;

        public Observable<Unit> OnOpenMenu => _onOpenMenu;
        public Observable<bool> OnMenuHold => _onMenuHold;
        public Observable<Unit> OnCloseMenu => _onCloseMenu;
        public Observable<Unit> OnDeleteNode => _onDeleteNode;
        public Observable<Unit> OnCutEdge => _onCutEdge;
        public Observable<bool> OnSelect => _onSelect;
        public Observable<bool> OnGrab => _onGrab;

        // ILeftHandInput
        public Observable<bool> OnLeftSelect => _onLeftSelect;
        public Observable<bool> OnLeftGrab => _onLeftGrab;

        /// <summary>移動入力（Left Stick）。Locomotion Providerへの入力に使用。</summary>
        public Observable<Vector2> OnMoveInput => _onMove;

        /// <summary>回転入力（Right Stick）。Snap Turn Providerへの入力に使用。</summary>
        public Observable<Vector2> OnTurnInput => _onTurn;

        /// <summary>VRヘッドのTransform参照。MirrorOutputController等で使用。</summary>
        public Transform? HeadTransform => headTransform;

        public Vector3 HeadPosition => headTransform != null ? headTransform.position : Vector3.zero;
        public Quaternion HeadRotation => headTransform != null ? headTransform.rotation : Quaternion.identity;
        public Vector3 HeadForward => headTransform != null ? headTransform.forward : Vector3.forward;

        // IRayProvider + IControllerPose (右手)
        public Vector3 RayOrigin => rightControllerTransform != null ? rightControllerTransform.position : Vector3.zero;
        public Vector3 RayDirection => rightControllerTransform != null ? rightControllerTransform.forward : Vector3.forward;
        public Quaternion ControllerRotation => rightControllerTransform != null ? rightControllerTransform.rotation : Quaternion.identity;

        // ILeftHandRay (左手)
        public Vector3 LeftRayOrigin => leftControllerTransform != null ? leftControllerTransform.position : Vector3.zero;
        public Vector3 LeftRayDirection => leftControllerTransform != null ? leftControllerTransform.forward : Vector3.forward;

        /// <summary>XR Rigの回転（スナップターン・リセンターのみ変化）。</summary>
        public Quaternion RigRotation => transform.rotation;

        private void OnEnable()
        {
            if (inputActions == null)
            {
                Debug.LogError("[ControllerInputRouter] inputActions is null!");
                return;
            }

            // オリジナルのSerializeField参照を保持
            if (_originalActions == null)
                _originalActions = inputActions;

            // 既存クローンを破棄してから新規クローン
            if (_clonedActions != null)
                Destroy(_clonedActions);

            _clonedActions = Instantiate(_originalActions);

            BindActions();
            EnableActions();
            SubscribeActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        private void OnDestroy()
        {
            _isDisposed = true;

            _onOpenMenu.Dispose();
            _onMenuHold.Dispose();
            _onCloseMenu.Dispose();
            _onDeleteNode.Dispose();
            _onCutEdge.Dispose();
            _onSelect.Dispose();
            _onGrab.Dispose();
            _onLeftSelect.Dispose();
            _onLeftGrab.Dispose();
            _onMove.Dispose();
            _onTurn.Dispose();

            // クローンしたInputActionAssetを破棄（メモリリーク防止）
            if (_clonedActions != null)
            {
                Destroy(_clonedActions);
                _clonedActions = null;
            }
        }

        private void BindActions()
        {
            var rightHand = _clonedActions!.FindActionMap("RightHand");
            var leftHand = _clonedActions!.FindActionMap("LeftHand");

            if (rightHand != null)
            {
                _selectAction = rightHand.FindAction("Select");
                _grabAction = rightHand.FindAction("Grab");
                _deleteAction = rightHand.FindAction("DeleteNode");
                _cutEdgeAction = rightHand.FindAction("CutEdge");
                _turnAction = rightHand.FindAction("Turn");
            }

            if (leftHand != null)
            {
                _openMenuAction = leftHand.FindAction("OpenMenu");
                _closeMenuAction = leftHand.FindAction("CloseMenu");
                _moveAction = leftHand.FindAction("Move");
                _leftSelectAction = leftHand.FindAction("LeftSelect");
                _leftGrabAction = leftHand.FindAction("LeftGrab");
            }

            if (_closeMenuAction == null)
                Debug.LogWarning("[ControllerInputRouter] CloseMenu action not found in LeftHand map!");
        }

        private void EnableActions()
        {
            _clonedActions!.Enable();
        }

        private void DisableActions()
        {
            _clonedActions?.Disable();
        }

        private void SubscribeActions()
        {
            SubscribeButton(_openMenuAction, _onOpenMenu);
            SubscribeToggle(_openMenuAction, _onMenuHold);
            SubscribeButton(_closeMenuAction, _onCloseMenu);
            SubscribeButton(_deleteAction, _onDeleteNode);
            SubscribeButton(_cutEdgeAction, _onCutEdge);
            SubscribeToggle(_selectAction, _onSelect);
            SubscribeToggle(_grabAction, _onGrab);
            SubscribeToggle(_leftSelectAction, _onLeftSelect);
            SubscribeToggle(_leftGrabAction, _onLeftGrab);
            SubscribeAxis(_moveAction, _onMove);
            SubscribeAxis(_turnAction, _onTurn);
        }

        // Dispose後のSubject.OnNextは例外を投げるため、フラグで保護
        private void SubscribeButton(InputAction? action, Subject<Unit> subject)
        {
            if (action == null) return;
            action.performed += _ => { if (!_isDisposed) subject.OnNext(Unit.Default); };
        }

        private void SubscribeToggle(InputAction? action, Subject<bool> subject)
        {
            if (action == null) return;
            action.performed += _ => { if (!_isDisposed) subject.OnNext(true); };
            action.canceled += _ => { if (!_isDisposed) subject.OnNext(false); };
        }

        private void SubscribeAxis(InputAction? action, Subject<Vector2> subject)
        {
            if (action == null) return;
            action.performed += ctx => { if (!_isDisposed) subject.OnNext(ctx.ReadValue<Vector2>()); };
            action.canceled += _ => { if (!_isDisposed) subject.OnNext(Vector2.zero); };
        }
    }
}
