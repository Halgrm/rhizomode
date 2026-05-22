#nullable enable

using Unity.Cinemachine;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// LookAt ターゲット周辺のランダムな球面内点へ漂うカメラ Body コンポーネント。
    /// Rector の同名コンポーネントを移植。period 秒ごとに新しい目標オフセットを抽選し、
    /// speed で現在位置から補間追従することで、有機的な手放しカメラの揺れを生む。
    /// </summary>
    [DisallowMultipleComponent]
    [CameraPipeline(CinemachineCore.Stage.Body)]
    [RequiredTarget(RequiredTargetAttribute.RequiredTargets.LookAt)]
    public sealed class CinemachineRandomSphereTarget : CinemachineComponentBase
    {
        [SerializeField, Tooltip("目標点への補間速度")]
        private float speed = 0.2f;

        [SerializeField, Tooltip("ランダム抽選する球の半径 (LookAt からの最大ずれ)")]
        private float radius = 1f;

        [SerializeField, Range(0f, 4f), Tooltip("目標点を更新する周期 (秒)")]
        private float period = 1f;

        private float _elapsed;
        private Vector3 _offset;

        /// <summary>目標点への補間速度。CameraManagerPanel から調整する。</summary>
        public float Speed { get => speed; set => speed = value; }

        /// <summary>ランダム抽選する球の半径 (LookAt からの最大ずれ)。</summary>
        public float Radius { get => radius; set => radius = value; }

        /// <summary>目標点を更新する周期 (秒)。</summary>
        public float Period { get => period; set => period = value; }

        public override bool IsValid => LookAtTarget != null;

        public override CinemachineCore.Stage Stage => CinemachineCore.Stage.Body;

        public override void MutateCameraState(ref CameraState curState, float deltaTime)
        {
#if UNITY_EDITOR
            // エディタの非再生中はカメラを漂わせない (シーン編集の妨げ防止)
            if (!Application.isPlaying) return;
#endif
            var target = LookAtTarget;
            if (target == null || !curState.HasLookAt()) return;

            _elapsed += Time.deltaTime;
            if (_elapsed > period)
            {
                _elapsed -= period;
                _offset = Random.insideUnitSphere * radius;
            }

            curState.RawPosition = Vector3.Lerp(
                curState.GetCorrectedPosition(),
                target.position + _offset,
                Time.deltaTime * speed);
        }
    }
}
