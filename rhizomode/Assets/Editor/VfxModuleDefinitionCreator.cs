#nullable enable

using System.Collections.Generic;
using Rhizomode.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Rhizomode.Editor
{
    /// <summary>
    /// VFXアセットからModuleDefinitionを自動生成するEditorウィンドウ。
    /// Rector の VfxAssetReader に相当する機能を提供する。
    /// </summary>
    public class VfxModuleDefinitionCreator : EditorWindow
    {
        private VisualEffectAsset? _vfxAsset;
        private string _moduleName = "";
        private string _savePath = "Assets/Data/ModuleDefinitions";
        private List<PropertyEntry> _discoveredProperties = new();
        private List<string> _discoveredEvents = new();
        private Vector2 _scrollPos;

        [MenuItem("Rhizomode/Create ModuleDefinition from VFX")]
        private static void ShowWindow()
        {
            var window = GetWindow<VfxModuleDefinitionCreator>("VFX → ModuleDefinition");
            window.minSize = new Vector2(420, 500);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("VFX → ModuleDefinition 自動生成", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawAssetSelection();
            EditorGUILayout.Space();

            if (_discoveredProperties.Count > 0 || _discoveredEvents.Count > 0)
            {
                DrawDiscoveredProperties();
                EditorGUILayout.Space();
                DrawCreateButton();
            }
        }

        private void DrawAssetSelection()
        {
            var newAsset = (VisualEffectAsset?)EditorGUILayout.ObjectField(
                "VFX Asset", _vfxAsset, typeof(VisualEffectAsset), false);

            if (newAsset != _vfxAsset)
            {
                _vfxAsset = newAsset;
                if (_vfxAsset != null)
                {
                    _moduleName = _vfxAsset.name;
                    DiscoverProperties();
                }
                else
                {
                    _discoveredProperties.Clear();
                    _discoveredEvents.Clear();
                }
            }

            _moduleName = EditorGUILayout.TextField("Module Name", _moduleName);
            _savePath = EditorGUILayout.TextField("Save Path", _savePath);
        }

        private void DrawDiscoveredProperties()
        {
            EditorGUILayout.LabelField($"検出: {_discoveredProperties.Count} params, {_discoveredEvents.Count} events",
                EditorStyles.miniLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MaxHeight(300));

            for (var i = 0; i < _discoveredProperties.Count; i++)
            {
                var prop = _discoveredProperties[i];
                EditorGUILayout.BeginHorizontal();
                prop.include = EditorGUILayout.Toggle(prop.include, GUILayout.Width(20));
                EditorGUILayout.LabelField(prop.name, GUILayout.Width(150));
                EditorGUILayout.LabelField(prop.paramType.ToString(), GUILayout.Width(60));

                if (prop.paramType == ParamType.Float)
                {
                    EditorGUILayout.LabelField("min", GUILayout.Width(25));
                    prop.min = EditorGUILayout.FloatField(prop.min, GUILayout.Width(50));
                    EditorGUILayout.LabelField("max", GUILayout.Width(28));
                    prop.max = EditorGUILayout.FloatField(prop.max, GUILayout.Width(50));
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Events (手動追加)", EditorStyles.boldLabel);
            for (var i = 0; i < _discoveredEvents.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _discoveredEvents[i] = EditorGUILayout.TextField(_discoveredEvents[i]);
                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    _discoveredEvents.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Event"))
            {
                _discoveredEvents.Add("");
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCreateButton()
        {
            if (!GUILayout.Button("ModuleDefinition を生成")) return;

            CreateModuleDefinition();
        }

        /// <summary>
        /// 一時的なVisualEffectインスタンスからExposedプロパティを検出する。
        /// </summary>
        private void DiscoverProperties()
        {
            _discoveredProperties.Clear();
            _discoveredEvents.Clear();

            if (_vfxAsset == null) return;

            // 一時GOでVisualEffectのExposedプロパティを列挙
            var tempGo = new GameObject("_VFX_Temp_Inspector");
            tempGo.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var vfx = tempGo.AddComponent<VisualEffect>();
                vfx.visualEffectAsset = _vfxAsset;

                // ExposedPropertiesをシリアライズ経由で列挙
                var so = new SerializedObject(vfx);
                so.Update();

                EnumerateFromVfxComponent(vfx);
            }
            finally
            {
                DestroyImmediate(tempGo);
            }
        }

        /// <summary>
        /// VisualEffectコンポーネントからExposedプロパティを列挙する。
        /// HasFloat/HasVector4/HasBool で存在確認しつつ取得。
        /// </summary>
        private void EnumerateFromVfxComponent(VisualEffect vfx)
        {
            // VFXParameterInfoを取得（Unity 6 API）
            var infos = new List<VFXExposedProperty>();
            vfx.visualEffectAsset.GetExposedProperties(infos);

            foreach (var info in infos)
            {
                var propName = info.name;

                // アンダースコア始まりは内部プロパティ、スキップ（Rector準拠）
                if (propName.StartsWith("_")) continue;

                var propType = info.type;

                if (propType == typeof(float))
                {
                    _discoveredProperties.Add(new PropertyEntry
                    {
                        name = propName,
                        paramType = ParamType.Float,
                        include = true,
                        min = 0f,
                        max = 1f,
                        defaultFloat = vfx.GetFloat(propName)
                    });
                }
                else if (propType == typeof(int))
                {
                    // intはfloatとして扱う（rhizomodeにint型がないため）
                    _discoveredProperties.Add(new PropertyEntry
                    {
                        name = propName,
                        paramType = ParamType.Float,
                        include = true,
                        min = 0f,
                        max = 100f,
                        defaultFloat = vfx.GetInt(propName)
                    });
                }
                else if (propType == typeof(Color))
                {
                    _discoveredProperties.Add(new PropertyEntry
                    {
                        name = propName,
                        paramType = ParamType.Color,
                        include = true,
                        defaultColor = vfx.GetVector4(propName)
                    });
                }
                else if (propType == typeof(Vector4))
                {
                    // Vector4もカラーとして扱う（VFX GraphのColor = Vector4）
                    _discoveredProperties.Add(new PropertyEntry
                    {
                        name = propName,
                        paramType = ParamType.Color,
                        include = true,
                        defaultColor = vfx.GetVector4(propName)
                    });
                }
                else if (propType == typeof(bool))
                {
                    _discoveredProperties.Add(new PropertyEntry
                    {
                        name = propName,
                        paramType = ParamType.Bool,
                        include = true,
                        defaultBool = vfx.GetBool(propName)
                    });
                }
            }

            // イベント名はVFXアセットのシリアライズデータから取得
            // VisualEffectAsset APIでは直接イベント列挙不可のため、
            // エディタ上でユーザーが手動追加する（Spawnなど）
        }

        private void CreateModuleDefinition()
        {
            var definition = ScriptableObject.CreateInstance<ModuleDefinition>();
            definition.moduleName = _moduleName;
            definition.parameters = new List<ParamDefinition>();
            definition.events = new List<string>(_discoveredEvents);

            foreach (var prop in _discoveredProperties)
            {
                if (!prop.include) continue;

                var paramDef = new ParamDefinition
                {
                    name = prop.name,
                    type = prop.paramType,
                    defaultFloat = prop.defaultFloat,
                    minFloat = prop.min,
                    maxFloat = prop.max,
                    defaultColor = prop.defaultColor,
                    defaultBool = prop.defaultBool
                };
                definition.parameters.Add(paramDef);
            }

            // 保存先ディレクトリを確保
            if (!AssetDatabase.IsValidFolder(_savePath))
            {
                CreateFolderRecursive(_savePath);
            }

            var assetPath = $"{_savePath}/{_moduleName}.asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AssetDatabase.CreateAsset(definition, assetPath);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = definition;

            Debug.Log($"[VfxModuleDefinitionCreator] Created: {assetPath} " +
                      $"({definition.parameters.Count} params, {definition.events.Count} events)");
        }

        private static void CreateFolderRecursive(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private class PropertyEntry
        {
            public string name = "";
            public ParamType paramType;
            public bool include = true;
            public float min;
            public float max = 1f;
            public float defaultFloat;
            public Color defaultColor;
            public bool defaultBool;
        }
    }
}
