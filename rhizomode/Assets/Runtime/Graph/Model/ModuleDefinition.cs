#nullable enable

using System.Collections.Generic;
using UnityEngine;

using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Model
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
        /// SendEventとして発火するイベント名リスト。パラメータとは独立管理。
        /// </summary>
        public List<string> events = new();

        private Dictionary<string, ParamDefinition>? _paramCache;
        private HashSet<string>? _eventCache;

        /// <summary>
        /// 名前でパラメータ定義を取得する。見つからない場合はnull。
        /// O(1)ルックアップ（初回アクセス時にキャッシュ構築）。
        /// </summary>
        public ParamDefinition? GetParam(string paramName)
        {
            EnsureParamCache();
            return _paramCache!.TryGetValue(paramName, out var def) ? def : null;
        }

        /// <summary>
        /// 指定名がイベントとして登録されているかどうか。
        /// </summary>
        public bool IsEvent(string name)
        {
            EnsureEventCache();
            return _eventCache!.Contains(name);
        }

        private void EnsureParamCache()
        {
            if (_paramCache != null) return;
            _paramCache = new Dictionary<string, ParamDefinition>(parameters.Count);
            foreach (var p in parameters)
                _paramCache[p.name] = p;
        }

        private void EnsureEventCache()
        {
            if (_eventCache != null) return;
            _eventCache = new HashSet<string>(events);
        }

        private void OnValidate()
        {
            // Inspectorで値が変更されたらキャッシュを破棄
            _paramCache = null;
            _eventCache = null;
        }
    }
}
