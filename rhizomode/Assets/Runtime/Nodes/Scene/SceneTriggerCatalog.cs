#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.NodeCatalog.Contracts;
using UnityEngine;

namespace Rhizomode.Nodes.Scene
{
    /// <summary>
    /// SceneTrigger ノードの動的 typeName 一覧を持つ ScriptableObject。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 4 follow-up: SceneTriggerNode は同一クラスで複数 typeName を扱う
    /// (SceneDark / SceneWhite / SceneNature 等)。<see cref="NodeTypeAttribute"/> による静的登録では
    /// 表現できないため、本 catalog SO に entries を列挙し、<see cref="SceneTriggerNodeFactory"/> が
    /// 動的に INodeFactory として公開する。
    ///
    /// .asset 配置は <c>Assets/Data/Config/SceneTriggerCatalog.asset</c> を想定 (Plan v5.3-1)。
    /// </remarks>
    [CreateAssetMenu(fileName = "SceneTriggerCatalog", menuName = "Rhizomode/Scene/SceneTrigger Catalog")]
    public sealed class SceneTriggerCatalog : ScriptableObject
    {
        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        public Entry? FindByTypeName(string typeName)
        {
            foreach (var e in entries)
            {
                if (e.TypeName == typeName) return e;
            }
            return null;
        }

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string typeName = "";
            [SerializeField] private string label = "";
            [SerializeField] private int sceneIndex;

            public string TypeName => typeName;
            public string Label => label;
            public int SceneIndex => sceneIndex;

            public Entry() { }

            // テスト用 / プログラム生成用
            public Entry(string typeName, string label, int sceneIndex)
            {
                this.typeName = typeName;
                this.label = label;
                this.sceneIndex = sceneIndex;
            }
        }
    }
}
