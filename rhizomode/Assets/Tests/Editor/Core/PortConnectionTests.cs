#nullable enable

using NUnit.Framework;
using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.Core.Tests
{
    public class PortConnectionTests
    {
        [Test]
        public void Float_Subscribe_PropagatesValue()
        {
            var output = new OutputPort<float>(ParamType.Float);
            var input = new InputPort<float>(ParamType.Float);

            float received = -1f;
            input.Observable.Subscribe(v => received = v);

            output.Subscribe(input);
            output.Emit(0.75f);

            Assert.AreEqual(0.75f, received, 0.0001f);
        }

        [Test]
        public void Color_Subscribe_PropagatesValue()
        {
            var output = new OutputPort<Color>(ParamType.Color);
            var input = new InputPort<Color>(ParamType.Color);

            Color received = Color.black;
            input.Observable.Subscribe(v => received = v);

            output.Subscribe(input);
            output.Emit(Color.red);

            Assert.AreEqual(Color.red, received);
        }

        [Test]
        public void Bool_Subscribe_PropagatesValue()
        {
            var output = new OutputPort<bool>(ParamType.Bool);
            var input = new InputPort<bool>(ParamType.Bool);

            bool received = false;
            input.Observable.Subscribe(v => received = v);

            output.Subscribe(input);
            output.Emit(true);

            Assert.IsTrue(received);
        }

        [Test]
        public void Dispose_StopsValuePropagation()
        {
            var output = new OutputPort<float>(ParamType.Float);
            var input = new InputPort<float>(ParamType.Float);

            float received = 0f;
            input.Observable.Subscribe(v => received = v);

            var subscription = output.Subscribe(input);
            output.Emit(1.0f);
            Assert.AreEqual(1.0f, received, 0.0001f);

            subscription.Dispose();
            output.Emit(2.0f);

            // Dispose後は値が変わらない
            Assert.AreEqual(1.0f, received, 0.0001f);
        }

        [Test]
        public void MultipleInputs_AllReceiveValue()
        {
            var output = new OutputPort<float>(ParamType.Float);
            var input1 = new InputPort<float>(ParamType.Float);
            var input2 = new InputPort<float>(ParamType.Float);

            float received1 = 0f, received2 = 0f;
            input1.Observable.Subscribe(v => received1 = v);
            input2.Observable.Subscribe(v => received2 = v);

            output.Subscribe(input1);
            output.Subscribe(input2);
            output.Emit(0.5f);

            Assert.AreEqual(0.5f, received1, 0.0001f);
            Assert.AreEqual(0.5f, received2, 0.0001f);
        }

        [Test]
        public void MultipleOutputs_MergeOnInput()
        {
            var output1 = new OutputPort<float>(ParamType.Float);
            var output2 = new OutputPort<float>(ParamType.Float);
            var input = new InputPort<float>(ParamType.Float);

            float received = 0f;
            input.Observable.Subscribe(v => received = v);

            output1.Subscribe(input);
            output2.Subscribe(input);

            output1.Emit(0.3f);
            Assert.AreEqual(0.3f, received, 0.0001f);

            // 最後に発行された値が勝つ
            output2.Emit(0.7f);
            Assert.AreEqual(0.7f, received, 0.0001f);
        }

        [Test]
        public void EmitMultipleValues_ReceivesLatest()
        {
            var output = new OutputPort<float>(ParamType.Float);
            var input = new InputPort<float>(ParamType.Float);

            float received = 0f;
            input.Observable.Subscribe(v => received = v);

            output.Subscribe(input);
            output.Emit(0.1f);
            output.Emit(0.2f);
            output.Emit(0.3f);

            Assert.AreEqual(0.3f, received, 0.0001f);
        }
    }
}
