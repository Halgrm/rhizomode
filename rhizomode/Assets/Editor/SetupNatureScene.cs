using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Disambiguate: Rhizomode.Editor namespace の中では bare "Scene" が Rhizomode.Scene
// namespace を指してしまう。Unity の SceneManagement.Scene 型を別名で確保。
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Rhizomode.Editor
{
    /// <summary>
    /// TerrainDemoScene_URP から中央3x3タイル＋環境をNatureシーンにセットアップする。
    /// メニュー: Rhizomode/Setup Nature Scene
    /// </summary>
    public static class SetupNatureScene
    {
        // デモのカメラ位置(-95.75, -18.48, 110.77)をVR原点(0,0,0)に合わせるオフセット
        private static readonly Vector3 TerrainOffset = new(95f, 18f, -110f);

        private const string TerrainDataRoot = "Assets/TerrainDemoScene_URP/Terrain/Data/";

        // 3x3中央タイルのTerrainDataファイル名 [xi, yi] → TileIndices[xi], TileIndices[yi]
        private static readonly int[] TileIndices = { 1, 2, 3 };

        private static readonly string[,] TerrainDataFiles =
        {
            {
                "Terrain_1_1_4bf22c8f-eb55-436d-84e3-dedfa85665b1",
                "Terrain_1_2_1694bb0f-cffe-402c-b6f9-cf47692fbb78",
                "Terrain_1_3_c8bbd3cb-768c-4918-85c9-b01968beea93"
            },
            {
                "Terrain_2_1_82fc4922-5564-4a18-858e-787f6d901556",
                "Terrain_2_2_b6e03cf0-de91-4cae-b6af-0128d856ae43",
                "Terrain_2_3_f3ed9c1f-cb52-413a-a7d7-8e03961f4d5b"
            },
            {
                "Terrain_3_1_f87b3edd-12d5-4d51-aea4-88280910a3d8",
                "Terrain_3_2_77e49ac9-7f8e-47c2-ae00-57296acecb1c",
                "Terrain_3_3_9b11d05b-b237-4de7-82ed-a402f9cedebd"
            },
        };

        [MenuItem("Rhizomode/Setup Nature Scene")]
        public static void Execute()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/Nature.unity", OpenSceneMode.Single);

            // 既存オブジェクトをクリア
            foreach (var root in scene.GetRootGameObjects())
                Object.DestroyImmediate(root);

            // --- Terrain 3x3 ---
            CreateTerrainGroup(scene);

            // --- Directional Light (MorningSun相当) ---
            CreateDirectionalLight(scene);

            // --- Global Volume ---
            CreatePostProcessVolume(scene);

            // --- Wind ---
            CreateWindZone(scene);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SetupNatureScene] Nature scene setup complete! 3x3 terrain tiles + environment.");
        }

        private static void CreateTerrainGroup(UnityScene scene)
        {
            var parent = new GameObject("Terrain");
            // デモのTerrain親(-2000,-40,-2000) + VR原点オフセット
            parent.transform.position = new Vector3(-2000f, -40f, -2000f) + TerrainOffset;
            SceneManager.MoveGameObjectToScene(parent, scene);

            var terrains = new Terrain[3, 3];

            for (var xi = 0; xi < 3; xi++)
            {
                for (var yi = 0; yi < 3; yi++)
                {
                    var dataName = TerrainDataFiles[xi, yi];
                    var dataPath = TerrainDataRoot + dataName + ".asset";
                    var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);

                    if (terrainData == null)
                    {
                        Debug.LogError($"[SetupNatureScene] TerrainData not found: {dataPath}");
                        continue;
                    }

                    var go = Terrain.CreateTerrainGameObject(terrainData);
                    go.name = dataName;
                    go.transform.SetParent(parent.transform, false);
                    go.transform.localPosition = new Vector3(
                        TileIndices[xi] * 1000f, 0f, TileIndices[yi] * 1000f);
                    go.isStatic = true;

                    var terrain = go.GetComponent<Terrain>();
                    terrain.drawInstanced = true;
                    terrain.detailObjectDistance = 0f; // 草を無効化（VRパフォーマンス）
                    terrains[xi, yi] = terrain;
                }
            }

            // 隣接Terrain設定（シーム防止）
            for (var xi = 0; xi < 3; xi++)
            {
                for (var yi = 0; yi < 3; yi++)
                {
                    if (terrains[xi, yi] == null) continue;

                    var left = xi > 0 ? terrains[xi - 1, yi] : null;
                    var right = xi < 2 ? terrains[xi + 1, yi] : null;
                    var top = yi < 2 ? terrains[xi, yi + 1] : null;
                    var bot = yi > 0 ? terrains[xi, yi - 1] : null;
                    terrains[xi, yi].SetNeighbors(left, top, right, bot);
                }
            }
        }

        private static void CreateDirectionalLight(UnityScene scene)
        {
            var go = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(go, scene);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.color = new Color(1f, 0.92f, 0.8f);
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(22.8f, 217.5f, 0f);
        }

        private static void CreatePostProcessVolume(UnityScene scene)
        {
            var go = new GameObject("PostProcess Volume");
            SceneManager.MoveGameObjectToScene(go, scene);

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;

            // デモのVolumeProfileを共有（あれば）
            var profile = FindVolumeProfile();
            if (profile != null)
                volume.profile = profile;
            else
                Debug.Log("[SetupNatureScene] VolumeProfile not found — assign manually in Inspector");
        }

        private static VolumeProfile FindVolumeProfile()
        {
            // TerrainDemoScene内のVolumeProfileを検索
            var guids = AssetDatabase.FindAssets("t:VolumeProfile",
                new[] { "Assets/TerrainDemoScene_URP" });
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            }
            return null;
        }

        private static void CreateWindZone(UnityScene scene)
        {
            var go = new GameObject("Wind");
            SceneManager.MoveGameObjectToScene(go, scene);

            var wind = go.AddComponent<WindZone>();
            wind.windMain = 0.5f;
            wind.windTurbulence = 0.3f;
            go.transform.rotation = Quaternion.Euler(0f, 310f, 0f);
        }
    }
}
