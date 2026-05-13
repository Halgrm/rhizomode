#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;
using UnityEngine;

namespace Rhizomode.NodeCatalog.Runtime
{
    /// <summary>
    /// AppDomain 上の全アセンブリを反射で走査し、<see cref="NodeTypeAttribute"/> 付き
    /// NodeBase 派生クラスを <see cref="NodeTypeRegistration"/> に変換するスキャナ。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 4: GameBootstrap のハードコード factory map (NodeFactoryMap, 38 entries) を
    /// 廃止し、本スキャナの出力で置換する。Plan v5.3 では Nodes.Standard / Audio / OscMidi /
    /// Ableton / Scene の 5 asmdef 横断で 39 typeName を出力する想定 (動的 VFX/Shader/Object3D は別)。
    ///
    /// 起動時に 1 回実行 → <see cref="NodeTypeRegistry"/> に投入する。
    /// </remarks>
    public sealed class NodeTypeAttributeScanner
    {
        private IReadOnlyList<NodeTypeRegistration>? _cache;

        /// <summary>
        /// 全ロード済みアセンブリを走査して [NodeType] 付きクラスを抽出する。
        /// 初回呼び出しのみ実走査し、以降はキャッシュを返す (Codex review #1)。
        /// </summary>
        public IReadOnlyList<NodeTypeRegistration> Scan()
        {
            if (_cache != null) return _cache;

            var results = new List<NodeTypeRegistration>();
            var seen = new HashSet<string>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray()!;
                    Debug.LogWarning(
                        $"[NodeTypeAttributeScanner] Partial type load in {asm.GetName().Name}: {e.Message}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"[NodeTypeAttributeScanner] Skip assembly {asm.GetName().Name}: {e.Message}");
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null) continue;
                    if (type.IsAbstract) continue;
                    if (type.IsGenericTypeDefinition || type.ContainsGenericParameters) continue;
                    if (!typeof(NodeBase).IsAssignableFrom(type)) continue;

                    var attr = type.GetCustomAttribute<NodeTypeAttribute>(inherit: false);
                    if (attr == null) continue;

                    if (!seen.Add(attr.TypeName))
                    {
                        Debug.LogWarning(
                            $"[NodeTypeAttributeScanner] Duplicate typeName '{attr.TypeName}' " +
                            $"(second occurrence on {type.FullName} ignored)");
                        continue;
                    }

                    var factory = BuildFactory(type, attr.TypeName);
                    if (factory == null) continue;

                    var display = new NodeTypeDisplayInfo(
                        typeName: attr.TypeName,
                        label: attr.Label,
                        description: string.Empty,
                        category: attr.Category,
                        colorKey: attr.Category.ToString(),
                        iconId: attr.Category.ToString());

                    results.Add(new NodeTypeRegistration(display, factory, type));
                }
            }

            _cache = results;
            return results;
        }

        private static Func<string, NodeBase>? BuildFactory(Type type, string typeName)
        {
            var ctor = type.GetConstructor(new[] { typeof(string) });
            if (ctor == null)
            {
                Debug.LogWarning(
                    $"[NodeTypeAttributeScanner] {type.FullName} has [NodeType('{typeName}')] " +
                    "but no public constructor (string id). Skipping.");
                return null;
            }

            return id =>
            {
                try
                {
                    return (NodeBase)ctor.Invoke(new object[] { id });
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[NodeTypeAttributeScanner] Factory invoke failed for '{typeName}': {e.Message}");
                    // フォールバックは呼び出し側 (Registry / Bootstrap) で処理する
                    throw;
                }
            };
        }
    }
}
