#nullable enable

using System.Collections.Generic;
using Rhizomode.Graph.CatalogBridge;
using Rhizomode.Graph.Model;

namespace Rhizomode.NodeCatalog.Runtime
{
    /// <summary>
    /// 複数の <see cref="INodeFactory"/> を順次試す合成 factory。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 4 follow-up: 静的 (<see cref="AttributeScannerNodeFactory"/>) と
    /// 動的 (<see cref="Nodes.Scene.SceneTriggerNodeFactory"/>, Module/Object3D factory 等) を
    /// 1 つの contract に束ねる。
    ///
    /// 競合解決ポリシー (Codex review 由来の明示化):
    /// - 先頭から順に <see cref="INodeFactory.CanCreate"/> を試し、**最初に true を返した factory** が勝つ。
    /// - 同じ typeName を複数の factory が返す場合、後ろの factory は silent shadow される。
    /// - 推奨される登録順: 静的 (Scanner) を先頭、動的 (SceneTrigger/Module/Object3D) を後続。
    ///   静的が動的 typeName を奪うことは [NodeType] 属性付与のミス意外発生しない。
    /// - 構築時に <see cref="DetectDuplicateTypeNames"/> で重複検出して呼び出し側に通知できる
    ///   (Bootstrap/Test で UnityEngine.Debug.LogWarning するなど任意)。
    /// </remarks>
    public sealed class CompositeNodeFactory : INodeFactory
    {
        private readonly IReadOnlyList<INodeFactory> _factories;

        public CompositeNodeFactory(IReadOnlyList<INodeFactory> factories)
        {
            _factories = factories;
        }

        public bool CanCreate(string typeName)
        {
            foreach (var f in _factories)
            {
                if (f.CanCreate(typeName)) return true;
            }
            return false;
        }

        public NodeBase? Create(string typeName, string nodeId)
        {
            foreach (var f in _factories)
            {
                if (f.CanCreate(typeName)) return f.Create(typeName, nodeId);
            }
            return null;
        }

        /// <summary>
        /// 合成された factory 群に対して、同じ typeName が複数の factory に登録されている
        /// ケースを列挙する。Bootstrap / Test の起動時診断で重複を検知するために使う。
        /// </summary>
        /// <remarks>
        /// 個々の <see cref="INodeFactory"/> 実装が <c>AllTypeNames</c> プロパティを (規約として)
        /// 持つことを期待する (interface には含まれない optional)。取得できない factory は無視される。
        /// </remarks>
        public IEnumerable<string> DetectDuplicateTypeNames()
        {
            var seen = new HashSet<string>();
            var duplicates = new HashSet<string>();

            foreach (var f in _factories)
            {
                var allTypeNamesProp = f.GetType().GetProperty("AllTypeNames");
                if (allTypeNamesProp == null) continue;
                if (allTypeNamesProp.GetValue(f) is not System.Collections.IEnumerable typeNames) continue;

                foreach (var obj in typeNames)
                {
                    if (obj is not string typeName) continue;
                    if (!seen.Add(typeName)) duplicates.Add(typeName);
                }
            }

            return duplicates;
        }
    }
}
