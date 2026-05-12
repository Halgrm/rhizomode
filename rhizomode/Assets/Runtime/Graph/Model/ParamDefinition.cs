#nullable enable

using UnityEngine;

using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Model
{
    /// <summary>
    /// モジュールパラメータの定義。ModuleDefinitionのパラメータリストに使用。
    /// </summary>
    [System.Serializable]
    public class ParamDefinition
    {
        [Tooltip("パラメータ名（ポート名・シェーダープロパティ名と一致させること）")]
        public string name = "";

        [Tooltip("パラメータの型")]
        public ParamType type;

        [Header("Float設定")]
        [Tooltip("Float型のデフォルト値")]
        public float defaultFloat;

        [Tooltip("Float型の最小値")]
        public float minFloat;

        [Tooltip("Float型の最大値")]
        public float maxFloat = 1f;

        [Header("Color設定")]
        [ColorUsage(true, true), Tooltip("Color型のデフォルト値（HDR対応）")]
        public Color defaultColor;

        [Header("Bool設定")]
        [Tooltip("Bool型のデフォルト値")]
        public bool defaultBool;

        [Header("イベント")]
        [Tooltip("trueの場合、Bool=trueでSendEvent発火として扱う")]
        public bool isEvent;
    }
}
