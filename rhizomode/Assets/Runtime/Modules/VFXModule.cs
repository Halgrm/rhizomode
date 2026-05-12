#nullable enable
using System.Collections.Generic;
using Rhizomode.Core;
using UnityEngine;
using UnityEngine.VFX;

namespace Rhizomode.Modules
{
    /// <summary>
    /// VFX Graph制御用モジュール。ModuleDefinitionで定義されたパラメータをVisualEffectに反映する。
    /// </summary>
    [RequireComponent(typeof(VisualEffect))]
    public class VFXModule : MonoBehaviour, IPerformanceModule
    {
        private static readonly List<ParamDefinition> EmptyParams = new();

        [SerializeField] private ModuleDefinition? definition;

        private VisualEffect? _vfx;

        /// <inheritdoc />
        public string ModuleName => definition != null ? definition.moduleName : "";

        /// <inheritdoc />
        public IReadOnlyList<ParamDefinition> Params =>
            definition != null ? definition.parameters : EmptyParams;

        /// <summary>
        /// ランタイムからModuleDefinitionを設定する。Awake前に呼ぶ想定。
        /// </summary>
        public void Initialize(ModuleDefinition def)
        {
            definition = def;
        }

        private void Awake()
        {
            _vfx = GetComponent<VisualEffect>();

            // Active信号が来るまでVFXを無効化（常時エミッションVFX対策）
            if (_vfx != null)
            {
                _vfx.enabled = false;
            }
        }

        private const string ActiveParam = "Active";

        /// <inheritdoc />
        public void SetParam(string paramName, object value)
        {
            if (_vfx == null || _vfx.visualEffectAsset == null) return;

            // "Active" はVFXの表示切替を制御する予約パラメータ
            if (paramName == ActiveParam)
            {
                _vfx.enabled = value is true;
                return;
            }

            if (definition == null) return;

            try
            {
                // events配列に登録されたイベントはSendEventで発火
                if (definition.IsEvent(paramName))
                {
                    Debug.Log($"[VFXModule] Event '{paramName}' value={value}");
                    if (value is true)
                    {
                        if (paramName == VisualEffectAsset.PlayEventName)
                        {
                            Debug.Log($"[VFXModule] Triggering OnPlay — enabled={_vfx.enabled} active={_vfx.gameObject.activeInHierarchy} alive={_vfx.aliveParticleCount} pos={_vfx.transform.position}");
                            _vfx.Play();
                            Debug.Log($"[VFXModule] After Play() — alive={_vfx.aliveParticleCount}");
                        }
                        else
                        {
                            Debug.Log($"[VFXModule] Calling _vfx.SendEvent('{paramName}')");
                            _vfx.SendEvent(paramName);
                        }
                    }
                    return;
                }

                var paramDef = definition.GetParam(paramName);
                if (paramDef == null) return;

                switch (paramDef.type)
                {
                    case ParamType.Float:
                        _vfx.SetFloat(paramName, (float)value);
                        break;
                    case ParamType.Color:
                        // VFX GraphはVector4でカラーを受け取る
                        _vfx.SetVector4(paramName, (Vector4)(Color)value);
                        break;
                    case ParamType.Bool:
                        SetBoolParam(paramName, paramDef, (bool)value);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[VFXModule] SetParam failed: {paramName} = {value} ({ex.Message})");
            }
        }

        /// <summary>
        /// Bool型パラメータの設定。isEventフラグが立っている場合はSendEventとして扱う。
        /// 後方互換: ParamDefinition.isEventも引き続きサポート。
        /// </summary>
        private void SetBoolParam(string paramName, ParamDefinition paramDef, bool value)
        {
            if (_vfx == null) return;

            if (paramDef.isEvent && value)
            {
                _vfx.SendEvent(paramName);
            }
            else if (!paramDef.isEvent)
            {
                _vfx.SetBool(paramName, value);
            }
        }

        /// <inheritdoc />
        public void Activate()
        {
            // Active信号で表示制御するため、ここでは何もしない
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            if (_vfx == null) return;

            _vfx.Stop();
            _vfx.enabled = false;
        }
    }
}
