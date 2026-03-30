#nullable enable

using System;
using R3;
using Rhizomode.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rhizomode.XR
{
    /// <summary>
    /// コントローラー入力を読み取り、R3 Observableとして公開する。
    /// IControllerInputを実装し、UI層への入力配信を担う。
    /// </summary>
    public class ControllerInputRouter : MonoBehaviour, IControllerInput
    {
        [SerializeField] private InputActionAsset? inputActions;
        [SerializeField] private Transform? headTransform;

        private InputAction? _selectAction;
        private InputAction? _grabAction;
        private InputAction? _deleteAction;
        private InputAction? _cutEdgeAction;
        private InputAction? _openMenuAction;
        private InputAction? _moveAction;
        private InputAction? _turnAction;

        private readonly Subject<Unit> _onOpenMenu = new();
        private readonly Subject<Unit> _onDeleteNode = new();
        private readonly Subject<Unit> _onCutEdge = new();
        private readonly Subject<bool> _onSelect = new();
        private readonly Subject<bool> _onGrab = new();
        private readonly Subject<Vector2> _onMove = new();
        private readonly Subject<Vector2> _onTurn = new();

        public Observable<Unit> OnOpenMenu => _onOpenMenu;
        public Observable<Unit> OnDeleteNode => _onDeleteNode;
        public Observable<Unit> OnCutEdge => _onCutEdge;
        public Observable<bool> OnSelect => _onSelect;
        public Observable<bool> OnGrab => _onGrab;

        /// <summary>移動入力（Left Stick）。Locomotion Providerへの入力に使用。</summary>
        public Observable<Vector2> OnMoveInput => _onMove;

        /// <summary>回転入力（Right Stick）。Snap Turn Providerへの入力に使用。</summary>
        public Observable<Vector2> OnTurnInput => _onTurn;

        public Vector3 HeadPosition => headTransform != null ? headTransform.position : Vector3.zero;
        public Quaternion HeadRotation => headTransform != null ? headTransform.rotation : Quaternion.identity;
        public Vector3 HeadForward => headTransform != null ? headTransform.forward : Vector3.forward;

        private void OnEnable()
        {
            if (inputActions == null) return;

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
            _onOpenMenu.Dispose();
            _onDeleteNode.Dispose();
            _onCutEdge.Dispose();
            _onSelect.Dispose();
            _onGrab.Dispose();
            _onMove.Dispose();
            _onTurn.Dispose();
        }

        private void BindActions()
        {
            var rightHand = inputActions!.FindActionMap("RightHand");
            var leftHand = inputActions!.FindActionMap("LeftHand");

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
                _moveAction = leftHand.FindAction("Move");
            }
        }

        private void EnableActions()
        {
            inputActions!.Enable();
        }

        private void DisableActions()
        {
            inputActions?.Disable();
        }

        private void SubscribeActions()
        {
            SubscribeButton(_openMenuAction, _onOpenMenu);
            SubscribeButton(_deleteAction, _onDeleteNode);
            SubscribeButton(_cutEdgeAction, _onCutEdge);
            SubscribeToggle(_selectAction, _onSelect);
            SubscribeToggle(_grabAction, _onGrab);
            SubscribeAxis(_moveAction, _onMove);
            SubscribeAxis(_turnAction, _onTurn);
        }

        private static void SubscribeButton(InputAction? action, Subject<Unit> subject)
        {
            if (action == null) return;
            action.performed += _ => subject.OnNext(Unit.Default);
        }

        private static void SubscribeToggle(InputAction? action, Subject<bool> subject)
        {
            if (action == null) return;
            action.performed += _ => subject.OnNext(true);
            action.canceled += _ => subject.OnNext(false);
        }

        private static void SubscribeAxis(InputAction? action, Subject<Vector2> subject)
        {
            if (action == null) return;
            action.performed += ctx => subject.OnNext(ctx.ReadValue<Vector2>());
            action.canceled += _ => subject.OnNext(Vector2.zero);
        }
    }
}
