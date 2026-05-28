#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Scene;
using Rhizomode.UI.Contracts;

namespace Rhizomode.Nodes.Scene.Tests
{
    public sealed class CameraSwitchNodeTests
    {
        [Test]
        public void CyclesOnButtonPress()
        {
            var node = new CameraSwitchNode("camera");
            var button = (IInlineButton)node;

            node.SetCameraList(new[] { "CamA", "CamB" });
            button.OnButtonPressed();
            Assert.AreEqual("CamA", node.SelectedCameraName);

            button.OnButtonPressed();
            Assert.AreEqual("CamB", node.SelectedCameraName);
        }

        [Test]
        public void RisingEdgeActivation()
        {
            using var graphState = new GraphState();
            var node = new CameraSwitchNode("camera");
            node.Setup(graphState);

            node.GetInputPort("Active")!.OnNext(false);
            node.GetInputPort("Active")!.OnNext(true);

            Assert.IsTrue(node.ConsumeActivationRequest());
            Assert.IsFalse(node.ConsumeActivationRequest());

            node.Dispose();
        }

        [Test]
        public void Persistence()
        {
            var source = new CameraSwitchNode("source");
            source.SetCameraList(new[] { "CamA", "CamB" });
            ((IInlineButton)source).OnButtonPressed();

            var data = source.ToNodeData();
            var restored = new CameraSwitchNode("restored");
            restored.RestoreParamsFromJson(data.paramsJson);

            Assert.AreEqual("CamA", restored.SelectedCameraName);
        }
    }
}
