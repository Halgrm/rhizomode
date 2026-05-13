#nullable enable

using System;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;

namespace Rhizomode.NodeCatalog.Runtime
{
    /// <summary>
    /// 1 つのノードタイプの完全な登録情報 (display + factory + runtime type)。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 (v4.9-4): NodeTypeInfo を表示メタ (Contracts) と生成情報 (Runtime) に分離。
    /// <see cref="NodeTypeAttributeScanner"/> が <see cref="NodeTypeAttribute"/> + 反射で構築する。
    /// </remarks>
    public sealed class NodeTypeRegistration
    {
        public NodeTypeDisplayInfo Display { get; }
        public Func<string, NodeBase> Factory { get; }
        public Type RuntimeType { get; }

        public NodeTypeRegistration(
            NodeTypeDisplayInfo display,
            Func<string, NodeBase> factory,
            Type runtimeType)
        {
            Display = display;
            Factory = factory;
            RuntimeType = runtimeType;
        }
    }
}
