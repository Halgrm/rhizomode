#nullable enable

using Unity.Cinemachine;
using UnityEngine;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// カメラの移動速度に応じて FOV を変化させる Cinemachine 拡張。
    /// Rector の同名コンポーネントを移植。速いほど FOV を広げることで、
    /// ドリーやワンダー系カメラの疾走感を強調する。
    /// </summary>
    /// <remarks>
    /// パイプライン最終段 (Finalize) で前フレームからの位置差分を速度として測り、
    /// minVelocity..maxVelocity を 0..1 に正規化、curve を通して minFov..maxFov を補間する。
    /// Finalize 段で Lens を上書きするため、本拡張が付くカメラでは FOV スライダー操作は
    /// 毎フレーム上書きされる (Rector と同じ挙動)。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CinemachineVelocityFov : CinemachineExtension
    {
        [SerializeField, Tooltip("最低速度時の FOV")]
        private float minFov = 60f;

        [SerializeField, Tooltip("最高速度時の FOV")]
        private float maxFov = 120f;

        [SerializeField, Tooltip("FOV 補間を始める速度 (m/s)")]
        private float minVelocity;

        [SerializeField, Tooltip("FOV が maxFov に達する速度 (m/s)")]
        private float maxVelocity = 10f;

        [SerializeField, Tooltip("正規化速度 (0..1) → FOV 補間係数のカーブ")]
        private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private Vector3 _lastPosition;
        private bool _hasLastPosition;

        /// <summary>最低速度時の FOV。CameraManagerPanel から調整する。</summary>
        public float MinFov { get => minFov; set => minFov = value; }

        /// <summary>最高速度時の FOV。</summary>
        public float MaxFov { get => maxFov; set => maxFov = value; }

        /// <summary>FOV 補間を始める速度 (m/s)。</summary>
        public float MinVelocity { get => minVelocity; set => minVelocity = value; }

        /// <summary>FOV が maxFov に達する速度 (m/s)。</summary>
        public float MaxVelocity { get => maxVelocity; set => maxVelocity = value; }

        /// <inheritdoc />
        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
            ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;

            // 初回フレーム (基準位置未確定) と deltaTime<=0 (一時停止 / 0 除算) は速度を
            // 算出せず基準位置だけ更新する。初回に _lastPosition=(0,0,0) のまま速度を測ると
            // カメラがライブ化した瞬間に FOV が 1 フレームだけ跳ねるため、それを防ぐ。
            if (deltaTime <= 0f || !_hasLastPosition)
            {
                _lastPosition = state.RawPosition;
                _hasLastPosition = true;
                return;
            }

            var velocity = (state.RawPosition - _lastPosition) / deltaTime;
            _lastPosition = state.RawPosition;

            var t = Mathf.InverseLerp(minVelocity, maxVelocity, velocity.magnitude);
            var lens = state.Lens;
            lens.FieldOfView = Mathf.Lerp(minFov, maxFov, curve.Evaluate(t));
            state.Lens = lens;
        }
    }
}
