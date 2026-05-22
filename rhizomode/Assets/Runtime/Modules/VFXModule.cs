#nullable enable
using System.Collections.Generic;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.Nodes.Modules;
using UnityEngine;
using UnityEngine.VFX;

namespace Rhizomode.Modules
{
    /// <summary>
    /// VFX Graph制御用モジュール。ModuleDefinitionで定義されたパラメータをVisualEffectに反映する。
    /// </summary>
    [RequireComponent(typeof(VisualEffect))]
    [PerformanceModule(NodeCategory.VFX, legacyTypeNamePrefix: "VFX_", customNodeType: typeof(VFXModuleNode))]
    public class VFXModule : MonoBehaviour, IPerformanceModule
    {
        private static readonly List<ParamDefinition> EmptyParams = new();

        [SerializeField] private ModuleDefinition? definition;

        private VisualEffect? _vfx;

        // VFXModuleNode が Vector3 プロパティを XYZ 3 つの Float ポートへ分解するため、
        // 軸ごとに届く値をプロパティ名で束ね Vector3 を再構成して保持する。
        private readonly Dictionary<string, Vector3> _vec3Accum = new();

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
                if (paramDef == null)
                {
                    // ModuleDefinition 未登録 = VFXModuleNode が VFX アセットの Exposed
                    // プロパティを自動ポート化したケース。VFX 側の型に合わせて駆動する。
                    SetUndefinedParam(paramName, value);
                    return;
                }

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

        /// <summary>
        /// ModuleDefinition に未登録のパラメータを、VFX アセットが公開する型に合わせて駆動する。
        /// VFXModuleNode が VFX Graph の Exposed プロパティを自動ポート化したケースで使う。
        /// </summary>
        /// <remarks>
        /// 値の C# 型 (= ノードのポート型) と VFX 側の Has* を突き合わせ、型不一致は黙って無視する。
        /// int プロパティは float ポートで届くため HasInt 経由で丸めて送る。
        /// Vector3 プロパティは VFXModuleNode が " X"/" Y"/" Z" 付きの 3 Float ポートに分解して
        /// 送ってくるため、軸サフィックスを <see cref="TrySetVector3Axis"/> で束ね直す。
        /// </remarks>
        private void SetUndefinedParam(string paramName, object value)
        {
            if (_vfx == null) return;

            if (value is float f)
            {
                if (_vfx.HasFloat(paramName)) _vfx.SetFloat(paramName, f);
                else if (_vfx.HasInt(paramName)) _vfx.SetInt(paramName, Mathf.RoundToInt(f));
                else TrySetVector3Axis(paramName, f);
                return;
            }
            if (value is Color c && _vfx.HasVector4(paramName))
            {
                _vfx.SetVector4(paramName, (Vector4)c);
                return;
            }
            if (value is bool b && _vfx.HasBool(paramName))
            {
                _vfx.SetBool(paramName, b);
            }
        }

        /// <summary>
        /// " X"/" Y"/" Z" サフィックス付きの Float 値を、対応する Vector3 プロパティの 1 軸として反映する。
        /// 他 2 軸の現在値は <see cref="_vec3Accum"/> (初回は VFX の現値) から引き継ぐ。
        /// </summary>
        private void TrySetVector3Axis(string portName, float value)
        {
            if (_vfx == null) return;

            int axis = AxisIndex(portName);
            if (axis < 0) return;

            var baseName = portName.Substring(0, portName.Length - 2);
            if (!_vfx.HasVector3(baseName)) return;

            if (!_vec3Accum.TryGetValue(baseName, out var vec))
                vec = _vfx.GetVector3(baseName);
            vec[axis] = value;
            _vec3Accum[baseName] = vec;
            _vfx.SetVector3(baseName, vec);
        }

        /// <summary>ポート名末尾の " X"/" Y"/" Z" を軸インデックス 0/1/2 に変換する。該当なしは -1。</summary>
        private static int AxisIndex(string portName)
        {
            if (portName.Length < 3 || portName[portName.Length - 2] != ' ') return -1;
            return portName[portName.Length - 1] switch
            {
                'X' => 0,
                'Y' => 1,
                'Z' => 2,
                _ => -1,
            };
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
