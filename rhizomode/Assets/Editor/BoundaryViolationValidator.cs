#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Rhizomode.Editor
{
    /// <summary>
    /// Phase 4F Round D: Boundary CI の Editor 同等チェック (限定有効化)。
    ///
    /// scripts/verify-asmdef-boundaries.sh と同等の検証を Unity Editor 内でも実行することで:
    /// - CI を待たずに Editor 上で違反を即検出
    /// - Build 直前に最終チェック
    ///
    /// 限定有効化 (Plan v5.3 Phase 1G 全 rule は Phase 5/7 で違反解消後):
    /// - 現状違反なし のルールのみ enable
    /// - Phase 5 で Graph.Model→Serialization 解消後、Phase 7 で UI/Interaction→Graph 解消後に追加 rule 有効化
    /// - 2026-05-16: Audio.Analysis (5c) + Ableton.Session (5b) を追加 (現状違反なし)
    ///
    /// 未有効化 rule (Phase 5/7 cleanup 後に追加予定、いずれも現状で実違反あり):
    /// - Audio.GraphAdapter ⊄ UI.Presentation (Audio/GraphAdapter/Rhizomode.Audio.GraphAdapter.asmdef:12)
    /// - Rhizomode.Interaction ⊄ Graph.* (Interaction/Rhizomode.Interaction.asmdef:10-12)
    /// - Rhizomode.UI.Presentation ⊄ NodeCatalog.Runtime (UI/Presentation/Rhizomode.UI.Presentation.asmdef:9)
    /// </summary>
    internal sealed class BoundaryViolationValidator : IPreprocessBuildWithReport
    {
        /// <summary>
        /// Round D で true に切り替え (限定ルールのみ)。Phase 5/7 で違反解消後に追加 rule。
        /// </summary>
        private const bool EnableRealChecks = true;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!EnableRealChecks)
            {
                Debug.Log("[BoundaryValidator] Skeleton mode. Real checks disabled.");
                return;
            }

            var violations = ValidateAll();
            if (violations.Length > 0)
            {
                throw new BuildFailedException(
                    $"[BoundaryValidator] {violations.Length} boundary violation(s) found:\n" +
                    string.Join("\n", violations));
            }
            Debug.Log($"[BoundaryValidator] All {EnabledRuleCount} boundary rules pass.");
        }

        [MenuItem("Tools/rhizomode/Validate Asmdef Boundaries")]
        public static void RunFromMenu()
        {
            var violations = ValidateAll();
            if (violations.Length == 0)
            {
                Debug.Log($"[BoundaryValidator] All {EnabledRuleCount} boundary rules pass.");
                EditorUtility.DisplayDialog(
                    "Asmdef Boundary Validation",
                    $"All {EnabledRuleCount} boundary rules pass.",
                    "OK");
                return;
            }

            Debug.LogError($"[BoundaryValidator] {violations.Length} violation(s) found:");
            foreach (var v in violations)
            {
                Debug.LogError($"[BoundaryValidator]   • {v}");
            }
            // モーダル dialog は MCP 経由実行で session を切るため Editor 専用に切替
            if (UnityEngine.Application.isBatchMode) return;
            EditorUtility.DisplayDialog(
                "Asmdef Boundary Validation",
                $"{violations.Length} violation(s) — see Console for details.",
                "OK");
        }

        // ---------------------------------------------------------------
        // Rule implementations (限定有効化、現状違反なし)
        // ---------------------------------------------------------------

        private const int EnabledRuleCount = 10;

        private static string[] ValidateAll()
        {
            var violations = new List<string>();

            // Rule 1: SharedKernel must not reference UnityEngine.* (noEngineReferences=true で保証されるが念のため)
            violations.AddRange(CheckNoReferences(
                "Rhizomode.SharedKernel",
                forbidden: new[] { "UnityEngine.CoreModule", "Unity.InputSystem", "Unity.Cinemachine" }));

            // Rule 2: SharedKernel must not reference R3
            violations.AddRange(CheckNoReferences(
                "Rhizomode.SharedKernel",
                forbidden: new[] { "R3.Unity" }));

            // Rule 3: NodeCatalog.Contracts must not reference UnityEngine 系 / Graph.* (pure contract)
            violations.AddRange(CheckNoReferences(
                "Rhizomode.NodeCatalog.Contracts",
                forbidden: new[] { "Rhizomode.Graph.Model", "Rhizomode.Graph.Serialization", "R3.Unity" }));

            // Rule 4: Audio.Contracts ⊄ Ableton/OscMidi/Scene/UI
            violations.AddRange(CheckNoReferences(
                "Rhizomode.Audio.Contracts",
                forbidden: new[]
                {
                    "Rhizomode.Ableton.Contracts", "Rhizomode.Ableton.Transport", "Rhizomode.Ableton.Session",
                    "Rhizomode.OscMidi.Contracts", "Rhizomode.OscMidi.Transport",
                    "Rhizomode.Scene.Contracts", "Rhizomode.Scene.Runtime",
                    "Rhizomode.UI", "Rhizomode.UI.Presentation", "Rhizomode.UI.Contracts"
                }));

            // Rule 5: Ableton.Contracts ⊄ OscMidi (clear DDD boundary)
            violations.AddRange(CheckNoReferences(
                "Rhizomode.Ableton.Contracts",
                forbidden: new[] { "Rhizomode.OscMidi.Transport", "Rhizomode.OscMidi.GraphAdapter" }));

            // Rule 5b (2026-05-16): Ableton.Session ⊄ OscMidi.* (Plan v5.3 hard rule)
            // Session.asmdef は SharedKernel / Ableton.Contracts / R3.Unity のみで現状違反なし。
            violations.AddRange(CheckNoReferences(
                "Rhizomode.Ableton.Session",
                forbidden: new[]
                {
                    "Rhizomode.OscMidi.Contracts", "Rhizomode.OscMidi.Transport",
                    "Rhizomode.OscMidi.GraphAdapter"
                }));

            // Rule 5c (2026-05-16): Audio.Analysis ⊄ Ableton/OscMidi/Scene/UI 系
            // Analysis.asmdef は SharedKernel / Audio.Contracts / Lasp.Runtime のみで現状違反なし。
            // Audio.GraphAdapter→UI.Presentation の意図的残違反は Phase 9 Round F cleanup 完了後に
            // GraphAdapter 用 rule を追加する。
            violations.AddRange(CheckNoReferences(
                "Rhizomode.Audio.Analysis",
                forbidden: new[]
                {
                    "Rhizomode.Ableton.Contracts", "Rhizomode.Ableton.Transport", "Rhizomode.Ableton.Session", "Rhizomode.Ableton.GraphAdapter",
                    "Rhizomode.OscMidi.Contracts", "Rhizomode.OscMidi.Transport", "Rhizomode.OscMidi.GraphAdapter",
                    "Rhizomode.Scene.Contracts", "Rhizomode.Scene.Runtime", "Rhizomode.Scene.GraphAdapter",
                    "Rhizomode.UI", "Rhizomode.UI.Contracts", "Rhizomode.UI.Presentation", "Rhizomode.UI.GraphAdapter"
                }));

            // Rule 6: Nodes.Defaults ⊄ Nodes.* concrete asmdefs (Plan v5.3-2 boundary)
            violations.AddRange(CheckNoReferences(
                "Rhizomode.Nodes.Defaults",
                forbidden: new[]
                {
                    "Rhizomode.Nodes.Standard", "Rhizomode.Nodes.Audio", "Rhizomode.Nodes.OscMidi",
                    "Rhizomode.Nodes.Ableton", "Rhizomode.Nodes.Scene"
                }));

            // Rule 7: Bootstrap is the ONLY asmdef referencing VContainer
            violations.AddRange(CheckVContainerOnlyBootstrap());

            // Rule 8 (Phase 9 Round F3): UI.Presentation ⊄ Graph.Model / Graph.Serialization
            // Plan v5.3 完了条件「UI.Presentation 配下から Graph.* 参照 0 件」を CI で固定。
            // Round E (E1-E6) で全 .cs source および asmdef references から物理撤去済。
            violations.AddRange(CheckNoReferences(
                "Rhizomode.UI.Presentation",
                forbidden: new[] { "Rhizomode.Graph.Model", "Rhizomode.Graph.Serialization", "Rhizomode.Graph.Runtime", "Rhizomode.Graph.Events", "Rhizomode.Graph.Mutation", "Rhizomode.Graph.Query", "Rhizomode.Graph.Snapshot", "Rhizomode.Graph.CatalogBridge" }));

            return violations.ToArray();
        }

        // ---------------------------------------------------------------
        // Rule helpers
        // ---------------------------------------------------------------

        private static IEnumerable<string> CheckNoReferences(string asmdefName, string[] forbidden)
        {
            var path = FindAsmdef(asmdefName);
            if (path == null) yield break; // asmdef がまだ存在しない (Phase 進行中) は OK

            var refs = GetReferences(path);
            foreach (var forbiddenRef in forbidden)
            {
                if (refs.Contains(forbiddenRef))
                {
                    yield return $"{asmdefName} references {forbiddenRef} (forbidden)";
                }
            }
        }

        private static IEnumerable<string> CheckVContainerOnlyBootstrap()
        {
            // Rhizomode.* の asmdef のみを対象 (package の VContainer.Editor 等は除外)
            var guids = AssetDatabase.FindAssets("t:asmdef");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("Rhizomode.", StringComparison.Ordinal)) continue;
                if (name == "Rhizomode.Bootstrap") continue; // Bootstrap のみ許可

                var refs = GetReferences(path);
                if (refs.Contains("VContainer") || refs.Contains("VContainer.Unity"))
                {
                    yield return $"{name} references VContainer (only Bootstrap is allowed)";
                }
            }
        }

        /// <summary>
        /// Asset Database から指定名の asmdef ファイルパスを取得する。
        /// </summary>
        private static string? FindAsmdef(string asmdefName)
        {
            var guids = AssetDatabase.FindAssets($"{asmdefName} t:asmdef");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == asmdefName)
                    return path;
            }
            return null;
        }

        /// <summary>
        /// asmdef ファイルから references 配列を抽出する。
        /// </summary>
        private static string[] GetReferences(string asmdefPath)
        {
            if (!File.Exists(asmdefPath)) return Array.Empty<string>();
            var json = File.ReadAllText(asmdefPath);

            var match = Regex.Match(json, @"""references""\s*:\s*\[([^\]]*)\]", RegexOptions.Singleline);
            if (!match.Success) return Array.Empty<string>();

            var inner = match.Groups[1].Value;
            return Regex.Matches(inner, @"""([^""]+)""")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToArray();
        }
    }
}
