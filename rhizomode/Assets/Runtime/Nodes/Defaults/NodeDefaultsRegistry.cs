#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.SharedKernel;
using UnityEngine;

namespace Rhizomode.Nodes.Defaults
{
    /// <summary>
    /// ノードの default パラメータ値を typeName + paramName で管理する SO。
    /// </summary>
    /// <remarks>
    /// Plan v5.3-2: <c>NodeDefaultsRegistry</c> はノード具体型を知らない。
    /// <c>typeName + paramName + ParamValue</c> の表のみを保持し、
    /// <see cref="NodeDefaultLifecycleProcessor"/> が <c>INodeParamAccessor.TrySetParam</c>
    /// 経由でノードに適用する。
    ///
    /// .asset 実体は <c>Assets/Data/Config/NodeDefaults/NodeDefaultsRegistry.asset</c> に配置
    /// (Plan v5.3-1: Runtime 配下に .asset を置かない)。
    /// </remarks>
    [CreateAssetMenu(fileName = "NodeDefaultsRegistry", menuName = "Rhizomode/Nodes/Defaults Registry")]
    public sealed class NodeDefaultsRegistry : ScriptableObject
    {
        [SerializeField] private List<TypeDefaults> entries = new();

        public IReadOnlyList<DefaultParamEntry> GetDefaultsFor(string typeName)
        {
            foreach (var typeDefaults in entries)
            {
                if (typeDefaults.TypeName == typeName)
                {
                    return typeDefaults.GetEntries();
                }
            }
            return Array.Empty<DefaultParamEntry>();
        }

        /// <summary>
        /// 1 つのノードタイプに対する複数の default param entry の SO 表現。
        /// </summary>
        [Serializable]
        public sealed class TypeDefaults
        {
            [SerializeField] private string typeName = "";
            [SerializeField] private List<RawEntry> rawEntries = new();

            public string TypeName => typeName;

            internal IReadOnlyList<DefaultParamEntry> GetEntries()
            {
                var result = new List<DefaultParamEntry>(rawEntries.Count);
                foreach (var raw in rawEntries)
                {
                    if (raw.TryToParamValue(out var pv))
                    {
                        result.Add(new DefaultParamEntry(raw.ParamName, pv));
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// 1 entry の serialize 用 raw 表現 (Unity Inspector で編集可能)。
        /// </summary>
        [Serializable]
        public sealed class RawEntry
        {
            [SerializeField] private string paramName = "";
            [SerializeField] private ParamType paramType = ParamType.Float;
            [SerializeField] private float floatValue;
            [SerializeField] private Color colorValue = Color.black;
            [SerializeField] private bool boolValue;

            public string ParamName => paramName;

            internal bool TryToParamValue(out ParamValue value)
            {
                value = paramType switch
                {
                    ParamType.Float => ParamValue.Float(floatValue),
                    ParamType.Color => ParamValue.Color(
                        new RzColor(colorValue.r, colorValue.g, colorValue.b, colorValue.a)),
                    ParamType.Bool => ParamValue.Bool(boolValue),
                    _ => ParamValue.Float(0f)
                };
                return true;
            }
        }
    }

    /// <summary>1 つの param に対する default 値の即値 representation。</summary>
    public readonly struct DefaultParamEntry
    {
        public string ParamName { get; }
        public ParamValue Value { get; }

        public DefaultParamEntry(string paramName, ParamValue value)
        {
            ParamName = paramName;
            Value = value;
        }
    }
}
