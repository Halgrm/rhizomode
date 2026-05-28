#nullable enable

using UnityEngine;

namespace Rhizomode.UI.Presentation.Character
{
    public sealed class VrCharacterBinder : MonoBehaviour
    {
        [SerializeField] private Transform? xrOrigin;
        [SerializeField] private Transform? hmd;
        [SerializeField] private Transform? leftController;
        [SerializeField] private Transform? rightController;
        [SerializeField] private Transform? rootBone;
        [SerializeField] private Transform? headBone;
        [SerializeField] private Transform? leftHandBone;
        [SerializeField] private Transform? rightHandBone;
        [SerializeField] private float hipHeightFromHmd = -0.8f;
        [SerializeField] private Vector3 rootOffset;
        [SerializeField] private Vector3 headOffset;
        [SerializeField] private Vector3 leftHandOffset;
        [SerializeField] private Vector3 rightHandOffset;

        private void LateUpdate()
        {
            if (xrOrigin == null || hmd == null)
            {
                return;
            }

            UpdateRoot();
            UpdateHead();
            UpdateLeftHand();
            UpdateRightHand();
        }

        private void UpdateRoot()
        {
            if (rootBone == null || xrOrigin == null || hmd == null)
            {
                return;
            }

            Vector3 horizontal = new Vector3(hmd.position.x, 0f, hmd.position.z);
            float heightY = hmd.position.y - xrOrigin.position.y + hipHeightFromHmd;
            rootBone.position = horizontal + xrOrigin.up * heightY + rootOffset;
            rootBone.rotation = Quaternion.Euler(0f, hmd.eulerAngles.y, 0f);
        }

        private void UpdateHead()
        {
            if (headBone == null || hmd == null)
            {
                return;
            }

            headBone.position = hmd.position + headOffset;
            headBone.rotation = hmd.rotation;
        }

        private void UpdateLeftHand()
        {
            if (leftHandBone == null || leftController == null)
            {
                return;
            }

            leftHandBone.position = leftController.position + leftHandOffset;
            leftHandBone.rotation = leftController.rotation;
        }

        private void UpdateRightHand()
        {
            if (rightHandBone == null || rightController == null)
            {
                return;
            }

            rightHandBone.position = rightController.position + rightHandOffset;
            rightHandBone.rotation = rightController.rotation;
        }
    }
}
