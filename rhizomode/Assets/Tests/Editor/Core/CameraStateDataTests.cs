#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.Serialization;
using UnityEngine;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// Phase 4 カメラ永続化 DTO (CameraStateData 系) の JsonUtility 往復・旧セーブ互換テスト。
    /// </summary>
    public class CameraStateDataTests
    {
        [Test]
        public void CameraStateData_JsonRoundTrip_PreservesCamerasAndNestedKnots()
        {
            var state = new CameraStateData { schemaVersion = 1, liveCameraName = "CM_Orbital" };
            state.cameras.Add(new CameraEntryData
            {
                name = "CM_Orbital",
                fov = 42f,
                dutch = 7f,
                lookAtTarget = "Stage",
                followTarget = "Performer",
                motionSourceNodeId = "lfo-1",
                motionSourcePort = "Value",
                motionDrive = 0.33f,
            });
            var path = new CameraPathData { cameraName = "CM_PathDolly" };
            path.knots.Add(new KnotData
            {
                position = new[] { 1f, 2f, 3f },
                tangentIn = new[] { -1f, 0f, 0f },
                tangentOut = new[] { 1f, 0f, 0f },
                rotation = new[] { 0f, 0f, 0f, 1f },
            });
            state.paths.Add(path);

            string json = JsonUtility.ToJson(state, true);
            var restored = JsonUtility.FromJson<CameraStateData>(json);

            Assert.IsNotNull(restored);
            Assert.AreEqual("CM_Orbital", restored!.liveCameraName);
            Assert.AreEqual(1, restored.cameras.Count);
            Assert.AreEqual(42f, restored.cameras[0].fov, 0.001f);
            Assert.AreEqual("lfo-1", restored.cameras[0].motionSourceNodeId);
            Assert.AreEqual(0.33f, restored.cameras[0].motionDrive, 0.001f);
            // ネストリスト paths[].knots[] が往復で保持されること (JsonUtility 1 段ネスト)
            Assert.AreEqual(1, restored.paths.Count);
            Assert.AreEqual("CM_PathDolly", restored.paths[0].cameraName);
            Assert.AreEqual(1, restored.paths[0].knots.Count);
            Assert.AreEqual(3f, restored.paths[0].knots[0].position[2], 0.001f);
            Assert.AreEqual(1f, restored.paths[0].knots[0].rotation[3], 0.001f);
        }

        [Test]
        public void GraphData_RoundTrip_IncludesCameraState()
        {
            var data = new GraphData();
            data.cameraState.liveCameraName = "CM_Follow";
            data.cameraState.cameras.Add(new CameraEntryData { name = "CM_Follow", fov = 55f });

            string json = JsonUtility.ToJson(data, true);
            var restored = JsonUtility.FromJson<GraphData>(json);

            Assert.IsNotNull(restored);
            Assert.IsNotNull(restored!.cameraState);
            Assert.AreEqual("CM_Follow", restored.cameraState.liveCameraName);
            Assert.AreEqual(1, restored.cameraState.cameras.Count);
            Assert.AreEqual(55f, restored.cameraState.cameras[0].fov, 0.001f);
        }

        [Test]
        public void GraphData_LegacyJsonWithoutCameraState_DefaultsToEmpty()
        {
            // Phase 4 以前のセーブファイル相当 (cameraState キー無し)
            const string legacyJson = "{\"version\":\"1.0\",\"nodes\":[],\"edges\":[]}";

            var restored = JsonUtility.FromJson<GraphData>(legacyJson);

            Assert.IsNotNull(restored);
            Assert.IsNotNull(restored!.cameraState, "cameraState は非 null の既定値になるべき");
            Assert.IsNotNull(restored.cameraState.cameras);
            Assert.IsNotNull(restored.cameraState.paths);
            Assert.AreEqual(0, restored.cameraState.cameras.Count);
            Assert.AreEqual(0, restored.cameraState.paths.Count);
        }

        [Test]
        public void CameraStateData_EmptyRoundTrip_IsStable()
        {
            var state = new CameraStateData();

            string json = JsonUtility.ToJson(state);
            var restored = JsonUtility.FromJson<CameraStateData>(json);

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored!.cameras.Count);
            Assert.AreEqual(0, restored.paths.Count);
            Assert.AreEqual("", restored.liveCameraName);
        }

        [Test]
        public void CameraStateData_PartialJsonWithoutLists_DoesNotThrow()
        {
            // cameras / paths キー欠落 — FromJson が落ちず既定値を保つこと
            const string partialJson = "{\"schemaVersion\":1,\"liveCameraName\":\"CM_Orbital\"}";

            CameraStateData? restored = null;
            Assert.DoesNotThrow(() => restored = JsonUtility.FromJson<CameraStateData>(partialJson));
            Assert.IsNotNull(restored);
            Assert.AreEqual("CM_Orbital", restored!.liveCameraName);
            Assert.IsNotNull(restored.cameras);
            Assert.IsNotNull(restored.paths);
        }
    }
}
