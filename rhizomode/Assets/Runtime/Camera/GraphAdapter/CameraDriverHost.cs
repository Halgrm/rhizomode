#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Scene;
using Unity.Cinemachine;
using UnityEngine;

namespace Rhizomode.Cameras.GraphAdapter
{
    /// <summary>
    /// Drives Cinemachine camera priority changes requested by CameraSwitchNode.
    /// </summary>
    public sealed class CameraDriverHost
    {
        public const int LivePriority = 20;
        public const int DormantPriority = 5;

        private readonly GraphState _graphState;
        private readonly List<CameraSwitchNode> _switchNodeBuffer = new();

        public CameraDriverHost(GraphState graphState)
        {
            _graphState = graphState;
        }

        public void Tick()
        {
            if (_graphState.IsDisposed) return;

            var cameras = Object.FindObjectsByType<CinemachineCamera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var names = BuildCameraNames(cameras);

            SnapshotSwitchNodes();
            foreach (var node in _switchNodeBuffer)
                node.SetCameraList(names);

            DriveRequestedSwitches(cameras);
        }

        private static string[] BuildCameraNames(CinemachineCamera[] cameras)
        {
            var names = new string[cameras.Length];
            for (var i = 0; i < cameras.Length; i++)
                names[i] = cameras[i] != null ? cameras[i].gameObject.name : "";
            return names;
        }

        private void SnapshotSwitchNodes()
        {
            _switchNodeBuffer.Clear();
            foreach (var node in _graphState.Nodes.Values)
            {
                if (node is CameraSwitchNode switchNode)
                    _switchNodeBuffer.Add(switchNode);
            }
        }

        private void DriveRequestedSwitches(CinemachineCamera[] cameras)
        {
            foreach (var node in _switchNodeBuffer)
            {
                if (!node.ConsumeActivationRequest()) continue;
                ApplyPriority(cameras, node.SelectedCameraName);
            }
        }

        private static void ApplyPriority(CinemachineCamera[] cameras, string liveCameraName)
        {
            if (string.IsNullOrEmpty(liveCameraName)) return;
            if (!HasCamera(cameras, liveCameraName)) return;

            foreach (var camera in cameras)
            {
                if (camera == null) continue;
                camera.Priority = camera.gameObject.name == liveCameraName
                    ? LivePriority
                    : DormantPriority;
            }
        }

        private static bool HasCamera(CinemachineCamera[] cameras, string cameraName)
        {
            foreach (var camera in cameras)
            {
                if (camera != null && camera.gameObject.name == cameraName)
                    return true;
            }
            return false;
        }
    }
}
