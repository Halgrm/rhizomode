#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.Serialization;
using UnityEngine;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// <see cref="NodeData.rotation"/> append-only field の round-trip 検証 (cue 表裏 fix)。
    /// </summary>
    public class NodeDataRotationTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void HasRotation_DefaultEmpty_ReturnsFalse()
        {
            var nd = new NodeData();
            Assert.IsFalse(nd.HasRotation, "default の new float[4]{0,0,0,0} は HasRotation=false (旧形式 sentinel)");
        }

        [Test]
        public void HasRotation_ZeroLength_ReturnsFalse()
        {
            var nd = new NodeData { rotation = new float[0] };
            Assert.IsFalse(nd.HasRotation);
        }

        [Test]
        public void HasRotation_WrongLength_ReturnsFalse()
        {
            var nd = new NodeData { rotation = new float[] { 1f, 0f, 0f } };
            Assert.IsFalse(nd.HasRotation, "Length != 4 は無効扱い");
        }

        [Test]
        public void HasRotation_IdentityQuaternion_ReturnsTrue()
        {
            var nd = new NodeData { rotation = NodeData.FromQuaternion(Quaternion.identity) };
            Assert.IsTrue(nd.HasRotation, "identity (0,0,0,1) は w=1 で HasRotation=true");
        }

        [Test]
        public void FromQuaternion_RoundTrip_PreservesValue()
        {
            var original = Quaternion.Euler(15f, 30f, 45f);
            var nd = new NodeData { rotation = NodeData.FromQuaternion(original) };
            var restored = nd.ToQuaternion();

            Assert.AreEqual(original.x, restored.x, Tolerance);
            Assert.AreEqual(original.y, restored.y, Tolerance);
            Assert.AreEqual(original.z, restored.z, Tolerance);
            Assert.AreEqual(original.w, restored.w, Tolerance);
        }

        [Test]
        public void ToQuaternion_NoRotation_ReturnsIdentity()
        {
            var nd = new NodeData();
            var q = nd.ToQuaternion();
            Assert.AreEqual(Quaternion.identity, q, "HasRotation=false なら identity を返す");
        }

        [Test]
        public void JsonRoundTrip_PreservesRotation()
        {
            var original = new NodeData
            {
                id = "n1",
                type = "TestNode",
                position = new[] { 1f, 2f, 3f },
                rotation = NodeData.FromQuaternion(Quaternion.Euler(20f, 40f, 60f)),
                paramsJson = "{}"
            };

            var json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<NodeData>(json);

            Assert.IsTrue(restored.HasRotation);
            var qOriginal = original.ToQuaternion();
            var qRestored = restored.ToQuaternion();
            Assert.AreEqual(qOriginal.x, qRestored.x, Tolerance);
            Assert.AreEqual(qOriginal.y, qRestored.y, Tolerance);
            Assert.AreEqual(qOriginal.z, qRestored.z, Tolerance);
            Assert.AreEqual(qOriginal.w, qRestored.w, Tolerance);
        }

        [Test]
        public void JsonRoundTrip_LegacyFormatWithoutRotationField_LoadsAsNoRotation()
        {
            // 旧形式 cue: rotation field 自体が JSON に存在しない
            var legacyJson =
                "{\"id\":\"n1\",\"type\":\"TestNode\",\"position\":[1.0,2.0,3.0],\"paramsJson\":\"{}\",\"groupId\":\"\"}";
            var restored = JsonUtility.FromJson<NodeData>(legacyJson);

            Assert.IsNotNull(restored);
            Assert.IsFalse(restored.HasRotation, "旧形式 cue は HasRotation=false で LookRotation fallback に流れる");
            Assert.AreEqual("n1", restored.id);
            Assert.AreEqual(1f, restored.position[0]);
        }

        [Test]
        public void JsonRoundTrip_LegacyFormatWithEmptyRotation_LoadsAsNoRotation()
        {
            // 旧形式 cue: rotation field は存在するが全 0 (=未初期化)
            var legacyJson =
                "{\"id\":\"n1\",\"type\":\"TestNode\",\"position\":[1.0,2.0,3.0],\"rotation\":[0.0,0.0,0.0,0.0],\"paramsJson\":\"{}\",\"groupId\":\"\"}";
            var restored = JsonUtility.FromJson<NodeData>(legacyJson);

            Assert.IsFalse(restored.HasRotation);
        }
    }
}
