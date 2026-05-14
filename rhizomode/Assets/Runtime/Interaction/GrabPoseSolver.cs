#nullable enable

using UnityEngine;

namespace Rhizomode.Interaction
{
    /// <summary>
    /// グラブ追従 (1:1 controller-pose follow) の共通計算。
    /// NodeGrabHandler / Object3DGrabHandler / PathControlPointGrabHandler が
    /// それぞれ持っていた 「掴んだ瞬間の controllerRotation / objectRotation / localOffset を
    /// 保持し、毎フレーム rotationDelta を掛けて位置・回転を再計算する」 ロジックを集約する。
    /// pure C# (UnityEngine.Vector3 / Quaternion のみ依存) で副作用なし。
    /// </summary>
    public static class GrabPoseSolver
    {
        /// <summary>
        /// グラブ開始時のスナップショット。
        /// </summary>
        public readonly struct GrabPose
        {
            public readonly Vector3 LocalOffset;
            public readonly Quaternion InitialControllerRotation;
            public readonly Quaternion InitialObjectRotation;

            public GrabPose(
                Vector3 localOffset,
                Quaternion initialControllerRotation,
                Quaternion initialObjectRotation)
            {
                LocalOffset = localOffset;
                InitialControllerRotation = initialControllerRotation;
                InitialObjectRotation = initialObjectRotation;
            }
        }

        /// <summary>
        /// グラブ開始時の pose を捕捉する。
        /// </summary>
        public static GrabPose Capture(
            Vector3 objectPosition,
            Quaternion objectRotation,
            Vector3 controllerOrigin,
            Quaternion controllerRotation)
        {
            return new GrabPose(
                objectPosition - controllerOrigin,
                controllerRotation,
                objectRotation);
        }

        /// <summary>
        /// 現在の controller pose から追従後の位置・回転を計算する。
        /// </summary>
        public static void Solve(
            in GrabPose pose,
            Vector3 controllerOrigin,
            Quaternion controllerRotation,
            out Vector3 position,
            out Quaternion rotation)
        {
            var rotationDelta = controllerRotation * Quaternion.Inverse(pose.InitialControllerRotation);
            position = controllerOrigin + rotationDelta * pose.LocalOffset;
            rotation = rotationDelta * pose.InitialObjectRotation;
        }

        /// <summary>
        /// 回転を必要としない caller (PathControlPointGrabHandler) 向けの位置だけ版。
        /// </summary>
        public static Vector3 SolvePosition(
            in GrabPose pose,
            Vector3 controllerOrigin,
            Quaternion controllerRotation)
        {
            var rotationDelta = controllerRotation * Quaternion.Inverse(pose.InitialControllerRotation);
            return controllerOrigin + rotationDelta * pose.LocalOffset;
        }
    }
}
