#nullable enable

using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// XR Originの初期設定と検証を行う。
    /// シーン上のXRリグ構成が正しいことを確認する。
    /// </summary>
    /// <remarks>
    /// Phase 3 (2026-05-19): Mirror カメラから controller モデルを隠す責務は
    /// <c>MirrorHiddenScope</c> MonoBehaviour に委譲した (rightController / leftController
    /// GameObject に scene 上で attach する)。本クラスは validation のみに純化。
    /// </remarks>
    public class XRRigSetup : MonoBehaviour
    {
        [SerializeField] private Transform? cameraOffset;
        [SerializeField] private Transform? rightController;
        [SerializeField] private Transform? leftController;

        /// <summary>カメラオフセットTransformへのアクセス。</summary>
        public Transform? CameraOffset => cameraOffset;

        /// <summary>右コントローラーTransformへのアクセス。</summary>
        public Transform? RightController => rightController;

        /// <summary>左コントローラーTransformへのアクセス。</summary>
        public Transform? LeftController => leftController;

        private void Awake()
        {
            ValidateSetup();
        }

        private void ValidateSetup()
        {
            if (cameraOffset == null)
                Debug.LogWarning("[XRRigSetup] Camera Offset is not assigned.");
            if (rightController == null)
                Debug.LogWarning("[XRRigSetup] Right Controller is not assigned.");
            if (leftController == null)
                Debug.LogWarning("[XRRigSetup] Left Controller is not assigned.");
        }
    }
}
