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
    /// VolumeManager.instance.stack から現在の Bloom override を取得して直接書き込む。
    /// Bloom が profile に存在しない場合は SetParam を no-op に倒し、映像継続を保証する。
    /// Module が複数 spawn された場合は last-write-wins (Bloom は scene 単一)。
    /// </remarks>
    [PerformanceModule(NodeCategory.Shader)]
    public sealed class BloomModule : MonoBehaviour, IPerformanceModule
    {
        private const string ParamThreshold = "Threshold";
        private const string ParamIntensity = "Intensity";
        private const string ParamScatter = "Scatter";

        private static readonly List<ParamDefinition> EmptyParams = new();

        [SerializeField] private ModuleDefinition? definition;

        private Bloom? _bloom;
        private float _intensityBeforeDeactivate;
        private bool _intensitySnapshotValid;

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
            EnsureBloom();
            if (_bloom == null) return;
            if (_intensitySnapshotValid)
            {
                _bloom.intensity.value = _intensityBeforeDeactivate;
                _intensitySnapshotValid = false;
            }
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            EnsureBloom();
            if (_bloom == null) return;
            // Bloom override を残したまま intensity 0 で実質無効化 (Profile を壊さない)
            _intensityBeforeDeactivate = _bloom.intensity.value;
            _intensitySnapshotValid = true;
            _bloom.intensity.value = 0f;
        }

        /// <inheritdoc />
        public void SetParam(string paramName, object value)
        {
            EnsureBloom();
            if (_bloom == null) return;

            try
            {
                var f = ToFinite((float)value, 0f);
                switch (paramName)
                {
                    case ParamThreshold:
                        _bloom.threshold.overrideState = true;
                        _bloom.threshold.value = Mathf.Max(0f, f);
                        break;
                    case ParamIntensity:
                        _bloom.intensity.overrideState = true;
                        _bloom.intensity.value = Mathf.Max(0f, f);
                        break;
                    case ParamScatter:
                        _bloom.scatter.overrideState = true;
                        _bloom.scatter.value = Mathf.Clamp01(f);
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
            try
            {
                _bloom = VolumeManager.instance?.stack?.GetComponent<Bloom>();
                if (_bloom == null)
                    Debug.LogWarning("[BloomModule] Bloom override not found in active VolumeStack. Add it to a Volume Profile.");
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
