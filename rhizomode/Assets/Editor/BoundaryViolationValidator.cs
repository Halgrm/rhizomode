#nullable enable

using System;
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
    /// Phase 0.5 雛形 (Plan v5.3) — Boundary CI の Editor 同等チェック。
    ///
    /// 実 rules は Phase 1G で有効化する (`EnableRealChecks = true`)。
    /// それまでは Build pre-process / Asset postprocessor で skeleton mode 動作 (常に成功)。
    ///
    /// scripts/verify-asmdef-boundaries.sh と同等の検証を Unity Editor 内でも実行することで:
    /// - CI を待たずに Editor 上で違反を即検出
    /// - Build 直前に最終チェック
    ///
    /// 検出対象:
    /// - SharedKernel が UnityEngine / R3 を参照していないか
    /// - NodeCatalog.Contracts が UnityEngine を参照していないか
    /// - Audio.* が Ableton/OscMidi/Scene/UI を参照していないか
    /// - Interaction が Graph.* を参照していないか
    /// - UI.Presentation が Graph.* / NodeCatalog.Runtime を参照していないか
    /// - Nodes.Defaults が Nodes.* を参照していないか
    /// - VContainer は Bootstrap のみ
    /// - CommandOrigin の整合性 (Phase 5 以降に追加)
    /// </summary>
    internal sealed class BoundaryViolationValidator : IPreprocessBuildWithReport
    {
        /// <summary>
        /// Phase 1G で true に切り替え。それまでは雛形モード (常に成功)。
        /// </summary>
        private const bool EnableRealChecks = false;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!EnableRealChecks)
            {
                Debug.Log("[BoundaryValidator] Skeleton mode (Phase 0.5). Real checks disabled until Phase 1G.");
                return;
            }

            var violations = ValidateAll();
            if (violations.Length > 0)
            {
                throw new BuildFailedException(
                    $"[BoundaryValidator] {violations.Length} boundary violation(s) found:\n" +
                    string.Join("\n", violations));
            }
        }

        [MenuItem("Tools/rhizomode/Validate Asmdef Boundaries")]
        public static void RunFromMenu()
        {
            var violations = ValidateAll();
            if (violations.Length == 0)
            {
                Debug.Log("[BoundaryValidator] All boundary rules pass.");
                EditorUtility.DisplayDialog(
                    "Asmdef Boundary Validation",
                    "All boundary rules pass.",
                    "OK");
                return;
            }

            var msg = string.Join("\n", violations);
            Debug.LogError($"[BoundaryValidator] Violations:\n{msg}");
            EditorUtility.DisplayDialog(
                "Asmdef Boundary Validation",
                $"{violations.Length} violation(s):\n\n{msg}",
                "OK");
        }

        // ---------------------------------------------------------------
        // Rule implementations
        // ---------------------------------------------------------------

        private static string[] ValidateAll()
        {
            // Phase 0.5 雛形: 実 rules は Phase 1G 以降で実装。
            // ここに各 rule check method を追加していく。
            //
            // 例 (Phase 1G で有効化):
            //   var violations = new List<string>();
            //   violations.AddRange(CheckSharedKernelNoUnityEngine());
            //   violations.AddRange(CheckSharedKernelNoR3());
            //   violations.AddRange(CheckNodeCatalogContractsNoUnityEngine());
            //   violations.AddRange(CheckAudioCrossSystem());
            //   violations.AddRange(CheckAbletonContractsNoOscMidi());
            //   violations.AddRange(CheckInteractionNoGraph());
            //   violations.AddRange(CheckUIPresentationNoGraph());
            //   violations.AddRange(CheckNodesDefaultsBoundary());
            //   violations.AddRange(CheckVContainerOnlyBootstrap());
            //   violations.AddRange(CheckCommandOriginConsistency());
            //   return violations.ToArray();

            return Array.Empty<string>();
        }

        // ---------------------------------------------------------------
        // Future: rule helper methods (Phase 1G 以降に実装)
        // ---------------------------------------------------------------

        /// <summary>
        /// Asset Database から指定名の asmdef ファイルパスを取得する。
        /// 例: FindAsmdef("Rhizomode.SharedKernel") → "Assets/Runtime/SharedKernel/Rhizomode.SharedKernel.asmdef"
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

            // "references": [...] の中身を正規表現で抽出
            var match = Regex.Match(json, @"""references""\s*:\s*\[([^\]]*)\]", RegexOptions.Singleline);
            if (!match.Success) return Array.Empty<string>();

            var inner = match.Groups[1].Value;
            return Regex.Matches(inner, @"""([^""]+)""")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToArray();
        }

        /// <summary>
        /// 指定 asmdef が forbidden を参照していないか検証。違反があればメッセージを返す。
        /// </summary>
        private static string? CheckNoReference(string asmdefName, string forbiddenRef)
        {
            var path = FindAsmdef(asmdefName);
            if (path == null) return null; // asmdef がまだ存在しない (Phase 進行中) は OK

            var refs = GetReferences(path);
            if (refs.Contains(forbiddenRef))
                return $"{asmdefName} references {forbiddenRef} (forbidden)";

            return null;
        }
    }
}
