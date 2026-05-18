#nullable enable

using System.Collections;
using Rhizomode.Cameras;
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

        // XR Interaction Toolkit が controller model を遅延スポーンするケース (Action-based controller の
        // Model Prefab 等) に備え、Start 後にも 1〜2 回 layer を再適用する。
        private const float DeferredApplyDelaySec = 0.5f;
        private const int DeferredApplyAttempts = 3;

        private void Awake()
        {
            ValidateSetup();
            ApplyPerformerUILayer();
        }

        private void Start()
        {
            // Start で再適用 (Awake 後にスポーンした子も拾う)。さらに数回の遅延 retry を回す。
            ApplyPerformerUILayer();
            StartCoroutine(DeferredApplyRoutine());
        }

        private IEnumerator DeferredApplyRoutine()
        {
            for (int i = 0; i < DeferredApplyAttempts; i++)
            {
                yield return new WaitForSeconds(DeferredApplyDelaySec);
                ApplyPerformerUILayer();
            }
        }

        /// <summary>
        /// 左右コントローラーの子孫を PerformerUI layer に揃える。Mirror カメラから controller
        /// モデルやレイビジュアライザーを隠すため。HMD カメラ側は <c>cameraOffset</c> を意図的に
        /// 触らない (camera 自体は何もレンダリングしないし、children に映したい requisite mesh が
        /// あった場合の事故を避ける)。
        /// </summary>
        private void ApplyPerformerUILayer()
        {
            if (rightController != null) PerformerUILayer.ApplyRecursive(rightController.gameObject);
            if (leftController != null) PerformerUILayer.ApplyRecursive(leftController.gameObject);
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
