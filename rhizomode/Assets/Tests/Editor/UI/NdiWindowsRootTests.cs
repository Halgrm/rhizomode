#nullable enable

using NUnit.Framework;
using Rhizomode.Nodes.Video;
using Rhizomode.UI;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.UI.Tests
{
    /// <summary>
    /// <see cref="NdiWindowsRoot"/> の registry + lifecycle 検証。
    /// </summary>
    public sealed class NdiWindowsRootTests
    {
        private GameObject? _rootGo;
        private NdiWindowsRoot? _root;

        [SetUp]
        public void SetUp()
        {
            _rootGo = new GameObject("TestNdiWindowsRoot");
            _root = _rootGo.AddComponent<NdiWindowsRoot>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_rootGo != null) Object.DestroyImmediate(_rootGo);
            _rootGo = null;
            _root = null;
        }

        [Test]
        public void CreateFor_GeneratesWindowAndRegistersByNodeId()
        {
            var node = new NdiReceiverNode("ndi-1");
            var window = _root!.CreateFor("ndi-1", node, (INdiViewWindowState)node);

            Assert.NotNull(window);
            Assert.AreEqual(1, _root.Count);
            Assert.IsTrue(_root.TryGet("ndi-1", out var w));
            Assert.AreSame(window, w);
        }

        [Test]
        public void CreateFor_IsIdempotent_SameNodeIdReturnsExistingWindow()
        {
            var node = new NdiReceiverNode("ndi-1");
            var w1 = _root!.CreateFor("ndi-1", node, (INdiViewWindowState)node);
            var w2 = _root!.CreateFor("ndi-1", node, (INdiViewWindowState)node);
            Assert.AreSame(w1, w2, "同 nodeId 再 create は既存を返す");
            Assert.AreEqual(1, _root.Count);
        }

        [Test]
        public void DestroyFor_RemovesFromRegistry()
        {
            var node = new NdiReceiverNode("ndi-1");
            _root!.CreateFor("ndi-1", node, (INdiViewWindowState)node);
            _root!.DestroyFor("ndi-1");
            Assert.AreEqual(0, _root.Count);
            Assert.IsFalse(_root.TryGet("ndi-1", out _));
        }

        [Test]
        public void DestroyFor_UnknownNodeId_IsNoOp()
        {
            // idempotent / null-safe (presenter Detach が二重に呼ばれても壊れない)
            Assert.DoesNotThrow(() => _root!.DestroyFor("unknown"));
            Assert.DoesNotThrow(() => _root!.DestroyFor(""));
            Assert.DoesNotThrow(() => _root!.DestroyFor(null!));
        }

        [Test]
        public void DestroyAll_ClearsRegistry()
        {
            for (int i = 0; i < 3; i++)
            {
                var node = new NdiReceiverNode("ndi-" + i);
                _root!.CreateFor("ndi-" + i, node, (INdiViewWindowState)node);
            }
            Assert.AreEqual(3, _root!.Count);

            _root!.DestroyAll();

            Assert.AreEqual(0, _root.Count, "cue 切替で全 window が破棄される");
        }

        [Test]
        public void CreateFor_ExplicitTransform_AppliesSavedPose()
        {
            var node = new NdiReceiverNode("ndi-1");
            ((INdiViewWindowState)node).SetWindowTransform(
                new Vector3(2.5f, 1.5f, 3.0f),
                new Vector3(0f, 90f, 0f),
                2.0f);

            var window = _root!.CreateFor("ndi-1", node, (INdiViewWindowState)node);

            Assert.AreEqual(new Vector3(2.5f, 1.5f, 3.0f), window.transform.position);
            Assert.AreEqual(2.0f * NdiViewWindow.BaseWidth, window.transform.localScale.x, 1e-5f);
        }

        [Test]
        public void CreateFor_NoExplicitTransform_AppliesDeterministicFallback()
        {
            // HasExplicitWindowTransform=false の node は HMD-forward + cascade offset で配置される。
            // EditMode test 環境では Camera.main の有無 / 向きが scene 状態に依存するので、
            // 厳密な座標ではなく「default (0,0,0) ではない位置 + 同 nodeId は同 position」
            // (determinism) の 2 点だけ検証する。
            var nodeA = new NdiReceiverNode("ndi-1");
            var nodeB = new NdiReceiverNode("ndi-1");  // 別 instance だが同 ID

            Assert.IsFalse(((INdiViewWindowState)nodeA).HasExplicitWindowTransform);

            var w1 = _root!.CreateFor("ndi-1", nodeA, (INdiViewWindowState)nodeA);
            var pos1 = w1.transform.position;
            _root!.DestroyFor("ndi-1");

            var w2 = _root!.CreateFor("ndi-1", nodeB, (INdiViewWindowState)nodeB);
            var pos2 = w2.transform.position;

            Assert.AreNotEqual(Vector3.zero, pos1, "fallback path は default zero ではない");
            Assert.AreEqual(pos1, pos2,
                "同 nodeId は何度 spawn しても同 cascade position (deterministic)");
        }
    }
}
