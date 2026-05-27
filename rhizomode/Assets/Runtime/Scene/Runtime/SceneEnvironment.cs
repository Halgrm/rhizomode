#nullable enable

using UnityEngine;
using UnityEngine.Rendering;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.Scene.Runtime
{
    /// <summary>
    /// Additiveシーンに配置し、ロード時にRenderSettingsを上書きするコンポーネント。
    /// SetActiveSceneを使わずに環境設定（Skybox/Fog/Ambient等）を切り替える。
    /// </summary>
    public class SceneEnvironment : MonoBehaviour
    {
        [Header("Skybox")]
        [SerializeField] private Material? skyboxMaterial;

        [Header("Ambient")]
        [SerializeField] private AmbientMode ambientMode = AmbientMode.Skybox;
        [SerializeField] private Color ambientSkyColor = new(0.212f, 0.227f, 0.259f);
        [SerializeField] private Color ambientEquatorColor = new(0.114f, 0.125f, 0.133f);
        [SerializeField] private Color ambientGroundColor = new(0.047f, 0.043f, 0.035f);
        [SerializeField, Range(0f, 8f)] private float ambientIntensity = 1f;

        [Header("Fog")]
        [SerializeField] private bool fogEnabled;
        [SerializeField] private Color fogColor = Color.gray;
        [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
        [SerializeField] private float fogDensity = 0.01f;
        [SerializeField] private float fogStartDistance = 10f;
        [SerializeField] private float fogEndDistance = 300f;

        [Header("Reflection")]
        [SerializeField] private DefaultReflectionMode reflectionMode = DefaultReflectionMode.Skybox;
        [SerializeField, Range(0f, 1f)] private float reflectionIntensity = 1f;

        [Header("Base Scene Lighting Toggles")]
        [Tooltip("true にすると、env load 中だけ base (SampleScene) の " +
                 "EnvOverridableDirectionalLight marker を持つ Directional Light を一時 disable する。" +
                 "屋内 env (concrete 等) で太陽光をカットしたいとき使う。")]
        [SerializeField] private bool disableBaseDirectionalLight = false;

        /// <summary>
        /// <see cref="disableBaseDirectionalLight"/> の getter (loader が参照する)。
        /// </summary>
        internal bool DisableBaseDirectionalLight => disableBaseDirectionalLight;

        /// <summary>
        /// このコンポーネントの設定でRenderSettingsを上書きする。
        /// </summary>
        public void Apply()
        {
            RenderSettings.skybox = skyboxMaterial;

            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSkyColor;
            RenderSettings.ambientEquatorColor = ambientEquatorColor;
            RenderSettings.ambientGroundColor = ambientGroundColor;
            RenderSettings.ambientIntensity = ambientIntensity;

            RenderSettings.fog = fogEnabled;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance = fogEndDistance;

            RenderSettings.defaultReflectionMode = reflectionMode;
            RenderSettings.reflectionIntensity = reflectionIntensity;

            DynamicGI.UpdateEnvironment();
        }
    }
}
