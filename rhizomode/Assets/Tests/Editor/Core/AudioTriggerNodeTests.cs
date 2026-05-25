#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using R3;
using Rhizomode.Graph.Model;
using Rhizomode.Nodes.Audio;
using Rhizomode.SharedKernel;

namespace Rhizomode.Core.Tests
{
    /// <summary>
    /// <see cref="AudioTriggerNode"/> の SetLevel パスにおける NaN sanitize / 立上り / 立下り edge 検出。
    /// </summary>
    public class AudioTriggerNodeTests
    {
        private const float Default = 1e-5f;

        [Test]
        public void SetLevel_EmitsLevelToOutput()
        {
            var node = new AudioTriggerNode("n1");
            var levels = SubscribeLevel(node);

            node.SetLevel(0.3f);
            node.SetLevel(0.7f);

            CollectionAssert.AreEqual(new[] { 0.3f, 0.7f }, levels);
        }

        [Test]
        public void SetLevel_NaN_EmitsZero()
        {
            var node = new AudioTriggerNode("n1");
            var levels = SubscribeLevel(node);

            node.SetLevel(float.NaN);

            Assert.AreEqual(1, levels.Count);
            Assert.AreEqual(0f, levels[0], Default, "NaN は 0 に sanitize される (runtime fallback 原則)");
        }

        [Test]
        public void SetLevel_PositiveInfinity_EmitsZero()
        {
            var node = new AudioTriggerNode("n1");
            var levels = SubscribeLevel(node);

            node.SetLevel(float.PositiveInfinity);

            Assert.AreEqual(0f, levels[0], Default);
        }

        [Test]
        public void SetLevel_NegativeInfinity_EmitsZero()
        {
            var node = new AudioTriggerNode("n1");
            var levels = SubscribeLevel(node);

            node.SetLevel(float.NegativeInfinity);

            Assert.AreEqual(0f, levels[0], Default);
        }

        [Test]
        public void Trigger_RisingEdge_EmitsTrue()
        {
            var node = new AudioTriggerNode("n1");
            var triggers = SubscribeTrigger(node);

            // default threshold = 0.5
            node.SetLevel(0.3f); // below → no edge
            node.SetLevel(0.7f); // above → rising

            CollectionAssert.AreEqual(new[] { true }, triggers);
        }

        [Test]
        public void Trigger_FallingEdge_EmitsFalse()
        {
            var node = new AudioTriggerNode("n1");
            var triggers = SubscribeTrigger(node);

            node.SetLevel(0.7f); // rising
            node.SetLevel(0.2f); // falling

            CollectionAssert.AreEqual(new[] { true, false }, triggers);
        }

        [Test]
        public void Trigger_StayingAbove_NoExtraEmit()
        {
            var node = new AudioTriggerNode("n1");
            var triggers = SubscribeTrigger(node);

            node.SetLevel(0.7f);
            node.SetLevel(0.8f);
            node.SetLevel(0.9f);

            CollectionAssert.AreEqual(new[] { true }, triggers);
        }

        [Test]
        public void Trigger_StayingBelow_NoEmit()
        {
            var node = new AudioTriggerNode("n1");
            var triggers = SubscribeTrigger(node);

            node.SetLevel(0.1f);
            node.SetLevel(0.2f);
            node.SetLevel(0.3f);

            CollectionAssert.IsEmpty(triggers);
        }

        [Test]
        public void TrySetParam_Threshold_UpdatesThreshold()
        {
            var node = new AudioTriggerNode("n1");
            var accessor = (INodeParamAccessor)node;

            var ok = accessor.TrySetParam("Threshold", ParamValue.Float(0.2f));
            Assert.IsTrue(ok);

            var triggers = SubscribeTrigger(node);
            node.SetLevel(0.1f); // below new 0.2 threshold
            node.SetLevel(0.25f); // above → rising

            CollectionAssert.AreEqual(new[] { true }, triggers);
        }

        [Test]
        public void TrySetParam_WrongType_ReturnsFalse()
        {
            var accessor = (INodeParamAccessor)new AudioTriggerNode("n1");
            var ok = accessor.TrySetParam("Threshold", ParamValue.Bool(true));
            Assert.IsFalse(ok);
        }

        [Test]
        public void TrySetParam_UnknownName_ReturnsFalse()
        {
            var accessor = (INodeParamAccessor)new AudioTriggerNode("n1");
            var ok = accessor.TrySetParam("Whatever", ParamValue.Float(1f));
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryGetParam_FreqMinFreqMaxThreshold_ReturnsDefaults()
        {
            var accessor = (INodeParamAccessor)new AudioTriggerNode("n1");

            Assert.IsTrue(accessor.TryGetParam("FreqMin", out var lo));
            Assert.IsTrue(accessor.TryGetParam("FreqMax", out var hi));
            Assert.IsTrue(accessor.TryGetParam("Threshold", out var th));

            Assert.AreEqual(60f, lo.AsFloat, Default);
            Assert.AreEqual(250f, hi.AsFloat, Default);
            Assert.AreEqual(0.5f, th.AsFloat, Default);
        }

        private static List<float> SubscribeLevel(AudioTriggerNode node)
        {
            var levels = new List<float>();
            var port = (OutputPort<float>)node.GetOutputPort("Level")!;
            port.Observable.Subscribe(v => levels.Add(v));
            return levels;
        }

        private static List<bool> SubscribeTrigger(AudioTriggerNode node)
        {
            var triggers = new List<bool>();
            var port = (OutputPort<bool>)node.GetOutputPort("Trigger")!;
            port.Observable.Subscribe(v => triggers.Add(v));
            return triggers;
        }
    }
}
