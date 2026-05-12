#nullable enable

using System;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;
using UnityEngine.SceneManagement;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.XR
{
    /// <summary>
    /// Additiveシーン読み込みサービス。Rectorの BGSceneManager に相当。
    /// ベースシーン (起動シーン) は常に残り、登録済みシーンを1つだけ追加ロードする。
    /// 切り替え時は旧シーンをアンロードしてから新シーンをロードする (Async 直列化)。
    /// 各 Additive シーンには SceneEnvironment コンポーネントを1個置き、ロード完了時に
    /// その値で RenderSettings を上書きする。アンロード時はベースシーンの SceneEnvironment にフォールバック。
    /// </summary>
    public class AdditiveSceneLoader : MonoBehaviour, ISceneLoader
    {
        [SerializeField, Tooltip("Build Settingsに登録済みのシーン名リスト")]
        private string[] sceneNames = Array.Empty<string>();

        [Header("Base Scene Fallback")]
        [Tooltip("Additive シーンが無効化 (アンロード or 未ロード) のときに RenderSettings を戻すための " +
                 "ベースシーン用 SceneEnvironment。SampleScene 等のベースシーン上のオブジェクトにアサインする。")]
        [SerializeField] private SceneEnvironment? baseSceneEnvironment;

        private string? _loadedSceneName;
        private bool _isTransitioning;
        private Scene _baseScene;

        public int SceneCount => sceneNames.Length;

        public string? GetSceneName(int index)
        {
            if (index < 0 || index >= sceneNames.Length) return null;
            return sceneNames[index];
        }

        private void Awake()
        {
            // 起動時のアクティブシーン (= ベースシーン) を保存しておく。
            // アンロード時に SetActiveScene でここに戻す
            _baseScene = SceneManager.GetActiveScene();
        }

        public void LoadScene(int index)
        {
            if (index < 0 || index >= sceneNames.Length)
            {
                Debug.LogWarning($"[AdditiveSceneLoader] Index out of range: {index} (count={sceneNames.Length})");
                return;
            }

            var sceneName = sceneNames[index];
            if (_loadedSceneName == sceneName) return;

            if (_isTransitioning)
            {
                Debug.LogWarning("[AdditiveSceneLoader] Scene transition already in progress, ignoring request");
                return;
            }

            _isTransitioning = true;

            try
            {
                // 旧シーンが無ければ即ロード、あればアンロード完了を待ってからロード (Async 直列化)
                if (string.IsNullOrEmpty(_loadedSceneName))
                {
                    BeginLoad(sceneName);
                }
                else
                {
                    BeginUnloadThenLoad(sceneName);
                }
            }
            catch (Exception e)
            {
                _isTransitioning = false;
                Debug.LogError($"[AdditiveSceneLoader] LoadScene failed: {e.Message}");
            }
        }

        public void UnloadCurrentScene()
        {
            if (string.IsNullOrEmpty(_loadedSceneName))
            {
                // 既に何もロードされていない: ベースの設定を念のため再適用 (画面崩れ復旧用)
                ApplyBaseEnvironment();
                return;
            }

            if (_isTransitioning)
            {
                Debug.LogWarning("[AdditiveSceneLoader] Scene transition already in progress, ignoring unload");
                return;
            }

            _isTransitioning = true;

            try
            {
                var scene = SceneManager.GetSceneByName(_loadedSceneName);
                if (scene.isLoaded)
                {
                    var op = SceneManager.UnloadSceneAsync(scene);
                    var unloadedName = _loadedSceneName;
                    _loadedSceneName = null;

                    if (op != null)
                    {
                        op.completed += _ =>
                        {
                            _isTransitioning = false;
                            // ベースシーンをアクティブに戻し、ベースの環境設定を適用
                            if (_baseScene.IsValid() && _baseScene.isLoaded)
                                SceneManager.SetActiveScene(_baseScene);
                            ApplyBaseEnvironment();
                            Debug.Log($"[AdditiveSceneLoader] Unloaded scene: {unloadedName}");
                        };
                    }
                    else
                    {
                        _isTransitioning = false;
                    }
                }
                else
                {
                    _loadedSceneName = null;
                    _isTransitioning = false;
                    ApplyBaseEnvironment();
                }
            }
            catch (Exception e)
            {
                _isTransitioning = false;
                Debug.LogError($"[AdditiveSceneLoader] UnloadCurrentScene failed: {e.Message}");
            }
        }

        private void BeginUnloadThenLoad(string newSceneName)
        {
            var old = _loadedSceneName!;
            var oldScene = SceneManager.GetSceneByName(old);
            if (!oldScene.isLoaded)
            {
                _loadedSceneName = null;
                BeginLoad(newSceneName);
                return;
            }

            var unloadOp = SceneManager.UnloadSceneAsync(oldScene);
            if (unloadOp == null)
            {
                Debug.LogWarning($"[AdditiveSceneLoader] UnloadSceneAsync returned null for '{old}'; proceeding with load");
                _loadedSceneName = null;
                BeginLoad(newSceneName);
                return;
            }

            unloadOp.completed += _ =>
            {
                _loadedSceneName = null;
                Debug.Log($"[AdditiveSceneLoader] Unloaded scene: {old}");
                BeginLoad(newSceneName);
            };
        }

        private void BeginLoad(string sceneName)
        {
            var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (loadOp == null)
            {
                Debug.LogError($"[AdditiveSceneLoader] LoadSceneAsync returned null for '{sceneName}'. Is it in Build Settings?");
                _isTransitioning = false;
                ApplyBaseEnvironment();
                return;
            }

            loadOp.completed += _ =>
            {
                _isTransitioning = false;
                _loadedSceneName = sceneName;

                // 新シーンをアクティブに切替 (Skybox/DynamicGI が新シーン基準で計算されるように)
                var loadedScene = SceneManager.GetSceneByName(sceneName);
                if (loadedScene.IsValid() && loadedScene.isLoaded)
                {
                    SceneManager.SetActiveScene(loadedScene);
                    ApplySceneEnvironment(loadedScene);
                }
                else
                {
                    Debug.LogWarning($"[AdditiveSceneLoader] Loaded scene '{sceneName}' not found in SceneManager");
                    ApplyBaseEnvironment();
                }

                Debug.Log($"[AdditiveSceneLoader] Loaded scene: {sceneName}");
            };
        }

        /// <summary>
        /// 指定シーン内の SceneEnvironment を探して Apply する。
        /// 不在ならベースにフォールバック、複数あれば警告して最初の 1 個を採用。
        /// </summary>
        private void ApplySceneEnvironment(Scene scene)
        {
            SceneEnvironment? found = null;
            int count = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                var envs = root.GetComponentsInChildren<SceneEnvironment>(includeInactive: false);
                count += envs.Length;
                if (envs.Length > 0 && found == null)
                    found = envs[0];
            }

            if (count == 0)
            {
                Debug.LogWarning(
                    $"[AdditiveSceneLoader] Scene '{scene.name}' has no SceneEnvironment — " +
                    $"falling back to base environment");
                ApplyBaseEnvironment();
                return;
            }

            if (count > 1)
            {
                Debug.LogWarning(
                    $"[AdditiveSceneLoader] Scene '{scene.name}' has {count} SceneEnvironment instances " +
                    $"(expected 1). Using the first one found.");
            }

            try
            {
                found!.Apply();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdditiveSceneLoader] SceneEnvironment.Apply failed: {e.Message}");
                ApplyBaseEnvironment();
            }
        }

        /// <summary>
        /// ベースシーン用 SceneEnvironment を適用する。アサインされていなければ警告のみ。
        /// </summary>
        private void ApplyBaseEnvironment()
        {
            if (baseSceneEnvironment == null)
            {
                Debug.LogWarning(
                    "[AdditiveSceneLoader] baseSceneEnvironment is not assigned — " +
                    "RenderSettings may retain stale values from the previous additive scene");
                return;
            }

            try
            {
                baseSceneEnvironment.Apply();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdditiveSceneLoader] baseSceneEnvironment.Apply failed: {e.Message}");
            }
        }
    }
}
