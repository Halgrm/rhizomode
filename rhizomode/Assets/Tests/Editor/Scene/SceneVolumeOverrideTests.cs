#nullable enable

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rhizomode.Scene.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rhizomode.Scene.Tests
{
    /// <summary>
    /// <see cref="SceneVolumeOverride"/> の Apply / Revert で Volume が動的生成 /
    /// 破棄されることを検証。Component は inert (Apply は loader が呼ぶ)。
    /// </summary>
    public sealed class SceneVolumeOverrideTests
    {
        private readonly List<GameObject> _trash = new();
        private readonly List<Object> _assetTrash = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _trash)
                if (go != null) Object.DestroyImmediate(go);
            _trash.Clear();
            foreach (var asset in _assetTrash)
                if (asset != null) Object.DestroyImmediate(asset);
            _assetTrash.Clear();
        }

        [Test]
        public void Apply_AddsVolumeWithEnvProfile()
        {
            var prof = NewProfile();
            var ovr = NewOverride(prof, priority: 100, weight: 1f);

            InvokeApply(ovr);

            var vol = ovr.GetComponent<Volume>();
            Assert.NotNull(vol, "Apply で Volume が動的生成される");
            Assert.IsTrue(vol.isGlobal);
            Assert.AreSame(prof, vol.sharedProfile);
            Assert.AreEqual(100, vol.priority);
            Assert.AreEqual(1f, vol.weight);
        }

        [Test]
        public void Revert_DestroysGeneratedVolume()
        {
            var prof = NewProfile();
            var ovr = NewOverride(prof);

            InvokeApply(ovr);
            Assert.NotNull(ovr.GetComponent<Volume>());

            InvokeRevert(ovr);
            // DestroyImmediate で即 null 化
            Assert.IsNull(ovr.GetComponent<Volume>(),
                "Revert で動的 Volume が Destroy される (toggle ではなく破棄)");
        }

        [Test]
        public void Apply_NoOpWhenEnvProfileNull()
        {
            // profile 未設定なら Apply は何もしない (Volume 生成しない)
            var ovr = NewOverride(null);

            InvokeApply(ovr);

            Assert.IsNull(ovr.GetComponent<Volume>());
        }

        [Test]
        public void Apply_IdempotentWhenCalledTwice()
        {
            // 二重 Apply は no-op (二重 Volume 生成しない)
            var prof = NewProfile();
            var ovr = NewOverride(prof);

            InvokeApply(ovr);
            InvokeApply(ovr);

            var vols = ovr.GetComponents<Volume>();
            Assert.AreEqual(1, vols.Length, "二重 Apply で Volume が 2 個生成されないこと");
        }

        [Test]
        public void ApplyRevertCycle_DoesNotLeakAcrossLoads()
        {
            var prof = NewProfile();
            var ovr = NewOverride(prof);

            for (int i = 0; i < 3; i++)
            {
                InvokeApply(ovr);
                Assert.NotNull(ovr.GetComponent<Volume>(), $"cycle {i} Apply");
                InvokeRevert(ovr);
                // DestroyImmediate 経由なので即 null
                Assert.IsNull(ovr.GetComponent<Volume>(), $"cycle {i} Revert");
            }
        }

        // --- helpers ---

        private VolumeProfile NewProfile()
        {
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            _assetTrash.Add(prof);
            return prof;
        }

        private SceneVolumeOverride NewOverride(VolumeProfile? profile, int priority = 100, float weight = 1f)
        {
            var go = new GameObject("VolumeOverride_" + _trash.Count);
            _trash.Add(go);
            var ovr = go.AddComponent<SceneVolumeOverride>();
            SetPrivate(ovr, "envProfile", profile);
            SetPrivate(ovr, "priority", priority);
            SetPrivate(ovr, "weight", weight);
            return ovr;
        }

        // Component の Apply / Revert は internal なので reflection で呼ぶ
        private static void InvokeApply(SceneVolumeOverride ovr) => InvokeNonPublic(ovr, "Apply");
        private static void InvokeRevert(SceneVolumeOverride ovr) => InvokeNonPublic(ovr, "Revert");

        private static void InvokeNonPublic(object obj, string method)
        {
            var m = obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(m, $"non-public method '{method}' not found on {obj.GetType().Name}");
            m!.Invoke(obj, null);
        }

        private static void SetPrivate(object obj, string name, object? value)
        {
            var f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f, $"private field '{name}' not found on {obj.GetType().Name}");
            f!.SetValue(obj, value);
        }
    }
}
