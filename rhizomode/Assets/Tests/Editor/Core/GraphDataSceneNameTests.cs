#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.Serialization;
using UnityEngine;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// <see cref="GraphData.sceneName"/> append-only field の round-trip + 旧形式互換。
    /// </summary>
    public class GraphDataSceneNameTests
    {
        [Test]
        public void SceneName_DefaultsToEmpty()
        {
            var data = new GraphData();
            Assert.AreEqual("", data.sceneName, "default は空文字 (旧形式 sentinel)");
        }

        [Test]
        public void JsonRoundTrip_PreservesSceneName()
        {
            var original = new GraphData { sceneName = "Forest" };
            var json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<GraphData>(json);

            Assert.AreEqual("Forest", restored.sceneName);
        }

        [Test]
        public void JsonRoundTrip_LegacyFormatWithoutSceneName_LoadsAsEmpty()
        {
            // 旧形式: sceneName field 自体が JSON に存在しない
            var legacyJson = "{\"version\":\"1.0\",\"nodes\":[],\"edges\":[]}";
            var restored = JsonUtility.FromJson<GraphData>(legacyJson);

            Assert.IsNotNull(restored);
            Assert.AreEqual("", restored.sceneName,
                "旧形式 cue は sceneName 空のまま → Load 側でシーン切替判定が常に false に流れる");
        }
    }
}
