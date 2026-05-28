using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Rhizomode.Editor
{
    /// <summary>
    /// Runtime probe for the NDI subsystem. Triggered from the menu in Play Mode.
    /// Dumps everything to the Console so the MCP read_console call can scrape it.
    /// </summary>
    public static class NdiRuntimeDiagnostic
    {
        [MenuItem("Rhizomode/Diagnostics/NDI Runtime Probe")]
        public static void Run()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== NDI RUNTIME PROBE ===");

            // 1. Klak.Ndi source enumeration (reflection to avoid asmdef ref)
            try
            {
                System.Type finderType = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    finderType = asm.GetType("Klak.Ndi.NdiFinder");
                    if (finderType != null) break;
                }
                if (finderType == null)
                {
                    sb.AppendLine($"[1] Klak.Ndi.NdiFinder type NOT FOUND in loaded assemblies");
                }
                else
                {
                    var srcProp = finderType.GetProperty("sourceNames", BindingFlags.Static | BindingFlags.Public);
                    var srcVal = srcProp?.GetValue(null) as System.Collections.IEnumerable;
                    int n = 0;
                    if (srcVal != null) foreach (var x in srcVal) { sb.AppendLine($"    src: '{x}'"); n++; }
                    sb.AppendLine($"[1] NdiFinder source count: {n}");
                }
            }
            catch (System.Exception e)
            {
                sb.AppendLine($"[1] NdiFinder exception: {e.GetType().Name}: {e.Message}");
            }

            // 2. NdiWindowsRoot in scene
            var rootType = System.Type.GetType("Rhizomode.UI.NdiWindowsRoot, Rhizomode.UI.Presentation");
            sb.AppendLine($"[2] NdiWindowsRoot type lookup: {(rootType != null ? "ok" : "NOT FOUND")}");
            if (rootType != null)
            {
                var roots = Object.FindObjectsByType(rootType, FindObjectsSortMode.None);
                sb.AppendLine($"    instance count: {roots.Length}");
                foreach (Component r in roots)
                {
                    sb.AppendLine($"    on '{r.gameObject.name}' children={r.transform.childCount}");
                    for (int i = 0; i < r.transform.childCount; i++)
                    {
                        var c = r.transform.GetChild(i);
                        var rend = c.GetComponentInChildren<Renderer>();
                        sb.AppendLine($"      [{i}] {c.name} pos={c.position} active={c.gameObject.activeSelf} rEnabled={rend?.enabled} mat={rend?.sharedMaterial?.name}");
                    }
                }
            }

            // 3. NdiReceiverPresenter in scene
            var presenterType = System.Type.GetType("Rhizomode.UI.NdiReceiverPresenter, Rhizomode.UI.Presentation");
            sb.AppendLine($"[3] NdiReceiverPresenter type lookup: {(presenterType != null ? "ok" : "NOT FOUND")}");
            if (presenterType != null)
            {
                var presenters = Object.FindObjectsByType(presenterType, FindObjectsSortMode.None);
                sb.AppendLine($"    instance count: {presenters.Length}");
                foreach (Component p in presenters)
                {
                    var nodeField = presenterType.GetField("_node", BindingFlags.Instance | BindingFlags.NonPublic);
                    var node = nodeField?.GetValue(p);
                    var sourceProp = node?.GetType().GetProperty("SourceName");
                    var source = sourceProp?.GetValue(node);
                    var nodeIdField = presenterType.GetField("_nodeId", BindingFlags.Instance | BindingFlags.NonPublic);
                    var nodeId = nodeIdField?.GetValue(p);
                    var windowField = presenterType.GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic);
                    var win = windowField?.GetValue(p);
                    var rootField = presenterType.GetField("_windowsRoot", BindingFlags.Instance | BindingFlags.NonPublic);
                    var rootVal = rootField?.GetValue(p);
                    sb.AppendLine($"    on '{p.gameObject.name}' nodeId='{nodeId}' source='{source}' window={(win != null ? "ok" : "null")} root={(rootVal != null ? "ok" : "null")}");

                    // Klak.Ndi.NdiReceiver on same go (reflection)
                    System.Type recvType = null;
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        recvType = asm.GetType("Klak.Ndi.NdiReceiver");
                        if (recvType != null) break;
                    }
                    if (recvType != null)
                    {
                        var recv = p.GetComponent(recvType);
                        if (recv != null)
                        {
                            var ndiNameProp = recvType.GetProperty("ndiName");
                            var tgtTexProp = recvType.GetProperty("targetTexture");
                            var nm = ndiNameProp?.GetValue(recv) as string;
                            var tt = tgtTexProp?.GetValue(recv) as Texture;
                            sb.AppendLine($"      receiver ndiName='{nm}' tgtTexture={(tt != null ? tt.name : "null")} size={FormatTextureSize(tt)}");
                        }
                        else
                        {
                            sb.AppendLine($"      no Klak.Ndi.NdiReceiver attached");
                        }
                    }
                }
            }

            // 4. Static contexts
            var ctxType = System.Type.GetType("Rhizomode.UI.NdiPresentationContext, Rhizomode.UI.Presentation");
            if (ctxType != null)
            {
                var hField = ctxType.GetField("Health", BindingFlags.Static | BindingFlags.Public);
                var wField = ctxType.GetField("WindowsRoot", BindingFlags.Static | BindingFlags.Public);
                sb.AppendLine($"[4] NdiPresentationContext.Health={(hField?.GetValue(null) != null ? "set" : "null")} WindowsRoot={(wField?.GetValue(null) != null ? "set" : "null")}");
            }
            else sb.AppendLine("[4] NdiPresentationContext type NOT FOUND");

            // 5. All NdiReceiverNode instances in graph
            var graphCtxType = System.Type.GetType("Rhizomode.Graph.Context.GraphContextBehaviour, Rhizomode.Graph.Context");
            if (graphCtxType != null)
            {
                var graphBehavs = Object.FindObjectsByType(graphCtxType, FindObjectsSortMode.None);
                sb.AppendLine($"[5] GraphContextBehaviour instances: {graphBehavs.Length}");
                foreach (Component gb in graphBehavs)
                {
                    var ctxField = graphCtxType.GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);
                    var ctx = ctxField?.GetValue(gb);
                    if (ctx == null) { sb.AppendLine("    _context null"); continue; }
                    var nodesProp = ctx.GetType().GetProperty("Nodes");
                    if (nodesProp == null) { sb.AppendLine("    Nodes prop not found"); continue; }
                    var nodes = nodesProp.GetValue(ctx) as System.Collections.IEnumerable;
                    int total = 0; int ndi = 0;
                    if (nodes != null)
                    {
                        foreach (var n in nodes)
                        {
                            total++;
                            var typeName = n.GetType().Name;
                            if (typeName == "NdiReceiverNode")
                            {
                                ndi++;
                                var idProp = n.GetType().GetProperty("Id");
                                var srcProp = n.GetType().GetProperty("SourceName");
                                sb.AppendLine($"      NDI node id='{idProp?.GetValue(n)}' source='{srcProp?.GetValue(n)}'");
                            }
                        }
                    }
                    sb.AppendLine($"    graph has {total} nodes, {ndi} NDI receivers");
                }
            }
            else sb.AppendLine("[5] GraphContextBehaviour type NOT FOUND");

            var outPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ndi_probe.txt");
            System.IO.File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[NDI PROBE] wrote {outPath}");
            Debug.LogError($"[NDI PROBE] {sb}");
        }

        private static string FormatTextureSize(Texture tex)
        {
            return tex == null ? "null" : $"{tex.width}x{tex.height}";
        }
    }
}
