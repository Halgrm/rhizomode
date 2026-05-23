#nullable enable

using NUnit.Framework;
using Rhizomode.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rhizomode.UI.Tests
{
    /// <summary>
    /// WorldPanelHost の LOD まわり契約検証 (N5 黒画面化 regression)。
    /// SetUIActive は PanelSettings.targetTexture の toggle で実装されており、
    /// 無効化中は ChangeResolution が no-op となること、有効化サイクルで描画状態が
    /// 復帰することを確認する。
    /// </summary>
    public class WorldPanelHostLodGuardTests
    {
        private GameObject? _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
                _go = null;
            }
        }

        [Test]
        public void ChangeResolution_WithoutInitialize_IsNoOp()
        {
            _go = new GameObject("PanelHostTest");
            var host = _go.AddComponent<WorldPanelHost>();

            Assert.IsFalse(host.IsInitialized, "Initialize 前は未初期化");

            Assert.DoesNotThrow(() => host.ChangeResolution(64),
                "未初期化時の ChangeResolution は no-op で例外を出さない");
        }

        [Test]
        public void ChangeResolution_AppliesWidth_WhenUIActive()
        {
            _go = CreateInitializedHost(initialWidth: 256, initialHeight: 154);
            var host = _go.GetComponent<WorldPanelHost>();

            Assert.AreEqual(256, host.TextureWidth, "Initialize 直後は指定幅");
            Assert.IsTrue(host.IsUIActive, "Initialize 直後は UI active");

            host.ChangeResolution(128);

            Assert.AreEqual(128, host.TextureWidth, "UI active 時は ChangeResolution が幅を更新する");
        }

        [Test]
        public void ChangeResolution_IsNoOp_WhenUIInactive()
        {
            _go = CreateInitializedHost(initialWidth: 256, initialHeight: 154);
            var host = _go.GetComponent<WorldPanelHost>();

            host.SetUIActive(false);
            Assert.IsFalse(host.IsUIActive);

            host.ChangeResolution(96);

            Assert.AreEqual(256, host.TextureWidth,
                "UI 無効時の ChangeResolution は no-op (RT を Release/Create しない → 黒画面化を防ぐ)");
        }

        [Test]
        public void ChangeResolution_PreservesWidth_AcrossDisableEnableCycle()
        {
            _go = CreateInitializedHost(initialWidth: 256, initialHeight: 154);
            var host = _go.GetComponent<WorldPanelHost>();

            host.SetUIActive(false);
            host.ChangeResolution(96);
            Assert.AreEqual(256, host.TextureWidth, "無効時の resize は反映されない");

            host.SetUIActive(true);
            host.ChangeResolution(96);
            Assert.AreEqual(96, host.TextureWidth, "再有効化後は resize が反映される");
        }

        [Test]
        public void HasRenderedAtLeastOnce_FalseImmediatelyAfterInitialize()
        {
            // cue 透明ノード fix: Initialize 直後は UIToolkit がまだ paint していないため
            // HasRenderedAtLeastOnce は false。LOD はこの間 deactivate を抑止する契約。
            _go = CreateInitializedHost(initialWidth: 256, initialHeight: 154);
            var host = _go.GetComponent<WorldPanelHost>();

            Assert.IsFalse(host.HasRenderedAtLeastOnce,
                "Initialize 直後は layout+paint 未完了 — LOD は強制 active を維持する");
        }

        [Test]
        public void HasRenderedAtLeastOnce_RemainsFalse_WhenInactive()
        {
            // Initialize 後すぐ deactivate された panel は UIToolkit が描画しないため
            // 何フレーム経過しても HasRenderedAtLeastOnce は false のまま (LOD 強制 active 継続)。
            _go = CreateInitializedHost(initialWidth: 256, initialHeight: 154);
            var host = _go.GetComponent<WorldPanelHost>();

            host.SetUIActive(false);
            Assert.IsFalse(host.HasRenderedAtLeastOnce, "active でないと paint されない");
        }

        private static GameObject CreateInitializedHost(int initialWidth, int initialHeight)
        {
            var go = new GameObject("PanelHostTest");
            var host = go.AddComponent<WorldPanelHost>();
            var uxml = ScriptableObject.CreateInstance<VisualTreeAsset>();
            host.Initialize(uxml, styleSheet: null, textureWidth: initialWidth, textureHeight: initialHeight);
            return go;
        }
    }
}
