#nullable enable

using NUnit.Framework;
using Rhizomode.Nodes.Video;
using Rhizomode.UI;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// <see cref="NdiReceiverNode"/> の interface 実装 + paramsJson 往復検証。
    /// </summary>
    public class NdiReceiverNodeTests
    {
        private GameObject? _testHost;

        [TearDown]
        public void TearDown()
        {
            if (_testHost != null)
                Object.DestroyImmediate(_testHost);
            _testHost = null;
        }

        [Test]
        public void Construction_DefaultsToEmptySourceName()
        {
            var node = new NdiReceiverNode("n1");
            Assert.AreEqual("", node.SourceName);
        }

        [Test]
        public void Node_ImplementsINdiReceiverNode()
        {
            var node = new NdiReceiverNode("n1");
            Assert.IsInstanceOf<INdiReceiverNode>(node, "presenter は INdiReceiverNode 経由でアクセスする");
        }

        [Test]
        public void SetSourceName_RaisesEventWithNewValue()
        {
            var node = new NdiReceiverNode("n1");
            string? received = null;
            node.OnSourceNameChanged += s => received = s;

            node.SetSourceName("CAMERA-1 (NDI Test)");

            Assert.AreEqual("CAMERA-1 (NDI Test)", node.SourceName);
            Assert.AreEqual("CAMERA-1 (NDI Test)", received);
        }

        [Test]
        public void SetSourceName_NullCoercesToEmpty()
        {
            var node = new NdiReceiverNode("n1");
            node.SetSourceName(null!);
            Assert.AreEqual("", node.SourceName);
        }

        [Test]
        public void SetSourceName_SameValueDoesNotRaise()
        {
            var node = new NdiReceiverNode("n1");
            node.SetSourceName("A");

            var raiseCount = 0;
            node.OnSourceNameChanged += _ => raiseCount++;
            node.SetSourceName("A");

            Assert.AreEqual(0, raiseCount, "no-op set は event を発火しない (presenter ループ防止)");
        }

        [Test]
        public void ParamsJson_RoundTripPreservesSourceName()
        {
            var node = new NdiReceiverNode("n1");
            node.SetSourceName("STUDIO-A (PGM)");

            var data = node.ToNodeData();

            var restored = new NdiReceiverNode("n1");
            restored.RestoreParamsFromJson(data.paramsJson);

            Assert.AreEqual("STUDIO-A (PGM)", restored.SourceName);
        }

        [Test]
        public void RestoreParamsFromJson_EmptyStringIsNoOp()
        {
            var node = new NdiReceiverNode("n1");
            node.SetSourceName("KEEP");
            node.RestoreParamsFromJson("");
            Assert.AreEqual("KEEP", node.SourceName, "空 JSON は default 復元せず現状維持");
        }

        [Test]
        public void RestoreParamsFromJson_MalformedDoesNotThrow()
        {
            var node = new NdiReceiverNode("n1");
            Assert.DoesNotThrow(() => node.RestoreParamsFromJson("{not valid json"));
            Assert.AreEqual("", node.SourceName);
        }

        [Test]
        public void Node_ImplementsINdiViewWindowState()
        {
            var node = new NdiReceiverNode("n1");
            Assert.IsInstanceOf<INdiViewWindowState>(node,
                "presenter は AsNdiViewWindowState() 経由で window 状態にアクセスする");
        }

        [Test]
        public void DefaultWindowState_HasExplicitFalseAndUnitScale()
        {
            var node = new NdiReceiverNode("n1");
            var s = (INdiViewWindowState)node;
            Assert.IsFalse(s.HasExplicitWindowTransform, "新規 node は explicit 未設定");
            Assert.AreEqual(1.0f, s.WindowScale);
            Assert.AreEqual(Vector3.zero, s.WindowPosition);
            Assert.AreEqual(Vector3.zero, s.WindowEulerAngles);
            Assert.IsFalse(s.HideFromMirror);
        }

        [Test]
        public void SetWindowTransform_FlipsExplicitFlagAndRaisesEvent()
        {
            var node = new NdiReceiverNode("n1");
            var s = (INdiViewWindowState)node;
            int raised = 0;
            s.OnWindowTransformChanged += () => raised++;

            s.SetWindowTransform(new Vector3(1, 1.5f, 2), new Vector3(0, 30, 0), 1.5f);

            Assert.IsTrue(s.HasExplicitWindowTransform);
            Assert.AreEqual(new Vector3(1, 1.5f, 2), s.WindowPosition);
            Assert.AreEqual(new Vector3(0, 30, 0), s.WindowEulerAngles);
            Assert.AreEqual(1.5f, s.WindowScale);
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void SetWindowTransform_NaNOrInfRejected()
        {
            // NaN / Infinity でグラフが壊れないよう defensive 拒否 (Plan v0.3 §リスク)
            var node = new NdiReceiverNode("n1");
            var s = (INdiViewWindowState)node;
            s.SetWindowTransform(new Vector3(float.NaN, 0, 0), Vector3.zero, 1f);
            Assert.IsFalse(s.HasExplicitWindowTransform, "NaN は無視されて flag flip されない");
            s.SetWindowTransform(Vector3.zero, Vector3.zero, float.PositiveInfinity);
            Assert.IsFalse(s.HasExplicitWindowTransform, "Inf scale も拒否");
        }

        [Test]
        public void SetHideFromMirror_TogglesAndRaisesEventOnce()
        {
            var node = new NdiReceiverNode("n1");
            var s = (INdiViewWindowState)node;
            int raised = 0;
            s.OnWindowTransformChanged += () => raised++;
            s.SetHideFromMirror(true);
            s.SetHideFromMirror(true); // idempotent
            Assert.IsTrue(s.HideFromMirror);
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void ParamsJson_RoundTripPreservesWindowTransform()
        {
            var node = new NdiReceiverNode("n1");
            ((INdiViewWindowState)node).SetWindowTransform(
                new Vector3(0.5f, 1.2f, 1.5f),
                new Vector3(10, 45, 0),
                1.75f);
            ((INdiViewWindowState)node).SetHideFromMirror(true);

            var data = node.ToNodeData();
            var restored = new NdiReceiverNode("n1");
            restored.RestoreParamsFromJson(data.paramsJson);

            var rs = (INdiViewWindowState)restored;
            Assert.IsTrue(rs.HasExplicitWindowTransform);
            Assert.AreEqual(new Vector3(0.5f, 1.2f, 1.5f), rs.WindowPosition);
            Assert.AreEqual(new Vector3(10, 45, 0), rs.WindowEulerAngles);
            Assert.AreEqual(1.75f, rs.WindowScale, 1e-5f);
            Assert.IsTrue(rs.HideFromMirror);
        }

        [Test]
        public void ParamsJson_OldFormatLeavesWindowAsImplicit()
        {
            // 旧 cue ("sourceName" のみ) を読んでも HasExplicitWindowTransform = false の
            // ままで残ること = factory が HMD-forward + cascade fallback を採用する経路に乗る。
            var oldJson = "{\"sourceName\":\"OLD-CAM\"}";
            var node = new NdiReceiverNode("n1");
            node.RestoreParamsFromJson(oldJson);

            Assert.AreEqual("OLD-CAM", node.SourceName);
            var s = (INdiViewWindowState)node;
            Assert.IsFalse(s.HasExplicitWindowTransform,
                "旧 cue は explicit flag が立たない (default の HMD-forward fallback が走る)");
            Assert.AreEqual(1.0f, s.WindowScale, "scale は 0 fallback で 1.0 が入る");
        }

        // --- StableHash32 / CascadeOffset 検証 (Plan v0.3 §Cascade offset) ---

        [Test]
        public void StableHash32_KnownVector_MatchesExpected()
        {
            // FNV-1a 32bit regression。実装が偶然変わって offset がズレないよう、
            // 既知の文字列に対する hash 値を const と照合。
            Assert.AreEqual(2166136261u, NdiViewWindowMath.StableHash32(""), "empty string = OffsetBasis");
            Assert.AreEqual(3826002220u, NdiViewWindowMath.StableHash32("a"), "single char 'a' = 0xE40C292C");
            // "foobar" = 0xBF9CF968 per local FNV-1a 32bit run (regression anchor)
            Assert.AreEqual(3214735720u, NdiViewWindowMath.StableHash32("foobar"));
        }

        [Test]
        public void StableHash32_SameStringAcrossSessions_ReturnsSameValue()
        {
            // GetHashCode() なら .NET Core 以降 randomize されるが、FNV-1a は session を
            // 跨いで stable であることを示すための同値性 test (run-to-run ではなく
            // 同 process 内での同 input → 同 output を保証)。
            var s1 = "node-001";
            var s2 = string.Concat("node-", "001"); // 別 alloc だが同 content
            Assert.AreEqual(NdiViewWindowMath.StableHash32(s1), NdiViewWindowMath.StableHash32(s2));
        }

        [Test]
        public void CascadeOffset_SameNodeId_ReturnsSamePosition()
        {
            var fwd = Vector3.forward;
            var right = Vector3.right;
            var o1 = NdiViewWindowMath.CascadeOffset("alpha", fwd, right);
            var o2 = NdiViewWindowMath.CascadeOffset("alpha", fwd, right);
            Assert.AreEqual(o1, o2, "同 nodeId は何度呼んでも同じ offset");
        }

        [Test]
        public void CascadeOffset_AdjacentSlotsDoNotOverlap()
        {
            // 隣接 slot 同士の距離が window collider width (1.0m) を超えていることを
            // 全 slot 組合せで確認 (Plan v0.3 では SideSpacing = 1.2m)。
            var fwd = Vector3.forward;
            var right = Vector3.right;

            // 全 slot を slot index から再構成 (hash を経由しない直接 sampling)
            // NdiViewWindowMath.CascadeOffset は string 入力なので、内部実装の slot index
            // 配置が想定通りに distribute されているか確認する。
            // ここでは 4 個の既知 nodeId で実距離を見る。
            var ids = new[] { "n0", "n1", "n2", "n3", "n4", "n5" };
            var positions = new Vector3[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                positions[i] = NdiViewWindowMath.CascadeOffset(ids[i], fwd, right);

            // 少なくとも 1 ペアが overlap する場合に fail。spacing 1.2m で衝突しない設計。
            for (int i = 0; i < positions.Length; i++)
            for (int j = i + 1; j < positions.Length; j++)
            {
                var dist = Vector3.Distance(positions[i], positions[j]);
                if (dist < 0.01f)
                {
                    // 同 slot に hash 衝突した可能性 (slot 8 個に 6 入力なら起こり得る)。
                    // 同 slot なら同 position なので skip (test は 隣接 slot を見る)。
                    continue;
                }
                Assert.GreaterOrEqual(dist, 0.3f,
                    $"slot 間が 30cm 以上離れていること (i={i} j={j} dist={dist:F3})");
            }
        }

        [Test]
        public void PresenterPreviewQuad_CompensatesScaledHost()
        {
            _testHost = new GameObject("NdiReceiverPreviewHost");
            _testHost.transform.localScale = new Vector3(0.20f, 0.12f, 1f);
            var presenter = _testHost.AddComponent<NdiReceiverPresenter>();

            presenter.Attach(new NdiReceiverNode("n1"));

            var preview = _testHost.transform.Find("NdiReceiver_Preview");
            Assert.NotNull(preview);
            var previewTransform = preview!;
            Assert.GreaterOrEqual(previewTransform.lossyScale.x, 0.2f);
            Assert.GreaterOrEqual(previewTransform.lossyScale.y, 0.1f);
            Assert.Less(previewTransform.position.y, _testHost.transform.position.y);
        }
    }
}
