#nullable enable

using UnityEngine;

namespace Rhizomode.Core
{
    /// <summary>
    /// モジュールパラメータの定義。ModuleDefinitionのパラメータリストに使用。
    /// </summary>
    [System.Serializable]
    public class ParamDefinition
    {
        public string name = "";
        public ParamType type;
        public float defaultFloat;
        public Color defaultColor;
        public bool defaultBool;

        /// <summary>
        /// trueの場合、Bool=trueでSendEvent発火として扱う。
        /// </summary>
        public bool isEvent;
    }
}
