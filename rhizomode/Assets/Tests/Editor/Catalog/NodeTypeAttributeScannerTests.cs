#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;

namespace Rhizomode.Catalog.Tests
{
    public class NodeTypeAttributeScannerTests
    {
        /// <summary>
        /// Phase 4 で [NodeType] 属性を付与した static ノード件数の期待値。
        /// 動的タイプ (SceneTrigger 3 個 + SceneObject + VFX/Shader/Object3D) は除外。
        /// </summary>
        private static readonly HashSet<string> ExpectedStaticTypeNames = new()
        {
            // Nodes.Audio (7)
            "AudioDevice", "AudioTrigger", "AudioBand", "BeatDetector", "TapTempo",
            "AudioMonitor", "SpectrumMonitor",
            // Nodes.OscMidi (2)
            "OscReceiver", "MidiCC",
            // Nodes.Ableton (4)
            "AbletonTempo", "AbletonTransport", "AbletonTrackVolume", "AbletonClipFire",
            // Nodes.Scene (2, SceneTrigger* は動的のため除外)
            "SceneSwitch", "CameraSwitch",
            // Nodes.Standard / Generators (3)
            "ConstFloat", "ConstColor", "Trigger",
            // Nodes.Standard / Math (4)
            "Multiply", "Add", "Remap", "Smooth",
            // Nodes.Standard / Math.Conversion (6)
            "HzToNote", "NoteToHz", "BpmToSec", "SecToBpm", "LinearToDb", "DbToLinear",
            // Nodes.Standard / Time (3)
            "Time", "Timer", "Delay",
            // Nodes.Standard / Generators (2)
            "LFO", "Noise",
            // Nodes.Standard / Utility (10)
            "Threshold", "Toggle", "FloatMonitor", "BoolMonitor", "ColorMonitor",
            "ColorToFloats", "FloatsToColor", "ColorToHSV", "HSVToColor", "Count",
            // Nodes.Video (1)
            "NdiReceiver"
        };

        [Test]
        public void Scanner_FindsAllStaticNodeTypes()
        {
            var scanner = new NodeTypeAttributeScanner();
            var registrations = scanner.Scan();
            var foundTypeNames = registrations.Select(r => r.Display.TypeName).ToHashSet();

            // ExpectedStaticTypeNames should be a subset of found
            var missing = ExpectedStaticTypeNames.Except(foundTypeNames).ToList();
            Assert.IsEmpty(missing, $"Missing typeName attributes: {string.Join(", ", missing)}");
        }

        [Test]
        public void Scanner_FindsExactlyExpectedStaticCount()
        {
            var scanner = new NodeTypeAttributeScanner();
            var registrations = scanner.Scan();
            var foundTypeNames = registrations.Select(r => r.Display.TypeName).ToHashSet();

            // 動的 (SceneTrigger* / SceneObject / VFX_/Shader_/Object3D_) は含まれないはず
            var extra = foundTypeNames.Except(ExpectedStaticTypeNames).ToList();
            Assert.IsEmpty(extra, $"Unexpected typeName attributes: {string.Join(", ", extra)}");

            Assert.AreEqual(ExpectedStaticTypeNames.Count, foundTypeNames.Count);
        }

        [Test]
        public void Factory_CanCreateAllStaticTypes()
        {
            var scanner = new NodeTypeAttributeScanner();
            var registrations = scanner.Scan();
            var factory = new AttributeScannerNodeFactory(registrations);

            foreach (var typeName in ExpectedStaticTypeNames)
            {
                Assert.IsTrue(factory.CanCreate(typeName), $"Factory cannot create {typeName}");
                var node = factory.Create(typeName, $"test-{typeName}");
                Assert.IsNotNull(node, $"Factory.Create returned null for {typeName}");
                Assert.AreEqual(typeName, node!.NodeType, $"NodeType mismatch for {typeName}");
            }
        }

        [Test]
        public void Factory_UnknownTypeName_ReturnsNull()
        {
            var scanner = new NodeTypeAttributeScanner();
            var factory = new AttributeScannerNodeFactory(scanner.Scan());

            Assert.IsFalse(factory.CanCreate("ThisDoesNotExist"));
            Assert.IsNull(factory.Create("ThisDoesNotExist", "id1"));
        }

        [Test]
        public void Scanner_DisplayInfo_HasNonEmptyLabel()
        {
            var scanner = new NodeTypeAttributeScanner();
            foreach (var reg in scanner.Scan())
            {
                Assert.IsNotEmpty(reg.Display.Label, $"Empty label for {reg.Display.TypeName}");
            }
        }
    }
}
