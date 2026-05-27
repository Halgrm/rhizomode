#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Rhizomode.Scene.Runtime;

namespace Rhizomode.Scene.EditorTools
{
    /// <summary>
    /// Build Settings に登録された環境シーンが rhizomode の "env scene 契約" を
    /// 守っているかを Tools menu から検証する Editor utility。Plan v0.3 §環境シーン契約。
    /// </summary>
    /// <remarks>
    /// <para>契約 (`docs/SCENE_AUTHORING.md` 参照):</para>
    /// <list type="bullet">
    ///   <item>必須: <see cref="SceneEnvironment"/> 1 個 (RenderSettings)</item>
    ///   <item>強く推奨: <see cref="SceneVolumeOverride"/> 1 個 (post-FX)</item>
    ///   <item>推奨: <see cref="SceneCameraOverride"/> 1 個 (camera clear)</item>
    /// </list>
    ///
    /// <para>違反検出のレベル分け:</para>
    /// <list type="bullet">
    ///   <item><see cref="SceneEnvironment"/> 欠落 → <b>error</b> (全 env)</item>
    ///   <item><see cref="SceneVolumeOverride"/> 欠落 → <b>warning</b> (全 env)、
    ///     launch-critical scene (<see cref="LaunchCriticalSceneNames"/>) は <b>error</b></item>
    ///   <item><see cref="SceneCameraOverride"/> 欠落 → <b>info</b></item>
    /// </list>
    /// </remarks>
    public static class EnvSceneValidator
    {
        /// <summary>launch-critical = SceneVolumeOverride 欠落も error にする env scene 名。</summary>
        private static readonly HashSet<string> LaunchCriticalSceneNames = new() { "concrete" };

        /// <summary>SampleScene の正規パス。env scene の母艦として扱う。</summary>
        private const string BaseScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Rhizomode/Validate Env Scenes")]
        public static void Validate()
        {
            var openScene = EditorSceneManager.GetActiveScene().path;

            var sceneAssetPaths = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .Where(p => p != BaseScenePath)
                .Where(p => p.StartsWith("Assets/Scenes/"))
                .ToArray();

            int errorCount = 0;
            int warnCount = 0;
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== rhizomode env scene validation ===");
            report.AppendLine($"base: {BaseScenePath}");
            report.AppendLine($"env scenes (Build Settings enabled, excl. base): {sceneAssetPaths.Length}");
            report.AppendLine();

            foreach (var path in sceneAssetPaths)
            {
                var sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
                var (env, vol, cam) = InspectScene(path);

                report.AppendLine($"[{sceneName}]");
                report.AppendLine($"  SceneEnvironment x {env}");
                report.AppendLine($"  SceneVolumeOverride x {vol}");
                report.AppendLine($"  SceneCameraOverride x {cam}");

                if (env == 0)
                {
                    Debug.LogError($"[EnvSceneValidator] '{sceneName}' has NO SceneEnvironment " +
                                   "(env scene 契約違反、必須)。RenderSettings が base へ fallback。");
                    errorCount++;
                }
                if (vol == 0)
                {
                    if (LaunchCriticalSceneNames.Contains(sceneName))
                    {
                        Debug.LogError($"[EnvSceneValidator] '{sceneName}' is launch-critical but " +
                                       "has NO SceneVolumeOverride — base post-FX (Bloom 等) が漏れる。");
                        errorCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"[EnvSceneValidator] '{sceneName}' has NO SceneVolumeOverride " +
                                         "— base SampleScene の Global Volume (Bloom/Vignette/Tonemapping) が漏れます。" +
                                         "意図的なら無視可。");
                        warnCount++;
                    }
                }
                if (env > 1) { Debug.LogWarning($"[EnvSceneValidator] '{sceneName}' has {env} SceneEnvironments (expected 1)"); warnCount++; }
                if (vol > 1) { Debug.LogWarning($"[EnvSceneValidator] '{sceneName}' has {vol} SceneVolumeOverrides (expected 0..1)"); warnCount++; }
                if (cam > 1) { Debug.LogWarning($"[EnvSceneValidator] '{sceneName}' has {cam} SceneCameraOverrides (expected 0..1)"); warnCount++; }
            }

            report.AppendLine();
            report.AppendLine($"=== summary: {errorCount} error, {warnCount} warning ===");
            Debug.Log(report.ToString());

            // open 中だった scene に戻す (validator は env scene を順に open するため)
            if (!string.IsNullOrEmpty(openScene) && openScene != EditorSceneManager.GetActiveScene().path)
                EditorSceneManager.OpenScene(openScene, OpenSceneMode.Single);
        }

        private static (int env, int vol, int cam) InspectScene(string path)
        {
            var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int env = 0, vol = 0, cam = 0;
            foreach (var root in sc.GetRootGameObjects())
            {
                env += root.GetComponentsInChildren<SceneEnvironment>(true).Length;
                vol += root.GetComponentsInChildren<SceneVolumeOverride>(true).Length;
                cam += root.GetComponentsInChildren<SceneCameraOverride>(true).Length;
            }
            return (env, vol, cam);
        }
    }
}
