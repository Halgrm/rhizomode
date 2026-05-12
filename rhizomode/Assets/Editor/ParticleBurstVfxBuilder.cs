#nullable enable
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Rhizomode.Editor
{
    /// <summary>
    /// ParticleBurst用VFX Graphアセットを構築し、Exposed Propertyを追加してPrefabに割り当てる。
    /// VFX Graph Editor API は internal のため reflection で操作する。
    /// </summary>
    public static class ParticleBurstVfxBuilder
    {
        private const string VfxAssetPath = "Assets/VFX/ParticleBurst.vfx";
        private const string PrefabPath = "Assets/Prefabs/Modules/ParticleBurst_VFX.prefab";

        [MenuItem("Rhizomode/Build ParticleBurst VFX Graph")]
        public static void Build()
        {
            try
            {
                var asset = CreateVfxAsset();
                if (asset == null) return;

                AddExposedProperties();
                AssignToPrefab(asset);

                Debug.Log("[ParticleBurstVfxBuilder] VFX Graph built and assigned to prefab.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ParticleBurstVfxBuilder] Build failed: {e}");
            }
        }

        private static VisualEffectAsset? CreateVfxAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(VfxAssetPath);
            if (existing != null) return existing;

            // VisualEffectAssetEditorUtility.CreateNew<T> と同じYAML直書き方式
            const string emptyAsset =
@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &114350483966674976
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: 7d4c867f6b72b714dbb5fd1780afe208, type: 3}
--- !u!2058629511 &1
VisualEffectResource:
  m_Graph: {fileID: 114350483966674976}
";
            var fullPath = Path.Combine(Application.dataPath, "..", VfxAssetPath);
            File.WriteAllText(fullPath, emptyAsset);
            AssetDatabase.ImportAsset(VfxAssetPath);

            return AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(VfxAssetPath);
        }

        /// <summary>
        /// Reflection経由でVFXGraph内部APIにアクセスし、Exposed Propertyを追加する。
        /// </summary>
        private static void AddExposedProperties()
        {
            var editorAsm = FindVfxEditorAssembly();
            if (editorAsm == null) return;

            // VisualEffectResource は UnityEditor.VFX 名前空間、UnityEditor.VFXModule assembly
            Type? resType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                resType = asm.GetType("UnityEditor.VFX.VisualEffectResource");
                if (resType != null) break;
            }
            if (resType == null) return;

            var getRes = resType.GetMethod("GetResourceAtPath",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (getRes == null) return;

            var resource = getRes.Invoke(null, new object[] { VfxAssetPath });
            if (resource == null) return;

            var graphProp = resource.GetType().GetProperty("graph",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var graph = graphProp?.GetValue(resource);
            if (graph == null) return;

            var addChild = FindMethod(graph.GetType(), "AddChild");
            var paramType = editorAsm.GetType("UnityEditor.VFX.VFXParameter");
            if (paramType == null || addChild == null) return;

            AddParam(graph, addChild, paramType, "Intensity", typeof(float), 1f);
            AddParam(graph, addChild, paramType, "BaseColor", typeof(Color), new Color(0f, 0.8f, 1f, 1f));
            AddParam(graph, addChild, paramType, "Active", typeof(bool), true);

            // RecompileIfNeeded(bool, bool)
            var recompile = FindMethod(graph.GetType(), "RecompileIfNeeded");
            recompile?.Invoke(graph, new object[] { false, true });

            // WriteAsset()
            var writeAsset = FindMethod(resource.GetType(), "WriteAsset");
            writeAsset?.Invoke(resource, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(VfxAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void AddParam(object graph, MethodInfo addChild, Type paramType,
            string name, Type valueType, object defaultValue)
        {
            var param = ScriptableObject.CreateInstance(paramType);

            // Init(Type)
            FindMethod(paramType, "Init", new[] { typeof(Type) })
                ?.Invoke(param, new object[] { valueType });

            // SetSettingValue(string, object)
            var setSetting = FindMethod(paramType, "SetSettingValue",
                new[] { typeof(string), typeof(object) });
            if (setSetting != null)
            {
                setSetting.Invoke(param, new object[] { "m_ExposedName", name });
                setSetting.Invoke(param, new object[] { "m_Exposed", true });
            }
            else
            {
                // フォールバック: 直接フィールド設定
                paramType.GetField("m_ExposedName",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(param, name);
                paramType.GetField("m_Exposed",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(param, true);
            }

            // value = defaultValue
            try
            {
                paramType.GetProperty("value",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.SetValue(param, defaultValue);
            }
            catch { /* 型不一致時はデフォルトのまま */ }

            addChild.Invoke(graph, new object[] { param, -1, true });
        }

        private static void AssignToPrefab(VisualEffectAsset vfxAsset)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;

            using var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath);
            var root = scope.prefabContentsRoot;

            var vfx = root.GetComponent<VisualEffect>();
            if (vfx != null)
            {
                vfx.visualEffectAsset = vfxAsset;
                vfx.enabled = true;
            }

            var ps = root.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                emission.enabled = false;
            }
        }

        private static MethodInfo? FindMethod(Type type, string name, Type[]? paramTypes = null)
        {
            var t = type;
            while (t != null)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static
                                           | BindingFlags.Public | BindingFlags.NonPublic;
                var method = paramTypes != null
                    ? t.GetMethod(name, flags, null, paramTypes, null)
                    : t.GetMethod(name, flags);
                if (method != null) return method;
                t = t.BaseType;
            }
            return null;
        }

        private static Assembly? FindVfxEditorAssembly()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Unity.VisualEffectGraph.Editor")
                    return asm;
            }
            return null;
        }
    }
}
