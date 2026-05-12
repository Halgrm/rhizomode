#nullable enable
using System.Collections.Generic;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

namespace Rhizomode.Modules
{
    /// <summary>
    /// シェーダーパラメータ制御用モジュール。MaterialPropertyBlockを使い、マテリアルインスタンスを生成しない。
    /// </summary>
    public class ShaderModule : MonoBehaviour, IPerformanceModule
    {
        private static readonly List<ParamDefinition> EmptyParams = new();

        [SerializeField] private Renderer? targetRenderer;
        [SerializeField] private ModuleDefinition? definition;

        private MaterialPropertyBlock? _mpb;
        private bool _mpbDirty;

        /// <inheritdoc />
        public string ModuleName => definition != null ? definition.moduleName : "";

        /// <inheritdoc />
        public IReadOnlyList<ParamDefinition> Params =>
            definition != null ? definition.parameters : EmptyParams;

        /// <summary>
        /// ランタイムからModuleDefinitionとRendererを設定する。Awake前に呼ぶ想定。
        /// </summary>
        public void Initialize(ModuleDefinition def, Renderer renderer)
        {
            definition = def;
            targetRenderer = renderer;
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        /// <inheritdoc />
        public void SetParam(string paramName, object value)
        {
            if (_mpb == null || targetRenderer == null || definition == null) return;

            // シェーダーにイベント概念はないのでスキップ
            if (definition.IsEvent(paramName)) return;

            var paramDef = definition.GetParam(paramName);
            if (paramDef == null) return;

            try
            {
                switch (paramDef.type)
                {
                    case ParamType.Float:
                        _mpb.SetFloat(paramName, (float)value);
                        break;
                    case ParamType.Color:
                        _mpb.SetColor(paramName, (Color)value);
                        break;
                    case ParamType.Bool:
                        // シェーダーにはboolが無いのでfloat 0/1で代用
                        _mpb.SetFloat(paramName, (bool)value ? 1f : 0f);
                        break;
                }

                _mpbDirty = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ShaderModule] SetParam failed: {paramName} = {value} ({ex.Message})");
            }
        }

        private void LateUpdate()
        {
            if (!_mpbDirty || _mpb == null || targetRenderer == null) return;
            _mpbDirty = false;
            targetRenderer.SetPropertyBlock(_mpb);
        }

        /// <inheritdoc />
        public void Activate()
        {
            if (targetRenderer == null) return;

            try
            {
                targetRenderer.enabled = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ShaderModule] Activate failed: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            if (targetRenderer == null) return;

            try
            {
                targetRenderer.enabled = false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ShaderModule] Deactivate failed: {ex.Message}");
            }
        }
    }
}
