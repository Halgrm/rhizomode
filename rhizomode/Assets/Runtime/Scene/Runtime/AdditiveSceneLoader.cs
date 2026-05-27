#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Scene.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Rhizomode.Scene.Runtime
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
        private UnityScene _baseScene;

        // env scene 1 つあたり 1 session。env load 毎に作り直し、unload で Dispose する。
        // 多 env 同時 active への将来拡張も session を List 化するだけで済む設計。
        private CameraOverrideSession? _cameraSession;
        private readonly List<SceneVolumeOverride> _activeVolumeOverrides = new();
        // base scene の disable された Directional Light 群と元の enabled 状態。
        // env unload 時に enabled を元に戻すための snapshot。
        private readonly Dictionary<Light, bool> _disabledBaseDirectionalLights = new();

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
                    // Revert 順序: Camera → Volume → Env (Apply の逆順、Plan v0.3 §Apply/Revert 規約)。
                    // ここで先に Revert しておくのは、scene unload 時に override component の
                    // GameObject が破棄されると参照が失われるため。
                    RevertOverridesBeforeUnload();

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
                    RevertOverridesBeforeUnload();
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

            // unload 前に override を revert する (component 破棄前に snapshot を解放)
            RevertOverridesBeforeUnload();

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
                    // Apply 順序: Env → Volume → Camera (Plan v0.3 §設計案/Apply 順序の規約)
                    ApplySceneEnvironment(loadedScene);
                    ApplyDirectionalLightToggle(loadedScene);
                    ApplyVolumeOverrides(loadedScene);
                    ApplyCameraOverrides(loadedScene);
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
        private void ApplySceneEnvironment(UnityScene scene)
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
        /// 指定 env scene 内の <see cref="SceneVolumeOverride"/> を全て Apply する。
        /// 1 つも無ければ post-FX (Bloom/Vignette/Tonemapping) が SampleScene の Global Volume
        /// から漏れることを警告する。Plan v0.3 §設計案/Volume profile 完全性契約。
        /// </summary>
        private void ApplyVolumeOverrides(UnityScene scene)
        {
            // 念のため stale 状態を revert (load → load の連続呼出し対応)
            foreach (var vo in _activeVolumeOverrides)
            {
                if (vo != null) vo.Revert();
            }
            _activeVolumeOverrides.Clear();

            CollectComponentsInScene(scene, _activeVolumeOverrides);

            if (_activeVolumeOverrides.Count == 0)
            {
                Debug.LogWarning(
                    $"[AdditiveSceneLoader] env scene '{scene.name}' has no SceneVolumeOverride — " +
                    "SampleScene の Global Volume (Bloom / Vignette / Tonemapping) が漏れます。" +
                    " env profile を持つ SceneVolumeOverride を 1 個追加してください。");
                return;
            }

            foreach (var vo in _activeVolumeOverrides)
            {
                if (vo == null) continue;
                try { vo.Apply(); }
                catch (Exception e)
                {
                    Debug.LogError($"[AdditiveSceneLoader] SceneVolumeOverride.Apply failed " +
                                   $"on '{vo.gameObject.name}': {e.Message}");
                }
            }
        }

        /// <summary>
        /// 指定 env scene 内の <see cref="SceneCameraOverride"/> を全て収集して
        /// <see cref="CameraOverrideSession"/> 経由で適用する。Session は loader-owned。
        /// 加えて base scene の <see cref="EnvOverridableCamera"/> marker を持つ camera
        /// を全 override に append し、cross-scene wiring を解決する。
        /// </summary>
        private void ApplyCameraOverrides(UnityScene scene)
        {
            _cameraSession?.Dispose();  // 前回 session の念のため revert
            _cameraSession = new CameraOverrideSession();

            var overrides = new List<SceneCameraOverride>();
            CollectComponentsInScene(scene, overrides);
            if (overrides.Count == 0) return; // SceneCameraOverride は optional

            // base scene の marker camera を全 override に注入。
            // env シーン側は targets を空にしておけば marker 経由で自動 wiring される。
            var markerCameras = CollectMarkerCameras();
            try { _cameraSession.Apply(overrides, markerCameras); }
            catch (Exception e)
            {
                Debug.LogError($"[AdditiveSceneLoader] CameraOverrideSession.Apply failed: {e.Message}");
            }
        }

        /// <summary>
        /// 全ロード済シーンから <see cref="EnvOverridableCamera"/> marker 付き camera を収集する。
        /// </summary>
        private static List<Camera> CollectMarkerCameras()
        {
            var result = new List<Camera>();
            // FindObjectsByType は active な instance のみを返す。env load 後の検索なので
            // marker camera は SampleScene (base) と loaded env の両方を含み得る。
            var markers = UnityEngine.Object.FindObjectsByType<EnvOverridableCamera>(
                FindObjectsSortMode.None);
            foreach (var m in markers)
            {
                var cam = m.GetComponent<Camera>();
                if (cam != null) result.Add(cam);
            }
            return result;
        }

        /// <summary>
        /// env の <see cref="SceneEnvironment.DisableBaseDirectionalLight"/> が true なら、
        /// base scene の <see cref="EnvOverridableDirectionalLight"/> marker 付き Directional
        /// Light を一時 disable する。snapshot を保存し、unload 時に元の enabled 状態に戻す。
        /// </summary>
        private void ApplyDirectionalLightToggle(UnityScene scene)
        {
            // 既存 snapshot は念のため revert (前回 cycle の残骸を防ぐ)
            RestoreBaseDirectionalLights();

            SceneEnvironment? env = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var e = root.GetComponentInChildren<SceneEnvironment>(includeInactive: false);
                if (e != null) { env = e; break; }
            }
            if (env == null || !env.DisableBaseDirectionalLight) return;

            var markers = UnityEngine.Object.FindObjectsByType<EnvOverridableDirectionalLight>(
                FindObjectsSortMode.None);
            foreach (var m in markers)
            {
                var light = m.GetComponent<Light>();
                if (light == null || light.type != LightType.Directional) continue;
                if (!_disabledBaseDirectionalLights.ContainsKey(light))
                    _disabledBaseDirectionalLights[light] = light.enabled;
                light.enabled = false;
            }
        }

        /// <summary>scene unload 時に base directional light の enabled 状態を snapshot から復元。</summary>
        private void RestoreBaseDirectionalLights()
        {
            foreach (var pair in _disabledBaseDirectionalLights)
            {
                if (pair.Key == null) continue;
                pair.Key.enabled = pair.Value;
            }
            _disabledBaseDirectionalLights.Clear();
        }

        /// <summary>
        /// scene unload 前に override を revert する。GameObject 破棄前に呼ぶこと
        /// (破棄後だと <see cref="SceneVolumeOverride._runtimeVolume"/> 等の参照が無効化される)。
        /// Revert 順序: Camera → Volume → Directional Light → Env (Apply の逆順)。
        /// </summary>
        private void RevertOverridesBeforeUnload()
        {
            // Camera (Apply の逆順)
            _cameraSession?.Dispose();
            _cameraSession = null;
            // Volume
            foreach (var vo in _activeVolumeOverrides)
            {
                if (vo == null) continue;
                try { vo.Revert(); }
                catch (Exception e)
                {
                    Debug.LogError($"[AdditiveSceneLoader] SceneVolumeOverride.Revert failed: {e.Message}");
                }
            }
            _activeVolumeOverrides.Clear();
            // Directional Light (base の enabled 復元)
            RestoreBaseDirectionalLights();
        }

        /// <summary>指定 scene 内の全 root を辿って T 型 component を収集する。</summary>
        private static void CollectComponentsInScene<T>(UnityScene scene, List<T> dest) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentsInChildren<T>(includeInactive: false);
                if (found != null && found.Length > 0)
                    dest.AddRange(found);
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
