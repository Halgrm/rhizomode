#nullable enable

using R3;
using UnityEngine;
using UnityEngine.InputSystem;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.Input.Desktop
{
    /// <summary>
    /// デスクトップデバッグ用の入力ルーター。マウス+キーボードでVRコントローラー入力をエミュレートする。
    /// VRヘッドセットなしでノードグラフ操作をデバッグできる。
    /// </summary>
    public class DesktopInputRouter : MonoBehaviour, IControllerInput, IControllerPose, ILeftHandRay, ILeftHandInput
    {
        [Header("Camera")]
        [SerializeField] private Camera? debugCamera;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float scrollSpeed = 5f;

        // マウス delta (px) → 回転角 (度) のスケール係数。デスクトップデバッグ専用のため
        // ライブ調整不要 (const)。lookSensitivity と乗算して最終感度を決める。
        private const float MouseLookDegreesPerPixel = 0.1f;
        // スクロール値 → 前後移動距離 (m) のスケール係数。scrollSpeed と乗算する。
        private const float ScrollMovePerUnit = 0.001f;

        // R3 Subjects
        private readonly Subject<Unit> _onOpenMenu = new();
        private readonly Subject<bool> _onMenuHold = new();
        private readonly Subject<Unit> _onCloseMenu = new();
        private readonly Subject<Unit> _onDeleteNode = new();
        private readonly Subject<Unit> _onCutEdge = new();
        private readonly Subject<bool> _onSelect = new();
        private readonly Subject<bool> _onGrab = new();
        private readonly Subject<bool> _onLeftSelect = new();
        private readonly Subject<bool> _onLeftGrab = new();
        private readonly Subject<Vector2> _onTurn = new();

        private bool _isDisposed;

        // カメラ操作状態
        private float _yaw;
        private float _pitch;

        // 前フレームのボタン状態
        private bool _prevSelect;
        private bool _prevGrab;
        private bool _prevLeftSelect;
        private bool _prevLeftGrab;

        // レイ情報キャッシュ
        private Vector3 _rayOrigin;
        private Vector3 _rayDirection;

        // IControllerInput
        public Observable<Unit> OnOpenMenu => _onOpenMenu;
        public Observable<bool> OnMenuHold => _onMenuHold;
        public Observable<Unit> OnCloseMenu => _onCloseMenu;
        public Observable<Unit> OnDeleteNode => _onDeleteNode;
        public Observable<Unit> OnCutEdge => _onCutEdge;
        public Observable<bool> OnSelect => _onSelect;
        public Observable<bool> OnGrab => _onGrab;

        public Vector3 HeadPosition => debugCamera != null ? debugCamera.transform.position : Vector3.zero;
        public Quaternion HeadRotation => debugCamera != null ? debugCamera.transform.rotation : Quaternion.identity;
        public Vector3 HeadForward => debugCamera != null ? debugCamera.transform.forward : Vector3.forward;

        /// <summary>VRヘッドのTransform参照互換。デスクトップカメラを返す。</summary>
        public Transform? HeadTransform => debugCamera != null ? debugCamera.transform : null;

        // IControllerPose / IRayProvider
        public Vector3 RayOrigin => _rayOrigin;
        public Vector3 RayDirection => _rayDirection;
        public Quaternion ControllerRotation => debugCamera != null ? debugCamera.transform.rotation : Quaternion.identity;

        // ILeftHandRay（デスクトップでは左右同じレイ）
        public Vector3 LeftRayOrigin => _rayOrigin;
        public Vector3 LeftRayDirection => _rayDirection;
        public Quaternion RigRotation => Quaternion.identity;

        // ILeftHandInput
        public Observable<bool> OnLeftSelect => _onLeftSelect;
        public Observable<bool> OnLeftGrab => _onLeftGrab;

        /// <summary>スティック入力互換。Object3DGrabHandler等で使用。</summary>
        public Observable<Vector2> OnTurnInput => _onTurn;

        private void Start()
        {
            if (debugCamera == null)
                debugCamera = GetComponent<Camera>();

            if (debugCamera != null)
            {
                var euler = debugCamera.transform.eulerAngles;
                _yaw = euler.y;
                _pitch = euler.x;
            }
        }

        private void Update()
        {
            if (_isDisposed || debugCamera == null) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            UpdateCameraMovement(keyboard, mouse);
            UpdateRay(mouse);
            UpdateButtonInputs(keyboard, mouse);
        }

        private void UpdateCameraMovement(Keyboard kb, Mouse mouse)
        {
            if (debugCamera == null) return;

            var t = debugCamera.transform;

            // マウス中ボタンドラッグで視点回転
            if (mouse.middleButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                _yaw += delta.x * lookSensitivity * MouseLookDegreesPerPixel;
                _pitch -= delta.y * lookSensitivity * MouseLookDegreesPerPixel;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            }

            t.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // WASD移動
            var move = Vector3.zero;
            if (kb.wKey.isPressed) move += t.forward;
            if (kb.sKey.isPressed) move -= t.forward;
            if (kb.aKey.isPressed) move -= t.right;
            if (kb.dKey.isPressed) move += t.right;
            if (kb.eKey.isPressed) move += Vector3.up;
            if (kb.qKey.isPressed) move -= Vector3.up;

            if (move.sqrMagnitude > 0.001f)
                t.position += move.normalized * (moveSpeed * Time.deltaTime);

            // スクロールホイールで前後移動
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                t.position += t.forward * (scroll * scrollSpeed * ScrollMovePerUnit);
        }

        private void UpdateRay(Mouse mouse)
        {
            if (debugCamera == null) return;

            var mousePos = mouse.position.ReadValue();
            var ray = debugCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
            _rayOrigin = ray.origin;
            _rayDirection = ray.direction;
        }

        private void UpdateButtonInputs(Keyboard kb, Mouse mouse)
        {
            var isCtrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            var isAlt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;

            // Select: マウス左クリック（Ctrl/Alt未押下時のみ）
            var selectNow = mouse.leftButton.isPressed && !isCtrl && !isAlt;
            if (selectNow != _prevSelect)
            {
                _onSelect.OnNext(selectNow);
                _prevSelect = selectNow;
            }

            // Grab: マウス右クリック
            var grabNow = mouse.rightButton.isPressed;
            if (grabNow != _prevGrab)
            {
                _onGrab.OnNext(grabNow);
                _prevGrab = grabNow;
            }

            // LeftSelect: Ctrl + 左クリック
            var leftSelectNow = mouse.leftButton.isPressed && isCtrl;
            if (leftSelectNow != _prevLeftSelect)
            {
                _onLeftSelect.OnNext(leftSelectNow);
                _prevLeftSelect = leftSelectNow;
            }

            // LeftGrab: Alt + 左クリック
            var leftGrabNow = mouse.leftButton.isPressed && isAlt;
            if (leftGrabNow != _prevLeftGrab)
            {
                _onLeftGrab.OnNext(leftGrabNow);
                _prevLeftGrab = leftGrabNow;
            }

            // OpenMenu: X キー
            if (kb.xKey.wasPressedThisFrame)
            {
                _onOpenMenu.OnNext(Unit.Default);
                _onMenuHold.OnNext(true);
            }
            if (kb.xKey.wasReleasedThisFrame)
                _onMenuHold.OnNext(false);

            // CloseMenu: Y キー
            if (kb.yKey.wasPressedThisFrame)
                _onCloseMenu.OnNext(Unit.Default);

            // DeleteNode: Delete キー
            if (kb.deleteKey.wasPressedThisFrame)
                _onDeleteNode.OnNext(Unit.Default);

            // CutEdge: Backspace キー
            if (kb.backspaceKey.wasPressedThisFrame)
                _onCutEdge.OnNext(Unit.Default);

            // Turn入力（スケール変更用）: 上下矢印キー
            var turnY = 0f;
            if (kb.upArrowKey.isPressed) turnY = 1f;
            else if (kb.downArrowKey.isPressed) turnY = -1f;
            _onTurn.OnNext(new Vector2(0f, turnY));
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
            _onTurn.Dispose();
        }
    }
}
