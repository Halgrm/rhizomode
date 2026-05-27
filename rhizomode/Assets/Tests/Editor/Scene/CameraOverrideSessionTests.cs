#nullable enable

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rhizomode.Scene.Runtime;
using UnityEngine;

namespace Rhizomode.Scene.Tests
{
    /// <summary>
    /// <see cref="CameraOverrideSession"/> + <see cref="SceneCameraOverride"/> の
    /// Apply / Revert ラウンドトリップを検証。loader-owned session が snapshot を
    /// 持つので components は inert (Apply を component から呼ばない) でも動く。
    /// </summary>
    public sealed class CameraOverrideSessionTests
    {
        private readonly List<GameObject> _trash = new();
        private CameraOverrideSession? _session;

        [TearDown]
        public void TearDown()
        {
            _session?.Dispose();
            _session = null;
            foreach (var go in _trash)
                if (go != null) Object.DestroyImmediate(go);
            _trash.Clear();
        }

        [Test]
        public void Apply_OverridesCameraClearAndBg()
        {
            var cam = NewCam(CameraClearFlags.Skybox, Color.gray);
            var ovr = NewOverride(CameraClearFlags.SolidColor, Color.red, cam);

            _session = new CameraOverrideSession();
            _session.Apply(new[] { ovr });

            Assert.AreEqual(CameraClearFlags.SolidColor, cam.clearFlags);
            Assert.AreEqual(Color.red, cam.backgroundColor);
        }

        [Test]
        public void Revert_RestoresOriginalState()
        {
            var cam = NewCam(CameraClearFlags.Skybox, Color.gray);
            var ovr = NewOverride(CameraClearFlags.SolidColor, Color.red, cam);

            _session = new CameraOverrideSession();
            _session.Apply(new[] { ovr });
            _session.Revert();

            Assert.AreEqual(CameraClearFlags.Skybox, cam.clearFlags);
            Assert.AreEqual(Color.gray, cam.backgroundColor);
        }

        [Test]
        public void Apply_FirstSnapshotWins_WhenMultipleOverridesHitSameCamera()
        {
            // 同一 camera を 2 つの override で重ねたとき、最初の base state を保持する。
            // (2nd の Apply が "現在 = 1st の Apply 後の状態" を snapshot しないこと)
            var cam = NewCam(CameraClearFlags.Skybox, Color.blue);
            var o1 = NewOverride(CameraClearFlags.SolidColor, Color.red, cam);
            var o2 = NewOverride(CameraClearFlags.Depth, Color.green, cam);

            _session = new CameraOverrideSession();
            _session.Apply(new[] { o1, o2 });
            _session.Revert();

            Assert.AreEqual(CameraClearFlags.Skybox, cam.clearFlags, "最初の Apply 時の base に戻る");
            Assert.AreEqual(Color.blue, cam.backgroundColor);
        }

        [Test]
        public void Apply_TolerantsNullTargetEntries()
        {
            // missing reference (= null) を Apply は警告 + skip し、有効な camera は処理する。
            var cam = NewCam(CameraClearFlags.Skybox, Color.gray);
            var ovr = NewOverride(CameraClearFlags.SolidColor, Color.red, null!, cam);

            _session = new CameraOverrideSession();
            Assert.DoesNotThrow(() => _session.Apply(new[] { ovr }));
            Assert.AreEqual(CameraClearFlags.SolidColor, cam.clearFlags, "有効な camera は処理される");
        }

        [Test]
        public void Dispose_AliasOfRevert()
        {
            var cam = NewCam(CameraClearFlags.Skybox, Color.gray);
            var ovr = NewOverride(CameraClearFlags.SolidColor, Color.red, cam);

            using (var s = new CameraOverrideSession())
            {
                s.Apply(new[] { ovr });
                Assert.AreEqual(Color.red, cam.backgroundColor);
            } // Dispose

            Assert.AreEqual(Color.gray, cam.backgroundColor);
        }

        [Test]
        public void Apply_AppliesToMarkerCamerasInAdditionToTargets()
        {
            // marker camera (base scene 由来想定) と explicit target を同時に上書きするケース。
            var explicitCam = NewCam(CameraClearFlags.Skybox, Color.gray);
            var markerCam   = NewCam(CameraClearFlags.Skybox, Color.white);
            var ovr = NewOverride(CameraClearFlags.SolidColor, Color.red, explicitCam);

            _session = new CameraOverrideSession();
            _session.Apply(new[] { ovr }, new[] { markerCam });

            Assert.AreEqual(Color.red, explicitCam.backgroundColor, "explicit target");
            Assert.AreEqual(Color.red, markerCam.backgroundColor, "marker camera");

            _session.Revert();
            Assert.AreEqual(Color.gray, explicitCam.backgroundColor);
            Assert.AreEqual(Color.white, markerCam.backgroundColor);
        }

        [Test]
        public void Apply_MarkerCameraOnly_WhenTargetsEmpty()
        {
            // env scene 側の targets が空でも marker camera だけで適用される
            // (cross-scene wiring の典型ケース)。
            var markerCam = NewCam(CameraClearFlags.Skybox, Color.green);
            var ovr = NewOverride(CameraClearFlags.SolidColor, Color.black /* no explicit targets */);

            _session = new CameraOverrideSession();
            _session.Apply(new[] { ovr }, new[] { markerCam });

            Assert.AreEqual(Color.black, markerCam.backgroundColor);
            _session.Revert();
            Assert.AreEqual(Color.green, markerCam.backgroundColor);
        }

        [Test]
        public void ReloadCycle_NoLeakedSnapshot()
        {
            // load → unload → load → unload で snapshot が leak しないこと。
            var cam = NewCam(CameraClearFlags.Skybox, Color.gray);
            var ovr1 = NewOverride(CameraClearFlags.SolidColor, Color.red, cam);
            var ovr2 = NewOverride(CameraClearFlags.SolidColor, Color.blue, cam);

            var s1 = new CameraOverrideSession();
            s1.Apply(new[] { ovr1 });
            s1.Dispose();
            Assert.AreEqual(Color.gray, cam.backgroundColor);

            var s2 = new CameraOverrideSession();
            s2.Apply(new[] { ovr2 });
            Assert.AreEqual(Color.blue, cam.backgroundColor);
            s2.Dispose();
            Assert.AreEqual(Color.gray, cam.backgroundColor,
                "2 度目の cycle でも 1 度目の base state に戻る (= snapshot pollution 無し)");
        }

        // --- helpers ---

        private Camera NewCam(CameraClearFlags flags, Color bg)
        {
            var go = new GameObject("TestCam_" + _trash.Count);
            _trash.Add(go);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = flags;
            cam.backgroundColor = bg;
            return cam;
        }

        private SceneCameraOverride NewOverride(CameraClearFlags flags, Color bg, params Camera[] targets)
        {
            var go = new GameObject("Override_" + _trash.Count);
            _trash.Add(go);
            var ovr = go.AddComponent<SceneCameraOverride>();
            // serialized private fields をテストから設定するため reflection を使う
            SetPrivate(ovr, "clearFlags", flags);
            SetPrivate(ovr, "backgroundColor", bg);
            SetPrivate(ovr, "targets", new List<Camera>(targets));
            return ovr;
        }

        private static void SetPrivate(object obj, string name, object value)
        {
            var f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f, $"private field '{name}' not found on {obj.GetType().Name}");
            f!.SetValue(obj, value);
        }
    }
}
