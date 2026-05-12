#nullable enable
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Modules;
using System.Collections.Generic;

namespace Rhizomode.Editor
{
    /// <summary>
    /// デモ用VFX/Shaderモジュールアセットを一括生成するエディタツール。
    /// </summary>
    public static class DemoModuleCreator
    {
        [MenuItem("Rhizomode/Create Demo Modules")]
        public static void CreateAll()
        {
            CreateParticleBurstVFX();
            CreatePulseGridShader();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DemoModuleCreator] All demo modules created.");
        }

        private static void CreateParticleBurstVFX()
        {
            var shader = Shader.Find("Rhizomode/ParticleBurstAdditive");
            if (shader == null)
            {
                Debug.LogError("[DemoModuleCreator] Shader 'Rhizomode/ParticleBurstAdditive' not found");
                return;
            }

            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0f, 0.8f, 1f, 1f));
            mat.SetFloat("_Intensity", 1f);
            AssetDatabase.CreateAsset(mat, "Assets/VFX/ParticleBurst_Mat.mat");

            var go = new GameObject("ParticleBurst_VFX");

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.5f;
            main.startSpeed = 3f;
            main.startSize = 0.08f;
            main.startColor = new Color(0f, 0.8f, 1f, 1f);
            main.maxParticles = 5000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.3f;

            var emission = ps.emission;
            emission.rateOverTime = 50f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 200, 200, 1, 0.5f)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0, 0.8f, 1f), 0f),
                    new GradientColorKey(new Color(0, 0.2f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = gradient;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = mat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;

            // VisualEffect is ready for when a VFX Graph asset is authored
            var vfx = go.AddComponent<VisualEffect>();
            vfx.enabled = false;

            go.AddComponent<VFXModule>();

            const string prefabPath = "Assets/Prefabs/Modules/ParticleBurst_VFX.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            var def = ScriptableObject.CreateInstance<ModuleDefinition>();
            def.moduleName = "ParticleBurst";
            def.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            def.parameters = new List<ParamDefinition>
            {
                new() { name = "Intensity", type = ParamType.Float, defaultFloat = 1f, minFloat = 0f, maxFloat = 10f },
                new() { name = "BaseColor", type = ParamType.Color, defaultColor = new Color(0f, 0.8f, 1f, 1f) },
                new() { name = "Active", type = ParamType.Bool, defaultBool = true }
            };
            def.events = new List<string> { "Spawn", "Burst" };
            AssetDatabase.CreateAsset(def, "Assets/Data/ModuleDefinitions/ParticleBurst.asset");
            Debug.Log("[DemoModuleCreator] ParticleBurst VFX module created.");
        }

        private static void CreatePulseGridShader()
        {
            var shader = Shader.Find("Rhizomode/PulseGrid");
            if (shader == null)
            {
                Debug.LogError("[DemoModuleCreator] Shader 'Rhizomode/PulseGrid' not found");
                return;
            }

            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0f, 0.8f, 1f, 1f));
            mat.SetFloat("_Intensity", 1f);
            mat.SetFloat("_Speed", 1f);
            mat.SetFloat("_Active", 1f);
            mat.SetFloat("_GridScale", 10f);
            AssetDatabase.CreateAsset(mat, "Assets/Shaders/Modules/PulseGrid_Mat.mat");

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "PulseGrid_Shader";
            go.transform.localScale = new Vector3(5f, 5f, 1f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            var meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.material = mat;

            go.AddComponent<ShaderModule>();

            const string prefabPath = "Assets/Prefabs/Modules/PulseGrid_Shader.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            var def = ScriptableObject.CreateInstance<ModuleDefinition>();
            def.moduleName = "PulseGrid";
            def.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            def.parameters = new List<ParamDefinition>
            {
                new() { name = "Intensity", type = ParamType.Float, defaultFloat = 1f, minFloat = 0f, maxFloat = 10f },
                new() { name = "BaseColor", type = ParamType.Color, defaultColor = new Color(0f, 0.8f, 1f, 1f) },
                new() { name = "Speed", type = ParamType.Float, defaultFloat = 1f, minFloat = 0f, maxFloat = 10f },
                new() { name = "Active", type = ParamType.Bool, defaultBool = true }
            };
            def.events = new List<string>();
            AssetDatabase.CreateAsset(def, "Assets/Data/ModuleDefinitions/PulseGrid.asset");
            Debug.Log("[DemoModuleCreator] PulseGrid Shader module created.");
        }
    }
}
