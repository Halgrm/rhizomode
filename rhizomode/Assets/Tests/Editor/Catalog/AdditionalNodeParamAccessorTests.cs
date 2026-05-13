#nullable enable

using NUnit.Framework;
using Rhizomode.Nodes.Audio;
using Rhizomode.Nodes.Generators;
using Rhizomode.Nodes.Math;
using Rhizomode.Nodes.Time;
using Rhizomode.SharedKernel;

namespace Rhizomode.Catalog.Tests
{
    /// <summary>
    /// Phase 4F Round A: 残りの float-param ノード 6 件への INodeParamAccessor 実装検証。
    /// </summary>
    public class AdditionalNodeParamAccessorTests
    {
        [Test]
        public void AudioTriggerNode_AllParamsSettable()
        {
            var node = new AudioTriggerNode("n1");
            var accessor = (INodeParamAccessor)node;

            Assert.IsTrue(accessor.TrySetParam("FreqMin", ParamValue.Float(50f)));
            Assert.IsTrue(accessor.TrySetParam("FreqMax", ParamValue.Float(8000f)));
            Assert.IsTrue(accessor.TrySetParam("Threshold", ParamValue.Float(0.3f)));

            accessor.TryGetParam("FreqMin", out var freqMin);
            accessor.TryGetParam("FreqMax", out var freqMax);
            accessor.TryGetParam("Threshold", out var threshold);

            Assert.AreEqual(50f, freqMin.AsFloat);
            Assert.AreEqual(8000f, freqMax.AsFloat);
            Assert.AreEqual(0.3f, threshold.AsFloat);
        }

        [Test]
        public void AudioTriggerNode_UnknownParam_ReturnsFalse()
        {
            var node = new AudioTriggerNode("n1");
            var accessor = (INodeParamAccessor)node;

            Assert.IsFalse(accessor.TrySetParam("Bogus", ParamValue.Float(1f)));
            Assert.IsFalse(accessor.TryGetParam("Bogus", out _));
        }

        [Test]
        public void AudioTriggerNode_WrongType_ReturnsFalse()
        {
            var node = new AudioTriggerNode("n1");
            var accessor = (INodeParamAccessor)node;

            Assert.IsFalse(accessor.TrySetParam("FreqMin", ParamValue.Bool(true)));
        }

        [Test]
        public void RemapNode_FourFloatParams_AllSettable()
        {
            var node = new RemapNode("n1");
            var accessor = (INodeParamAccessor)node;

            Assert.IsTrue(accessor.TrySetParam("InMin", ParamValue.Float(-1f)));
            Assert.IsTrue(accessor.TrySetParam("InMax", ParamValue.Float(2f)));
            Assert.IsTrue(accessor.TrySetParam("OutMin", ParamValue.Float(0f)));
            Assert.IsTrue(accessor.TrySetParam("OutMax", ParamValue.Float(100f)));
        }

        [Test]
        public void SmoothNode_Damping_NegativeClampedToZero()
        {
            var node = new SmoothNode("n1");
            var accessor = (INodeParamAccessor)node;

            accessor.TrySetParam("Damping", ParamValue.Float(-1f));
            accessor.TryGetParam("Damping", out var d);
            Assert.AreEqual(0f, d.AsFloat);
        }

        [Test]
        public void DelayNode_DelayTime_NegativeClampedToZero()
        {
            var node = new DelayNode("n1");
            var accessor = (INodeParamAccessor)node;

            accessor.TrySetParam("DelayTime", ParamValue.Float(-2f));
            accessor.TryGetParam("DelayTime", out var t);
            Assert.AreEqual(0f, t.AsFloat);
        }

        [Test]
        public void LfoNode_FrequencyAndAmplitude_Settable()
        {
            var node = new LfoNode("n1");
            var accessor = (INodeParamAccessor)node;

            Assert.IsTrue(accessor.TrySetParam("Frequency", ParamValue.Float(2.5f)));
            Assert.IsTrue(accessor.TrySetParam("Amplitude", ParamValue.Float(0.8f)));

            accessor.TryGetParam("Frequency", out var freq);
            accessor.TryGetParam("Amplitude", out var amp);
            Assert.AreEqual(2.5f, freq.AsFloat);
            Assert.AreEqual(0.8f, amp.AsFloat);
        }

        [Test]
        public void LfoNode_NegativeFrequency_ClampedToZero()
        {
            var node = new LfoNode("n1");
            var accessor = (INodeParamAccessor)node;

            accessor.TrySetParam("Frequency", ParamValue.Float(-1f));
            accessor.TryGetParam("Frequency", out var freq);
            Assert.AreEqual(0f, freq.AsFloat);
        }

        [Test]
        public void NoiseNode_SpeedAndAmplitude_Settable()
        {
            var node = new NoiseNode("n1");
            var accessor = (INodeParamAccessor)node;

            Assert.IsTrue(accessor.TrySetParam("Speed", ParamValue.Float(3f)));
            Assert.IsTrue(accessor.TrySetParam("Amplitude", ParamValue.Float(0.5f)));

            accessor.TryGetParam("Speed", out var speed);
            accessor.TryGetParam("Amplitude", out var amp);
            Assert.AreEqual(3f, speed.AsFloat);
            Assert.AreEqual(0.5f, amp.AsFloat);
        }
    }
}
