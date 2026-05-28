#nullable enable

using UnityEngine;

namespace Rhizomode.UI.Presentation.Character
{
    /// <summary>
    /// Drives a Unity Humanoid avatar (e.g. a VRChat-style rig) from the VR rig:
    /// the avatar's head tracks the HMD and the hands track the controllers via
    /// Animator humanoid IK.
    /// </summary>
    /// <remarks>
    /// <para>VRChat avatars import as Mecanim Humanoid, so we use
    /// <see cref="Animator.SetIKPosition"/> / <see cref="Animator.SetIKRotation"/>
    /// rather than hard-overwriting bone transforms. That keeps the elbow/shoulder
    /// chain solved instead of detaching the hand from the arm.</para>
    ///
    /// <para>Head alignment: each LateUpdate we shift the whole avatar root so the
    /// head bone sits at the HMD, then copy the HMD rotation onto the head bone.
    /// This is the simplified "VR IK" approach (root follow + head match + hand IK)
    /// — good enough for a VJ avatar without a full FinalIK / VRIK solver.</para>
    ///
    /// <para>Requires the Animator to have a runtimeAnimatorController whose layer 0
    /// has "IK Pass" enabled, otherwise <see cref="OnAnimatorIK"/> never fires.</para>
    /// </remarks>
    [RequireComponent(typeof(Animator))]
    public sealed class VrHumanoidBinder : MonoBehaviour
    {
        [Header("XR Source")]
        [SerializeField] private Transform? hmd;
        [SerializeField] private Transform? leftController;
        [SerializeField] private Transform? rightController;

        [Header("Tuning")]
        [Tooltip("Vertical offset applied to the head match (m). Negative lowers the avatar.")]
        [SerializeField] private float headHeightOffset = 0f;
        [Tooltip("Local rotation offset applied to each hand IK goal (controller → palm alignment).")]
        [SerializeField] private Vector3 handRotationOffsetEuler = Vector3.zero;
        [Tooltip("How strongly hands snap to the controllers (0..1).")]
        [SerializeField, Range(0f, 1f)] private float handWeight = 1f;

        private Animator? _animator;
        private Transform? _headBone;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator != null && _animator.isHuman)
            {
                _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        // Root follow + head match runs after the Animator has posed the body.
        private void LateUpdate()
        {
            if (_animator == null || hmd == null || _headBone == null)
            {
                return;
            }

            // Shift the avatar so its head bone coincides with the HMD position.
            Vector3 headTarget = hmd.position + Vector3.up * headHeightOffset;
            Vector3 delta = headTarget - _headBone.position;
            transform.position += delta;

            // Match head orientation to the HMD (yaw/pitch/roll of the headset).
            _headBone.rotation = hmd.rotation;
        }

        // Hand IK goals are pushed in the Animator IK pass (requires IK Pass layer flag).
        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || !_animator.isHuman)
            {
                return;
            }

            Quaternion handOffset = Quaternion.Euler(handRotationOffsetEuler);
            ApplyHandGoal(AvatarIKGoal.LeftHand, leftController, handOffset);
            ApplyHandGoal(AvatarIKGoal.RightHand, rightController, handOffset);
        }

        private void ApplyHandGoal(AvatarIKGoal goal, Transform? controller, Quaternion handOffset)
        {
            if (_animator == null)
            {
                return;
            }

            if (controller == null)
            {
                _animator.SetIKPositionWeight(goal, 0f);
                _animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            _animator.SetIKPositionWeight(goal, handWeight);
            _animator.SetIKRotationWeight(goal, handWeight);
            _animator.SetIKPosition(goal, controller.position);
            _animator.SetIKRotation(goal, controller.rotation * handOffset);
        }
    }
}
