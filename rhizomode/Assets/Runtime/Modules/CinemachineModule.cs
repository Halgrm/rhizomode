#nullable enable

using System.Collections.Generic;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;
using UnityEngine;

#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

namespace Rhizomode.Modules
{
    /// <summary>
    /// CinemachineCamera制御用モジュール。ModuleDefinitionで定義されたパラメータを
    /// CinemachineCamera（Unity 6 / Cinemachine 3.x）に反映する。
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    [PerformanceModule(NodeCategory.Scene, legacyTypeNamePrefix: "Cinemachine_")]
    public class CinemachineModule : MonoBehaviour, IPerformanceModule
    {
        [SerializeField] private ModuleDefinition? definition;

        private CinemachineCamera? _camera;

        /// <inheritdoc />
        public string ModuleName => definition != null ? definition.moduleName : "";

        /// <inheritdoc />
        public IReadOnlyList<ParamDefinition> Params =>
            definition != null ? definition.parameters : new List<ParamDefinition>();

        /// <summary>
        /// ランタイムからModuleDefinitionを設定する。
        /// </summary>
        public void Initialize(ModuleDefinition def)
        {
            definition = def;
        }

        private void Awake()
        {
            _camera = GetComponent<CinemachineCamera>();
        }

        /// <inheritdoc />
        public void SetParam(string paramName, object value)
        {
            if (_camera == null || definition == null) return;

            try
            {
                var paramDef = definition.GetParam(paramName);
                if (paramDef == null) return;

                switch (paramName)
                {
                    case "FOV":
                        if (value is float fov)
                        {
                            var fovLens = _camera.Lens;
                            fovLens.FieldOfView = Mathf.Clamp(fov, 1f, 179f);
                            _camera.Lens = fovLens;
                        }
                        break;
                    case "Dutch":
                        if (value is float dutch)
                        {
                            var dutchLens = _camera.Lens;
                            dutchLens.Dutch = dutch;
                            _camera.Lens = dutchLens;
                        }
                        break;
                    case "Priority":
                        if (value is float priority)
                            _camera.Priority = Mathf.RoundToInt(priority);
                        break;
                    default:
                        ApplyGenericParam(paramName, paramDef, value);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CinemachineModule] SetParam failed: {paramName} = {value} ({ex.Message})");
            }
        }

        /// <summary>
        /// ModuleDefinitionで定義されたカスタムパラメータを汎用的に適用する。
        /// 名前ベースでCinemachineプロパティにマッピングする拡張ポイント。
        /// </summary>
        private void ApplyGenericParam(string paramName, ParamDefinition paramDef, object value)
        {
            // 将来的な拡張用。未知のパラメータはログのみ。
            if (paramDef.type == ParamType.Float && value is float f)
            {
                Debug.Log($"[CinemachineModule] Unhandled float param: {paramName} = {f}");
            }
        }

        /// <inheritdoc />
        public void Activate()
        {
            if (_camera == null) return;

            try
            {
                _camera.enabled = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CinemachineModule] Activate failed: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            if (_camera == null) return;

            try
            {
                _camera.enabled = false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CinemachineModule] Deactivate failed: {ex.Message}");
            }
        }
    }
}
