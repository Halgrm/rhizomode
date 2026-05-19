#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rhizomode.Modules
{
    /// <summary>
    /// URP Volume の Bloom Override を rhizomode ノードグラフから driving するモジュール。
    /// </summary>
    /// <remarks>
    /// シーン内の <see cref="Volume"/> を見つけ、その <c>profile</c> (instance copy) の Bloom override を
    /// 直接書き換える。VolumeManager.stack はフレーム毎に source Volume から再ブレンドされて
    /// 上書きされてしまうため、stack 側の書き換えは効かない。
    /// "Active" Bool ポートで Bloom override 自体の active を切り替える (default true)。
    /// 複数 spawn 時は last-write-wins (Bloom は scene 単一の global resource)。
    /// </remarks>
    [PerformanceModule(NodeCategory.Shader)]
    public sealed class BloomModule : MonoBehaviour, IPerformanceModule
    {
        private const string ParamActive = "Active";
        private const string ParamThreshold = "Threshold";
        private const string ParamIntensity = "Intensity";
        private const string ParamScatter = "Scatter";

        private static readonly List<ParamDefinition> EmptyParams = new();

        [SerializeField] private ModuleDefinition? definition;

        private Bloom? _bloom;
        private bool _searchAttempted;

        /// <inheritdoc />
        public string ModuleName => definition != null ? definition.moduleName : "Bloom";

        /// <inheritdoc />
        public IReadOnlyList<ParamDefinition> Params =>
            definition != null ? definition.parameters : EmptyParams;

        /// <inheritdoc />
        public void Initialize(ModuleDefinition def)
        {
            definition = def;
        }

        /// <inheritdoc />
        public void Activate()
        {
            // Active 状態は "Active" Bool ポートが SetParam 経由で push する。
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            EnsureBloom();
            if (_bloom != null) _bloom.active = false;
        }

        /// <inheritdoc />
        public void SetParam(string paramName, object value)
        {
            EnsureBloom();
            if (_bloom == null) return;

            try
            {
                switch (paramName)
                {
                    case ParamActive:
                        _bloom.active = (bool)value;
                        break;
                    case ParamThreshold:
                        _bloom.threshold.overrideState = true;
                        _bloom.threshold.value = Mathf.Max(0f, ToFinite((float)value, _bloom.threshold.value));
                        break;
                    case ParamIntensity:
                        _bloom.intensity.overrideState = true;
                        _bloom.intensity.value = Mathf.Max(0f, ToFinite((float)value, _bloom.intensity.value));
                        break;
                    case ParamScatter:
                        _bloom.scatter.overrideState = true;
                        _bloom.scatter.value = Mathf.Clamp01(ToFinite((float)value, _bloom.scatter.value));
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BloomModule] SetParam failed: {paramName} = {value} ({ex.Message})");
            }
        }

        private void EnsureBloom()
        {
            if (_bloom != null) return;
            if (_searchAttempted) return; // warning 連発を避ける
            _searchAttempted = true;

            try
            {
                var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
                Volume? target = null;
                foreach (var v in volumes)
                {
                    if (v == null || !v.isActiveAndEnabled) continue;
                    // global Volume を優先、なければ最初の有効な Volume
                    if (v.isGlobal) { target = v; break; }
                    if (target == null) target = v;
                }

                if (target == null || target.profile == null)
                {
                    Debug.LogWarning("[BloomModule] No active Volume with profile found in scene.");
                    return;
                }

                // profile (instance) は volume が runtime copy を持つので asset を汚さない。
                if (!target.profile.TryGet(out _bloom))
                {
                    Debug.LogWarning($"[BloomModule] Bloom override not found in '{target.profile.name}'. Add Bloom override to the Volume Profile.");
                    _bloom = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BloomModule] EnsureBloom failed: {ex.Message}");
            }
        }

        private static float ToFinite(float value, float fallback)
        {
            return float.IsFinite(value) ? value : fallback;
        }
    }
}
