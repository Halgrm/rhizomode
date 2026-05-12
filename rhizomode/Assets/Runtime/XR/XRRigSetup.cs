#nullable enable

using UnityEngine;

namespace Rhizomode.XR
{
    /// <summary>
    /// XR Originの初期設定と検証を行う。
    /// シーン上のXRリグ構成が正しいことを確認する。
    /// </summary>
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
