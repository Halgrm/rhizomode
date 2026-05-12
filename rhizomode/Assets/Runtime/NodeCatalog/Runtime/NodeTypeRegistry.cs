#nullable enable

using System.Collections.Generic;
using System.Linq;

using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.NodeCatalog.Runtime
{
    /// <summary>
    /// 全ノードタイプのUI情報を管理するレジストリ。
    /// ノード生成メニューとノードVisual生成が参照する。
    /// </summary>
    public class NodeTypeRegistry
    {
        private readonly Dictionary<string, NodeTypeInfo> _registry = new();

        public IReadOnlyDictionary<string, NodeTypeInfo> AllTypes => _registry;

        /// <summary>
        /// ノードタイプ情報を登録する。
        /// </summary>
        public void Register(NodeTypeInfo info)
        {
            _registry[info.TypeName] = info;
        }

        /// <summary>
        /// タイプ名からノード情報を取得する。未登録の場合null。
        /// </summary>
        public NodeTypeInfo? GetInfo(string typeName)
        {
            return _registry.TryGetValue(typeName, out var info) ? info : null;
        }

        /// <summary>
        /// 指定カテゴリのノードタイプ一覧を返す。
        /// </summary>
        public IEnumerable<NodeTypeInfo> GetByCategory(NodeCategory category)
        {
            return _registry.Values.Where(info => info.Category == category);
        }
    }
}
