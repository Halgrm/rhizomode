#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Cameras;
using Rhizomode.Graph.Serialization;
using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Rhizomode.UI
{
    /// <summary>
    /// <see cref="ICameraStatePersistence"/> 実装。シーン上の <see cref="CinemachineCamera"/> を
    /// 走査してカメラ状態を捕捉 / 復元する。
    /// </summary>
    /// <remarks>
    /// カメラはシーン常駐 (ロードで生成されない) のため GameObject 名をキーに復元する。
    /// ターゲットは <see cref="LookAtTargetMarker.DisplayName"/> で解決する。
    /// </remarks>
    public sealed class CameraStatePersistenceService : ICameraStatePersistence
    {
        // CameraManagerPanelController と同値 (live=20 / dormant=5)。
        private const int LivePriority = 20;
        private const int DormantPriority = 5;

        public CameraStateData Capture()
        {
            var data = new CameraStateData();
            var cameras = FindCameras();

            CinemachineCamera? live = null;
            int bestPriority = int.MinValue;

            foreach (var cam in cameras)
            {
                if (cam == null) continue;
                data.cameras.Add(CaptureCamera(cam));

                var path = CapturePath(cam);
                if (path != null) data.paths.Add(path);

                int priority = cam.Priority.Value;
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    live = cam;
                }
            }

            data.liveCameraName = live != null ? live.name : "";
            return data;
        }

        public void Restore(CameraStateData? state)
        {
            if (state == null) return;
            var cameras = FindCameras();
            var byName = IndexByName(cameras);

            if (state.cameras != null)
            {
                // F-P4-003: 重複した保存エントリは最初の 1 件のみ適用する。
                var applied = new HashSet<string>();
                foreach (var entry in state.cameras)
                {
                    if (entry == null || !applied.Add(entry.name)) continue;
                    TryRestoreCamera(byName, entry);
                }
            }

            if (state.paths != null)
            {
                var appliedPaths = new HashSet<string>();
                foreach (var pathData in state.paths)
                {
                    if (pathData == null || !appliedPaths.Add(pathData.cameraName)) continue;
                    TryRestorePath(byName, pathData);
                }
            }

            ApplyLivePriority(cameras, state.liveCameraName);
        }

        private static CinemachineCamera[] FindCameras() =>
            UnityEngine.Object.FindObjectsByType<CinemachineCamera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static Dictionary<string, CinemachineCamera> IndexByName(
            IReadOnlyList<CinemachineCamera> cameras)
        {
            var map = new Dictionary<string, CinemachineCamera>();
            foreach (var cam in cameras)
            {
                if (cam == null) continue;
                if (map.ContainsKey(cam.name))
                {
                    Debug.LogWarning(
                        $"[CameraStatePersistence] Duplicate camera name '{cam.name}' — using first.");
                    continue;
                }
                map[cam.name] = cam;
            }
            return map;
        }

        private static CameraEntryData CaptureCamera(CinemachineCamera cam)
        {
            var entry = new CameraEntryData
            {
                name = cam.name,
                fov = cam.Lens.FieldOfView,
                dutch = cam.Lens.Dutch,
                lookAtTarget = ResolveMarkerName(cam.LookAt),
                followTarget = ResolveMarkerName(cam.Follow),
            };

            var binding = cam.GetComponent<CameraMotionSourceBinding>();
            if (binding != null)
            {
                entry.motionSourceNodeId = binding.NodeId;
                entry.motionSourcePort = binding.PortName;
            }

            var motion = cam.GetComponent<ICameraMotion>();
            if (motion != null) entry.motionDrive = motion.Drive;
            return entry;
        }

        private static CameraPathData? CapturePath(CinemachineCamera cam)
        {
            var pathController = cam.GetComponent<PathCameraController>();
            var container = pathController != null ? pathController.Spline : null;
            if (container == null || container.Spline == null) return null;

            var data = new CameraPathData { cameraName = cam.name };
            foreach (var knot in container.Spline)
                data.knots.Add(ToKnotData(knot));
            return data;
        }

        private static void TryRestoreCamera(
            IReadOnlyDictionary<string, CinemachineCamera> byName, CameraEntryData entry)
        {
            if (entry == null) return;
            if (!byName.TryGetValue(entry.name, out var cam) || cam == null)
            {
                Debug.LogWarning(
                    $"[CameraStatePersistence] Camera '{entry.name}' not found — skipped.");
                return;
            }
            try { RestoreCamera(cam, entry); }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[CameraStatePersistence] Restore camera '{entry.name}' failed: {e.Message}");
            }
        }

        private static void RestoreCamera(CinemachineCamera cam, CameraEntryData entry)
        {
            var lens = cam.Lens;
            lens.FieldOfView = entry.fov;
            lens.Dutch = entry.dutch;
            cam.Lens = lens;

            ApplyTarget(entry.lookAtTarget, t => cam.LookAt = t);
            ApplyTarget(entry.followTarget, t => cam.Follow = t);

            bool hasSource = !string.IsNullOrEmpty(entry.motionSourceNodeId)
                             && !string.IsNullOrEmpty(entry.motionSourcePort);

            var binding = cam.GetComponent<CameraMotionSourceBinding>();
            if (binding != null)
            {
                if (hasSource) binding.SetBinding(entry.motionSourceNodeId, entry.motionSourcePort);
                else binding.Clear();
            }

            // ソース未接続カメラは保存済みの静的 Drive 値を適用 (接続カメラはパネルが購読し直す)。
            if (!hasSource)
                cam.GetComponent<ICameraMotion>()?.SetDrive(entry.motionDrive);
        }

        private static void TryRestorePath(
            IReadOnlyDictionary<string, CinemachineCamera> byName, CameraPathData pathData)
        {
            if (pathData == null) return;
            if (!byName.TryGetValue(pathData.cameraName, out var cam) || cam == null)
            {
                Debug.LogWarning(
                    $"[CameraStatePersistence] Path camera '{pathData.cameraName}' not found — skipped.");
                return;
            }
            try { RestorePath(cam, pathData); }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[CameraStatePersistence] Restore path '{pathData.cameraName}' failed: {e.Message}");
            }
        }

        private static void RestorePath(CinemachineCamera cam, CameraPathData pathData)
        {
            var pathController = cam.GetComponent<PathCameraController>();
            var container = pathController != null ? pathController.Spline : null;
            if (container == null || container.Spline == null) return;
            if (pathData.knots == null || pathData.knots.Count == 0) return;

            RewriteSpline(container.Spline, pathData.knots);
            pathController!.NotifySplineMutated();
        }

        private static void RewriteSpline(Spline spline, List<KnotData> knots)
        {
            if (spline.Count == knots.Count)
            {
                for (int i = 0; i < knots.Count; i++)
                    spline.SetKnot(i, ToBezierKnot(knots[i]));
                return;
            }
            spline.Clear();
            foreach (var knot in knots)
                spline.Add(ToBezierKnot(knot));
        }

        private static void ApplyTarget(string displayName, Action<Transform?> apply)
        {
            // F-P4-002: 空 = 保存時に未設定 (null) だった → 明示クリア。
            // 前キューの古いターゲットを引き継がせない。
            if (string.IsNullOrEmpty(displayName))
            {
                apply(null);
                return;
            }
            var target = ResolveTarget(displayName);
            if (target != null) apply(target);
            // 非空名が解決不能 = マーカー欠落。fail-open で現状維持。
            else Debug.LogWarning(
                $"[CameraStatePersistence] Target '{displayName}' not found — kept current.");
        }

        private static Transform? ResolveTarget(string displayName)
        {
            Transform? found = null;
            int matches = 0;
            foreach (var marker in LookAtTargetMarker.AllTargets)
            {
                if (marker == null || marker.DisplayName != displayName) continue;
                matches++;
                if (found == null) found = marker.transform;
            }
            // F-P4-004: DisplayName 重複は順序依存になるため警告する。
            if (matches > 1)
                Debug.LogWarning(
                    $"[CameraStatePersistence] Duplicate target name '{displayName}' " +
                    $"({matches} markers) — using first.");
            return found;
        }

        private static void ApplyLivePriority(
            IReadOnlyList<CinemachineCamera> cameras, string liveCameraName)
        {
            if (string.IsNullOrEmpty(liveCameraName)) return;
            // F-P4-003: 同名カメラが複数あっても最初の 1 台だけをライブにする。
            bool liveAssigned = false;
            foreach (var cam in cameras)
            {
                if (cam == null) continue;
                if (!liveAssigned && cam.name == liveCameraName)
                {
                    cam.Priority = LivePriority;
                    liveAssigned = true;
                }
                else
                {
                    cam.Priority = DormantPriority;
                }
            }
        }

        private static string ResolveMarkerName(Transform? target)
        {
            if (target == null) return "";
            var marker = target.GetComponent<LookAtTargetMarker>();
            if (marker != null) return marker.DisplayName;
            // マーカー無しターゲットは名前で復元できない (復元時に "" → クリア扱い)。
            Debug.LogWarning(
                $"[CameraStatePersistence] Target '{target.name}' has no LookAtTargetMarker — " +
                "won't survive save/load. Add a LookAtTargetMarker to make it persistable.");
            return "";
        }

        private static KnotData ToKnotData(BezierKnot knot) => new()
        {
            position = ToArray3(knot.Position),
            tangentIn = ToArray3(knot.TangentIn),
            tangentOut = ToArray3(knot.TangentOut),
            rotation = ToArray4(knot.Rotation.value),
        };

        private static BezierKnot ToBezierKnot(KnotData knot) => new(
            ToFloat3(knot.position),
            ToFloat3(knot.tangentIn),
            ToFloat3(knot.tangentOut),
            ToQuaternion(knot.rotation));

        private static float[] ToArray3(float3 v) => new[] { v.x, v.y, v.z };

        private static float[] ToArray4(float4 v) => new[] { v.x, v.y, v.z, v.w };

        private static float3 ToFloat3(float[]? a) =>
            a != null && a.Length >= 3 ? new float3(a[0], a[1], a[2]) : float3.zero;

        private static quaternion ToQuaternion(float[]? a) =>
            a != null && a.Length >= 4
                ? new quaternion(a[0], a[1], a[2], a[3])
                : quaternion.identity;
    }
}
