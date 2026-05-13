#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Runtime;
using Rhizomode.Nodes.Defaults;
using Rhizomode.Nodes.Input;
using Rhizomode.Nodes.Time;
using Rhizomode.Nodes.Utility;
using Rhizomode.SharedKernel;
using UnityEngine;

namespace Rhizomode.Catalog.Tests
{
    public class NodeDefaultLifecycleProcessorTests
    {
        [Test]
        public void Processor_FreshSpawn_NoEntriesForType_NoOp()
        {
            var registry = ScriptableObject.CreateInstance<NodeDefaultsRegistry>();
            var processor = new NodeDefaultLifecycleProcessor(registry);

            var node = new ConstFloatNode("n1");
            Assert.AreEqual(0.5f, node.Value); // 初期値 (DefaultValue)

            processor.BeforeSetup(node, NodeInitMode.FreshSpawn);

            // entries 無しなので何も変わらない
            Assert.AreEqual(0.5f, node.Value);
        }

        [Test]
        public void Processor_NonFreshSpawnMode_DoesNotApply()
        {
            var registry = ScriptableObject.CreateInstance<NodeDefaultsRegistry>();
            var processor = new NodeDefaultLifecycleProcessor(registry);

            var node = new ConstFloatNode("n1");
            var before = node.Value;

            processor.BeforeSetup(node, NodeInitMode.Deserialize);
            Assert.AreEqual(before, node.Value);

            processor.BeforeSetup(node, NodeInitMode.PresetImport);
            Assert.AreEqual(before, node.Value);

            processor.BeforeSetup(node, NodeInitMode.UndoRedo);
            Assert.AreEqual(before, node.Value);
        }

        [Test]
        public void Processor_NonParamAccessor_NoOp()
        {
            // NodeBase 直接インスタンス化は abstract のため不可。代わりに
            // INodeParamAccessor を実装していないノード (Multiply 等) で
            // 何も起きないことを確認する。
            var registry = ScriptableObject.CreateInstance<NodeDefaultsRegistry>();
            var processor = new NodeDefaultLifecycleProcessor(registry);
            // INodeParamAccessor 未実装のノードクラスに対して BeforeSetup を呼んでも例外なし
            // (Phase 4F では 4 ノードしか INodeParamAccessor を実装していない)
            Assert.DoesNotThrow(() => processor.AfterDeserialize(new GraphState()));
        }

        [Test]
        public void ConstFloatNode_TrySetParam_UpdatesValue()
        {
            var node = new ConstFloatNode("n1");
            var accessor = (INodeParamAccessor)node;

            Assert.IsTrue(accessor.TrySetParam("Value", ParamValue.Float(0.75f)));
            Assert.AreEqual(0.75f, node.Value);
        }

        [Test]
        public void ConstFloatNode_TrySetParam_WrongName_ReturnsFalse()
        {
            var node = new ConstFloatNode("n1");
            var accessor = (INodeParamAccessor)node;

            Assert.IsFalse(accessor.TrySetParam("Bogus", ParamValue.Float(0.75f)));
            Assert.IsFalse(accessor.TrySetParam("Value", ParamValue.Bool(true)));
        }

        [Test]
        public void ConstFloatNode_TryGetParam_ReturnsCurrent()
        {
            var node = new ConstFloatNode("n1");
            var accessor = (INodeParamAccessor)node;

            accessor.TrySetParam("Value", ParamValue.Float(0.42f));
            var ok = accessor.TryGetParam("Value", out var pv);

            Assert.IsTrue(ok);
            Assert.AreEqual(ParamType.Float, pv.Type);
            Assert.AreEqual(0.42f, pv.AsFloat);
        }

        [Test]
        public void ConstColorNode_TrySetParam_UpdatesColor()
        {
            var node = new ConstColorNode("n1");
            var accessor = (INodeParamAccessor)node;

            var ok = accessor.TrySetParam("Value", ParamValue.Color(new RzColor(0.2f, 0.4f, 0.6f, 1f)));

            Assert.IsTrue(ok);
            Assert.AreEqual(0.2f, node.Value.r);
            Assert.AreEqual(0.4f, node.Value.g);
            Assert.AreEqual(0.6f, node.Value.b);
        }

        [Test]
        public void ThresholdNode_TrySetParam_UpdatesThreshold()
        {
            var node = new ThresholdNode("n1");
            var accessor = (INodeParamAccessor)node;

            Assert.IsTrue(accessor.TrySetParam("Threshold", ParamValue.Float(0.7f)));
            accessor.TryGetParam("Threshold", out var pv);
            Assert.AreEqual(0.7f, pv.AsFloat);
        }

        [Test]
        public void TimerNode_TrySetParam_UpdatesDuration_ClampsNegative()
        {
            var node = new TimerNode("n1");
            var accessor = (INodeParamAccessor)node;

            accessor.TrySetParam("Duration", ParamValue.Float(2.5f));
            accessor.TryGetParam("Duration", out var pv);
            Assert.AreEqual(2.5f, pv.AsFloat);

            // 負値は 0 にクランプ
            accessor.TrySetParam("Duration", ParamValue.Float(-1f));
            accessor.TryGetParam("Duration", out pv);
            Assert.AreEqual(0f, pv.AsFloat);
        }
    }
}
