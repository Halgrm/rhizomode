#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Rhizomode.Presentation.Layering;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rhizomode.Editor
{
    /// <summary>
    /// Plan 5-phase refactor の Phase 3 (2026-05-19) で導入。
    /// <c>[RequireMirrorHidden]</c> 属性付き component が attach されている scene / prefab
    /// GameObject の祖先 (self 含む) に <see cref="MirrorHiddenScope"/> が存在することを CI で強制する。
    /// </summary>
    /// <remarks>
    /// 検査ルール:
    /// <list type="bullet">
    ///   <item>R-MH-1: <c>[RequireMirrorHidden]</c> 付き Component の祖先に <c>MirrorHiddenScope</c> 必須</item>
    ///   <item>R-MH-2: TagManager.asset の layer index 8 名が <see cref="MirrorHiddenLayer.LayerName"/> と一致</item>
    /// </list>
    /// 哲学:
    /// NodeBase の <c>[NodeType]</c> + Default SO + typeName 完全一致による自己宣言型プロトコルと
    /// 同じパターン。「scope を忘れたら build が落ちる」状態を CI で固定し、新しい manager
    /// 追加者が規約を知らなくても安全になる。
    /// </remarks>
    internal sealed class MirrorHiddenSceneValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var violations = ValidateAll();
            if (violations.Length > 0)
            {
                throw new BuildFailedException(
                    $"[MirrorHiddenValidator] {violations.Length} violation(s) found:\n"
                    + string.Join("\n", violations));
            }
            Debug.Log("[MirrorHiddenValidator] All MirrorHidden coverage rules pass.");
        }

        [MenuItem("Tools/rhizomode/Validate MirrorHidden Coverage")]
        public static void RunFromMenu()
        {
            var violations = ValidateAll();
            if (violations.Length == 0)
            {
                Debug.Log("[MirrorHiddenValidator] All MirrorHidden coverage rules pass.");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog(
                        "MirrorHidden Coverage Validation",
                        "All MirrorHidden coverage rules pass.",
                        "OK");
                return;
            }

            Debug.LogError($"[MirrorHiddenValidator] {violations.Length} violation(s) found:");
            foreach (var v in violations) Debug.LogError($"[MirrorHiddenValidator]   • {v}");
            if (Application.isBatchMode) return;
            EditorUtility.DisplayDialog(
                "MirrorHidden Coverage Validation",
                $"{violations.Length} violation(s) — see Console for details.",
                "OK");
        }

        // ---------------------------------------------------------------
        // ValidateAll
        // ---------------------------------------------------------------

        private static string[] ValidateAll()
        {
            var violations = new List<string>();
            violations.AddRange(ValidateTagManagerLayerName());
            violations.AddRange(ValidateOpenScenes());
            violations.AddRange(ValidatePrefabs());
            return violations.ToArray();
        }

        // R-MH-2: TagManager.asset の layer index 8 が MirrorHidden であること
        private static IEnumerable<string> ValidateTagManagerLayerName()
        {
            var actual = LayerMask.LayerToName(8);
            if (actual != MirrorHiddenLayer.LayerName)
            {
                yield return $"TagManager.asset layer index 8 = '{actual}' "
                           + $"(expected '{MirrorHiddenLayer.LayerName}'). "
                           + $"MirrorHiddenLayer.LayerName と TagManager の同期が壊れています。";
            }
        }

        // R-MH-1: open している全 scene 上の RequireMirrorHidden 付き component を検査
        private static IEnumerable<string> ValidateOpenScenes()
        {
            var requiredTypes = GetRequireMirrorHiddenTypes();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var violation in WalkAndCheck(root, scene.path, requiredTypes))
                        yield return violation;
                }
            }
        }

        // R-MH-1: 全 prefab asset を走査して RequireMirrorHidden 付きを検査
        private static IEnumerable<string> ValidatePrefabs()
        {
            var requiredTypes = GetRequireMirrorHiddenTypes();
            var guids = AssetDatabase.FindAssets("t:prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null) continue;

                foreach (var violation in WalkAndCheck(prefabRoot, path, requiredTypes))
                    yield return violation;
            }
        }

        // 与えられた root 配下を再帰的に走査し、各 GameObject の component に
        // RequireMirrorHidden 属性付き型が含まれていれば、自身 / 祖先のいずれかに
        // MirrorHiddenScope があるかを検査する。
        private static IEnumerable<string> WalkAndCheck(GameObject node, string location, HashSet<Type> requiredTypes)
        {
            foreach (var comp in node.GetComponents<Component>())
            {
                if (comp == null) continue; // missing script
                var t = comp.GetType();
                if (!requiredTypes.Contains(t)) continue;

                if (!HasScopeOnSelfOrAncestor(node))
                {
                    yield return $"{location} :: '{GetGameObjectPath(node)}' has [{t.Name}] "
                               + $"but no MirrorHiddenScope on self or ancestors.";
                }
            }

            foreach (Transform child in node.transform)
            {
                foreach (var violation in WalkAndCheck(child.gameObject, location, requiredTypes))
                    yield return violation;
            }
        }

        private static bool HasScopeOnSelfOrAncestor(GameObject node)
        {
            var t = node.transform;
            while (t != null)
            {
                if (t.gameObject.GetComponent<MirrorHiddenScope>() != null) return true;
                t = t.parent;
            }
            return false;
        }

        // ---------------------------------------------------------------
        // Type reflection
        // ---------------------------------------------------------------

        // RequireMirrorHidden 属性が付いた MonoBehaviour サブクラスを起動時に一度集める。
        // Editor 単発実行なので reflection コストは無視できる。
        private static HashSet<Type>? _cachedRequiredTypes;

        private static HashSet<Type> GetRequireMirrorHiddenTypes()
        {
            if (_cachedRequiredTypes != null) return _cachedRequiredTypes;
            var set = new HashSet<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray()!; }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                    if (t.GetCustomAttributes(typeof(RequireMirrorHiddenAttribute), inherit: true).Length == 0) continue;
                    set.Add(t);
                }
            }
            _cachedRequiredTypes = set;
            return set;
        }

        private static string GetGameObjectPath(GameObject go)
        {
            var sb = new System.Text.StringBuilder(go.name);
            var t = go.transform.parent;
            while (t != null)
            {
                sb.Insert(0, t.name + "/");
                t = t.parent;
            }
            return sb.ToString();
        }
    }
}
