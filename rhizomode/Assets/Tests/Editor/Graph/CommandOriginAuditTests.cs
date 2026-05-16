#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Rhizomode.Graph.Tests
{
    /// <summary>
    /// CommandOrigin の adapter-affinity を source-scan で CI 検証する。
    /// memory <c>feedback_command_origin.md</c>「Boundary CI で検証」約束の充足
    /// (F-Vf-d.2 で Interaction.GraphAdapter 移送完了後の負債解消、2026-05-16)。
    /// </summary>
    /// <remarks>
    /// 各 <c>*.GraphAdapter</c> 配下の .cs を読み <c>CommandOrigin.X</c> パターンを抽出し、
    /// adapter ごとの期待値 (Interaction.GraphAdapter→Interaction、UI.GraphAdapter→Ui、…)
    /// と一致しない出現を fail とする。XML doc comment / inline comment は scan 対象から除外
    /// (`//` で始まる行をスキップ)。
    /// </remarks>
    public class CommandOriginAuditTests
    {
        private static readonly Regex OriginPattern = new(
            @"CommandOrigin\.(\w+)",
            RegexOptions.Compiled);

        [TestCase("Runtime/Interaction/GraphAdapter", "Interaction")]
        [TestCase("Runtime/UI/GraphAdapter",          "Ui")]
        [TestCase("Runtime/Audio/GraphAdapter",       "Audio")]
        [TestCase("Runtime/OscMidi/GraphAdapter",     "OscMidi")]
        [TestCase("Runtime/Ableton/GraphAdapter",     "Ableton")]
        [TestCase("Runtime/Scene/GraphAdapter",       "Scene")]
        public void Adapter_OnlyEmitsExpectedOrigin(string relativePath, string expectedOrigin)
        {
            var rootPath = Path.Combine(Application.dataPath, relativePath);
            if (!Directory.Exists(rootPath))
            {
                Assert.Inconclusive($"Adapter folder not found: {rootPath}");
                return;
            }

            var violations = new List<string>();
            foreach (var file in Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(file);
                foreach (var (origin, lineNo) in ScanOrigins(file))
                {
                    if (origin == expectedOrigin) continue;
                    violations.Add($"{fileName}:{lineNo}: CommandOrigin.{origin} (expected {expectedOrigin})");
                }
            }

            if (violations.Count > 0)
            {
                Assert.Fail(
                    $"Adapter '{relativePath}' must only emit CommandOrigin.{expectedOrigin}.\n"
                    + string.Join("\n", violations));
            }
        }

        [Test]
        public void CommandOriginTest_NotUsedInRuntime()
        {
            var runtimeRoot = Path.Combine(Application.dataPath, "Runtime");
            if (!Directory.Exists(runtimeRoot))
            {
                Assert.Inconclusive($"Runtime folder not found: {runtimeRoot}");
                return;
            }

            var violations = new List<string>();
            foreach (var file in Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                foreach (var (origin, lineNo) in ScanOrigins(file))
                {
                    if (origin != "Test") continue;
                    var fileName = Path.GetFileName(file);
                    violations.Add($"{fileName}:{lineNo}");
                }
            }

            if (violations.Count > 0)
            {
                Assert.Fail(
                    "CommandOrigin.Test must only be used in Tests asmdef. Found in Runtime:\n"
                    + string.Join("\n", violations));
            }
        }

        /// <summary>
        /// .cs ファイルから CommandOrigin.X の出現を抽出する。`//`/`///` で始まる comment 行は除外。
        /// </summary>
        private static IEnumerable<(string Origin, int LineNumber)> ScanOrigins(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", System.StringComparison.Ordinal)) continue;
                foreach (Match match in OriginPattern.Matches(line))
                {
                    yield return (match.Groups[1].Value, i + 1);
                }
            }
        }
    }
}
