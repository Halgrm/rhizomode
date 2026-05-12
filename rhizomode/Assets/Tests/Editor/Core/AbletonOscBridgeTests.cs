#nullable enable

using System.Threading.Tasks;
using NUnit.Framework;
using Rhizomode.ExternalInput;
using UnityEngine;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// AbletonOscBridgeの構造的検証。AbletonLink.Instanceなしの空Layoutフォールバック
    /// と、構造体の値型挙動を確認する。OscJack統合の動作確認は手動検証で行う。
    /// </summary>
    public class AbletonOscBridgeTests
    {
        [Test]
        public void AbletonClipMeta_DefaultValues_AreSafe()
        {
            var meta = new AbletonClipMeta();
            Assert.IsFalse(meta.HasClip);
            Assert.IsNull(meta.Name);
        }

        [Test]
        public void AbletonTrackMeta_CanHoldClipArray()
        {
            var track = new AbletonTrackMeta
            {
                Name = "Drums",
                Clips = new[]
                {
                    new AbletonClipMeta { HasClip = true, Name = "Kick", Color = Color.red },
                    new AbletonClipMeta { HasClip = false, Name = string.Empty, Color = Color.gray },
                }
            };

            Assert.AreEqual("Drums", track.Name);
            Assert.AreEqual(2, track.Clips.Length);
            Assert.IsTrue(track.Clips[0].HasClip);
            Assert.AreEqual("Kick", track.Clips[0].Name);
            Assert.IsFalse(track.Clips[1].HasClip);
        }

        [Test]
        public async Task QueryLayoutAsync_WithoutAbletonLink_ReturnsTrueWithEmptyLayout()
        {
            // AbletonLink.InstanceがnullのときはWarningログ＋空Layoutで継続する設計
            var go = new GameObject("BridgeTest");
            try
            {
                var bridge = go.AddComponent<AbletonOscBridge>();

                var ok = await bridge.QueryLayoutAsync(timeoutMs: 100);

                Assert.IsTrue(ok, "Bridge should return true on missing AbletonLink (Skip相当)");
                Assert.IsTrue(bridge.IsReady, "Layout should be marked ready even when empty");
                Assert.IsNotNull(bridge.Tracks);
                Assert.AreEqual(0, bridge.Tracks.Length);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AbletonOscBridge_InitialState_IsEmpty()
        {
            var go = new GameObject("BridgeTest");
            try
            {
                var bridge = go.AddComponent<AbletonOscBridge>();

                Assert.AreEqual(0, bridge.NumTracks);
                Assert.AreEqual(0, bridge.NumScenes);
                Assert.IsFalse(bridge.IsReady);
                Assert.IsNotNull(bridge.Tracks);
                Assert.AreEqual(0, bridge.Tracks.Length);

                // Macro 関連の初期状態
                Assert.IsFalse(bridge.IsMacrosReady);
                Assert.IsNotNull(bridge.Macros);
                Assert.AreEqual(0, bridge.Macros.Length);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AbletonMacroMeta_DefaultValues_AreSafe()
        {
            var meta = new AbletonMacroMeta();
            Assert.AreEqual(0, meta.ParamId);
            Assert.IsNull(meta.Name);
            Assert.AreEqual(0f, meta.Value);
            Assert.AreEqual(0f, meta.Min);
            Assert.AreEqual(0f, meta.Max);
        }

        [Test]
        public async Task QueryMacrosAsync_WithoutAbletonLink_ReturnsTrueWithEmptyMacros()
        {
            var go = new GameObject("BridgeTest");
            try
            {
                var bridge = go.AddComponent<AbletonOscBridge>();

                var ok = await bridge.QueryMacrosAsync(trackIndex: -1, deviceIndex: 0, macroCount: 8, timeoutMs: 100);

                Assert.IsTrue(ok, "AbletonLinkが未生成でも空Macroで継続する設計");
                Assert.IsTrue(bridge.IsMacrosReady);
                Assert.AreEqual(0, bridge.Macros.Length);
                Assert.AreEqual(-1, bridge.MacroTrackIndex);
                Assert.AreEqual(0, bridge.MacroDeviceIndex);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
