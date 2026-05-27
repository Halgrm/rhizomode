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
