#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Rhizomode.Core
{
    /// <summary>
    /// 演出モジュールの定義。パラメータ構成とPrefabの参照を持つScriptableObject。
    /// </summary>
    [CreateAssetMenu(menuName = "Rhizomode/ModuleDefinition")]
    public class ModuleDefinition : ScriptableObject
    {
        public string moduleName = "";
        public GameObject? prefab;
        public List<ParamDefinition> parameters = new();

        /// <summary>
        /// 名前でパラメータ定義を取得する。見つからない場合はnull。
        /// </summary>
        public ParamDefinition? GetParam(string paramName)
        {
            return parameters.Find(p => p.name == paramName);
        }
    }
}
