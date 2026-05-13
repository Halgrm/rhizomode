#nullable enable

using NUnit.Framework;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Tests
{
    public class SharedKernelValueTypeTests
    {
        [Test]
        public void RzColor_DefaultEquality_FieldByField()
        {
            var a = new RzColor(0.5f, 0.5f, 0.5f, 1f);
            var b = new RzColor(0.5f, 0.5f, 0.5f, 1f);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void RzMath_Approximately_WithinEpsilon_True()
        {
            Assert.IsTrue(RzMath.Approximately(0.1f + 0.2f, 0.3f));
            Assert.IsTrue(RzMath.Approximately(
                new RzVector3(1f, 2f, 3f),
                new RzVector3(1f + 1e-7f, 2f, 3f)));
        }

        [Test]
        public void RzMath_IsFinite_NaNOrInf_False()
        {
            Assert.IsFalse(RzMath.IsFinite(float.NaN));
            Assert.IsFalse(RzMath.IsFinite(float.PositiveInfinity));
            Assert.IsTrue(RzMath.IsFinite(0f));
        }

        [Test]
        public void ParamValue_Variant_AsCorrectType()
        {
            Assert.AreEqual(1.5f, ParamValue.Float(1.5f).AsFloat);
            Assert.AreEqual(true, ParamValue.Bool(true).AsBool);
            Assert.AreEqual(RzColor.White, ParamValue.Color(RzColor.White).AsColor);
        }

        [Test]
        public void ParamValue_WrongVariant_Throws()
        {
            var v = ParamValue.Float(1f);
            Assert.Throws<System.InvalidOperationException>(() => _ = v.AsColor);
        }
    }
}
