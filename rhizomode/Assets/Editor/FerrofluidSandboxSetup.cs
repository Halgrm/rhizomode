using Rhizomode.Modules.Ferrofluid;
using Rhizomode.Scene.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class FerrofluidSandboxSetup
{
    [MenuItem("Rhizomode/Ferrofluid/Debug: List Module Type Registrations")]
    public static void DebugListModuleTypes()
    {
        // GraphState の factory リストをリフレクションで覗いて Ferrofluid 関連 typeName を出力。
        // VContainer の LifetimeScope.Container.Resolve は Rhizomode.Editor の asmdef 境界違反
        // (Plan v5.4 §15「VContainer は Bootstrap 専用」) になるため、scene 上の GraphState を
        // 持つ MonoBehaviour (GraphStateBehaviour 等) を Object.FindFirstObjectByType で拾う。
        var scenes = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        Rhizomode.Graph.Model.GraphState graphState = null;
        foreach (var mb in scenes)
        {
            // GraphStateBehaviour に "GraphState" public field/property がある (旧 GameBootstrap 経由配線)
            var t = mb.GetType();
            var prop = t.GetProperty("GraphState",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop?.PropertyType == typeof(Rhizomode.Graph.Model.GraphState))
            {
                graphState = (Rhizomode.Graph.Model.GraphState)prop.GetValue(mb);
                if (graphState != null) break;
            }
        }
        if (graphState == null)
        {
            Debug.LogError("[FerrofluidDebug] GraphState not found — must be in Play mode and scene wired.");
            return;
        }
        try
        {
            var factoriesField = typeof(Rhizomode.Graph.Model.GraphState).GetField(
                "_nodeFactories",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var factories = factoriesField?.GetValue(graphState) as System.Collections.IDictionary;
            if (factories == null) { Debug.LogError("[FerrofluidDebug] factories dict not found"); return; }

            var keys = new System.Collections.Generic.List<string>();
            foreach (var k in factories.Keys) keys.Add(k.ToString());
            keys.Sort();
            var ferro = keys.FindAll(k => k.Contains("Ferro", System.StringComparison.OrdinalIgnoreCase));
            Debug.Log($"[FerrofluidDebug] Total factory keys: {keys.Count}. Ferrofluid related: [{string.Join(", ", ferro)}]");
            var modules = keys.FindAll(k => k.StartsWith("Module_"));
            Debug.Log($"[FerrofluidDebug] All Module_* keys: [{string.Join(", ", modules)}]");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[FerrofluidDebug] {ex.Message}");
        }
    }

    [MenuItem("Rhizomode/Scenes/Add SceneEnvironment to casle")]
    public static void AddSceneEnvironmentToCasle()
    {
        const string scenePath = "Assets/Scenes/casle.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[FerrofluidSandboxSetup] Failed to open scene: {scenePath}");
            return;
        }

        // 既存の SceneEnvironment があれば早期 return
        foreach (var root in scene.GetRootGameObjects())
        {
            var existing = root.GetComponentInChildren<SceneEnvironment>(true);
            if (existing != null)
            {
                Debug.Log($"[FerrofluidSandboxSetup] casle scene already has SceneEnvironment: {existing.gameObject.name}");
                return;
            }
        }

        // 現在の RenderSettings を SceneEnvironment SerializedField に直接書き込む。
        // SceneEnvironment の public Apply() で逆方向 (SO→RenderSettings) は実装済みだが、
        // 逆 (RenderSettings → SO) は無いので SerializedObject 経由で書く。
        var go = new GameObject("_SceneEnvironment_casle");
        SceneManager.MoveGameObjectToScene(go, scene);
        var env = go.AddComponent<SceneEnvironment>();

        var so = new SerializedObject(env);
        so.FindProperty("skyboxMaterial").objectReferenceValue = RenderSettings.skybox;
        so.FindProperty("ambientMode").enumValueIndex = (int)RenderSettings.ambientMode;
        so.FindProperty("ambientSkyColor").colorValue = RenderSettings.ambientSkyColor;
        so.FindProperty("ambientEquatorColor").colorValue = RenderSettings.ambientEquatorColor;
        so.FindProperty("ambientGroundColor").colorValue = RenderSettings.ambientGroundColor;
        so.FindProperty("ambientIntensity").floatValue = RenderSettings.ambientIntensity;
        so.FindProperty("fogEnabled").boolValue = RenderSettings.fog;
        so.FindProperty("fogColor").colorValue = RenderSettings.fogColor;
        so.FindProperty("fogMode").enumValueIndex = (int)RenderSettings.fogMode;
        so.FindProperty("fogDensity").floatValue = RenderSettings.fogDensity;
        so.FindProperty("fogStartDistance").floatValue = RenderSettings.fogStartDistance;
        so.FindProperty("fogEndDistance").floatValue = RenderSettings.fogEndDistance;
        so.FindProperty("reflectionMode").enumValueIndex = (int)RenderSettings.defaultReflectionMode;
        so.FindProperty("reflectionIntensity").floatValue = RenderSettings.reflectionIntensity;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[FerrofluidSandboxSetup] Added SceneEnvironment to casle scene at root.");
    }

    [MenuItem("Rhizomode/Ferrofluid/Create Module Prefab + Definition")]
    public static void CreateModuleAssets()
    {
        // 1. Prefab を作る
        var root = new GameObject("Ferrofluid_Module");
        var module = root.AddComponent<FerrofluidModule>();
        var spawner = root.AddComponent<FerrofluidBallSpawner>();

        // material をリフレクションで割当 (private field)
        var spawnerType = typeof(FerrofluidBallSpawner);
        var matField = spawnerType.GetField("ballMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (matField != null)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Shaders/Ferrofluid/Ferrofluid_Black.mat");
            matField.SetValue(spawner, mat);
        }
        var moduleSpawnerField = typeof(FerrofluidModule).GetField("spawner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        moduleSpawnerField?.SetValue(module, spawner);

        const string prefabPath = "Assets/Prefabs/Ferrofluid_Module.prefab";
        var dir = System.IO.Path.GetDirectoryName(prefabPath);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir!);
            AssetDatabase.Refresh();
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        // 2. ModuleDefinition SO
        const string defPath = "Assets/Data/ModuleDefinitions/Ferrofluid.asset";
        var def = AssetDatabase.LoadAssetAtPath<Rhizomode.Modules.ModuleDefinition>(defPath);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<Rhizomode.Modules.ModuleDefinition>();
            AssetDatabase.CreateAsset(def, defPath);
        }
        def.moduleName = "Ferrofluid";
        def.prefab = prefab;
        def.parameters = new System.Collections.Generic.List<Rhizomode.Graph.Model.ParamDefinition>
        {
            new() { name = "Count", type = Rhizomode.SharedKernel.ParamType.Float, defaultFloat = 12, minFloat = 1, maxFloat = 100, defaultBool = true },
            new() { name = "WaveTrigger", type = Rhizomode.SharedKernel.ParamType.Bool, defaultBool = false, isEvent = true },
            new() { name = "MoveTrigger", type = Rhizomode.SharedKernel.ParamType.Bool, defaultBool = false, isEvent = true },
            new() { name = "OutlineTrigger", type = Rhizomode.SharedKernel.ParamType.Bool, defaultBool = false, isEvent = true },
        };
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FerrofluidSandboxSetup] Module prefab + definition created at {prefabPath} / {defPath}");
    }

    [MenuItem("Rhizomode/Ferrofluid/Trigger Wave _F1")]
    public static void TriggerWave()
    {
        var s = Object.FindFirstObjectByType<FerrofluidBallSpawner>();
        if (s != null) s.TriggerWave();
    }

    [MenuItem("Rhizomode/Ferrofluid/Trigger Random Move _F2")]
    public static void TriggerRandomMove()
    {
        var s = Object.FindFirstObjectByType<FerrofluidBallSpawner>();
        if (s != null) s.TriggerRandomMove();
    }

    [MenuItem("Rhizomode/Ferrofluid/Trigger Outline FX _F3")]
    public static void TriggerOutlineFx()
    {
        var s = Object.FindFirstObjectByType<FerrofluidBallSpawner>();
        if (s != null) s.TriggerOutlineFx();
    }

    [MenuItem("Rhizomode/Ferrofluid/Setup Bloom Volume")]
    public static void SetupBloomVolume()
    {
        // Volume Profile 作成 or 取得
        var profilePath = "Assets/Shaders/Ferrofluid/Ferrofluid_Volume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        // Bloom 設定
        if (!profile.TryGet<Bloom>(out var bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 1.2f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.6f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.85f;
        bloom.tint.overrideState = true;
        bloom.tint.value = new Color(0.5f, 0.7f, 1.0f);

        // Tonemapping (HDR を画面に焼く)
        if (!profile.TryGet<Tonemapping>(out var tonemap))
        {
            tonemap = profile.Add<Tonemapping>(true);
        }
        tonemap.mode.overrideState = true;
        tonemap.mode.value = TonemappingMode.Neutral;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // Scene の Global Volume にアタッチ
        var volGo = GameObject.Find("Global Volume");
        if (volGo != null)
        {
            var vol = volGo.GetComponent<Volume>();
            if (vol == null) vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.profile = profile;
            EditorUtility.SetDirty(vol);
        }

        // Camera に post-processing を enable
        var camGo = GameObject.Find("Camera");
        if (camGo != null)
        {
            var camData = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null) camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            EditorUtility.SetDirty(camGo);
        }

        Debug.Log("[FerrofluidSandboxSetup] Bloom volume set up.");
    }
}
