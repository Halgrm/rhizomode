#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Rhizomode.Editor
{
    /// <summary>
    /// ModuleDefinitionのカスタムInspector。
    /// VFXアセットからパラメータ・イベントを自動読み取りする。
    /// Rector の VfxAssetReader の手法を参考に、.vfx YAMLパースでmin/max・イベント名も取得。
    /// </summary>
    [CustomEditor(typeof(ModuleDefinition))]
    public class ModuleDefinitionEditor : UnityEditor.Editor
    {
        private VisualEffectAsset? _vfxAsset;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var def = (ModuleDefinition)target;

            // --- GameBootstrap登録トグル ---
            DrawBootstrapRegistration(def);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("VFX 自動読み取り", EditorStyles.boldLabel);

            // PrefabにVisualEffectがあれば、そこからVFXアセットを自動検出
            if (_vfxAsset == null && def.prefab != null)
            {
                var vfx = def.prefab.GetComponent<VisualEffect>();
                if (vfx != null)
                    _vfxAsset = vfx.visualEffectAsset;
            }

            _vfxAsset = (VisualEffectAsset?)EditorGUILayout.ObjectField(
                "VFX Asset", _vfxAsset, typeof(VisualEffectAsset), false);

            if (_vfxAsset == null)
            {
                EditorGUILayout.HelpBox("VFXアセットを設定すると、パラメータとイベントを自動読み取りできます。", MessageType.Info);
                return;
            }

            if (GUILayout.Button("VFXからパラメータ同期"))
            {
                SyncFromVfxAsset(def, _vfxAsset);
            }
        }

        /// <summary>
        /// シーン上のGameBootstrap.moduleDefinitions配列への登録/解除トグルを描画する。
        /// </summary>
        private static void DrawBootstrapRegistration(ModuleDefinition def)
        {
            var bootstrap = FindBootstrap();
            if (bootstrap == null)
            {
                EditorGUILayout.HelpBox("シーンにGameBootstrapが見つかりません。", MessageType.Warning);
                return;
            }

            var so = new SerializedObject(bootstrap);
            var prop = so.FindProperty("moduleDefinitions");
            if (prop == null || !prop.isArray) return;

            // 現在登録されているか確認
            var isRegistered = false;
            var registeredIndex = -1;
            for (var i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == def)
                {
                    isRegistered = true;
                    registeredIndex = i;
                    break;
                }
            }

            EditorGUILayout.Space(6);
            var newValue = EditorGUILayout.ToggleLeft(
                "GameBootstrap に登録（Spawn可能）", isRegistered);

            if (newValue == isRegistered) return;

            so.Update();
            if (newValue)
            {
                // 配列末尾に追加
                prop.InsertArrayElementAtIndex(prop.arraySize);
                prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = def;
            }
            else if (registeredIndex >= 0)
            {
                // 配列から削除（objectReferenceをnullにしてからDeleteで完全除去）
                prop.GetArrayElementAtIndex(registeredIndex).objectReferenceValue = null;
                prop.DeleteArrayElementAtIndex(registeredIndex);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(bootstrap);
        }

        private static MonoBehaviour? FindBootstrap()
        {
            // GameBootstrapはRhizomode.XR名前空間なのでEditor asmdefからは型参照不可。
            // FindObjectsByTypeで名前マッチする。
            var allMono = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mono in allMono)
            {
                if (mono.GetType().Name == "GameBootstrap")
                    return mono;
            }
            return null;
        }

        private void SyncFromVfxAsset(ModuleDefinition def, VisualEffectAsset vfxAsset)
        {
            Undo.RecordObject(def, "Sync from VFX Asset");

            // モジュール名をVFXアセット名から設定（未設定の場合）
            if (string.IsNullOrEmpty(def.moduleName))
                def.moduleName = vfxAsset.name;

            // Prefab自動生成
            EnsurePrefab(def, vfxAsset);

            // .vfx YAMLからmin/max情報を取得（Rector方式）
            var yamlParamInfos = ReadParameterInfoFromYaml(vfxAsset);

            // GetExposedPropertiesでパラメータ取得
            SyncParameters(def, vfxAsset, yamlParamInfos);

            // .vfx YAMLからイベント名を取得（Rector方式）
            SyncEvents(def, vfxAsset);

            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ModuleDefinitionEditor] 同期完了: {def.moduleName} " +
                      $"({def.parameters.Count} params, {def.events.Count} events)");
        }

        /// <summary>
        /// Prefabが未設定の場合、VisualEffect + VFXModule付きPrefabを自動生成する。
        /// </summary>
        private static void EnsurePrefab(ModuleDefinition def, VisualEffectAsset vfxAsset)
        {
            var prefabDir = "Assets/Prefabs/Modules";

            if (def.prefab != null)
            {
                // 既存Prefabがあれば、VFXアセット参照だけ更新
                var prefabPath = AssetDatabase.GetAssetPath(def.prefab);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
                    var vfx = scope.prefabContentsRoot.GetComponent<VisualEffect>();
                    if (vfx != null) vfx.visualEffectAsset = vfxAsset;
                }
                return;
            }

            // ディレクトリ確保
            if (!AssetDatabase.IsValidFolder(prefabDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                AssetDatabase.CreateFolder("Assets/Prefabs", "Modules");
            }

            var go = new GameObject($"{vfxAsset.name}_VFX");
            var vfxComp = go.AddComponent<VisualEffect>();
            vfxComp.visualEffectAsset = vfxAsset;
            go.AddComponent<VFXModule>();

            var savePath = $"{prefabDir}/{vfxAsset.name}_VFX.prefab";
            savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

            def.prefab = PrefabUtility.SaveAsPrefabAsset(go, savePath);
            Object.DestroyImmediate(go);

            Debug.Log($"[ModuleDefinitionEditor] Prefab生成: {savePath}");
        }

        /// <summary>
        /// GetExposedProperties API + YAMLパース情報でパラメータリストを同期する。
        /// </summary>
        private static void SyncParameters(
            ModuleDefinition def, VisualEffectAsset vfxAsset,
            Dictionary<string, YamlParamInfo> yamlInfos)
        {
            var tempGo = new GameObject("_VFX_Temp");
            tempGo.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var vfx = tempGo.AddComponent<VisualEffect>();
                vfx.visualEffectAsset = vfxAsset;

                var exposed = new List<VFXExposedProperty>();
                vfxAsset.GetExposedProperties(exposed);

                def.parameters.Clear();

                // Gradient型パラメータ名を収集（サブプロパティも除外するため）
                var gradientNames = new HashSet<string>();
                foreach (var kvp in yamlInfos)
                {
                    if (kvp.Value.RealType == "Gradient")
                        gradientNames.Add(kvp.Key);
                }

                var addedNames = new HashSet<string>();

                foreach (var prop in exposed)
                {
                    if (prop.name.StartsWith("_")) continue;
                    if (!addedNames.Add(prop.name)) continue;

                    // Gradient型およびそのサブプロパティをスキップ
                    var isGradientRelated = false;
                    foreach (var gName in gradientNames)
                    {
                        if (prop.name == gName || prop.name.StartsWith(gName + " "))
                        {
                            isGradientRelated = true;
                            break;
                        }
                    }
                    if (isGradientRelated)
                    {
                        Debug.LogWarning(
                            $"[ModuleDefinitionEditor] Gradient型スキップ: {prop.name}");
                        continue;
                    }

                    var hasYaml = yamlInfos.TryGetValue(prop.name, out var yamlInfo);

                    var propType = prop.type;

                    if (propType == typeof(float))
                    {
                        def.parameters.Add(new ParamDefinition
                        {
                            name = prop.name,
                            type = ParamType.Float,
                            defaultFloat = vfx.GetFloat(prop.name),
                            minFloat = FiniteOr(hasYaml ? yamlInfo.Min : float.NegativeInfinity, 0f),
                            maxFloat = FiniteOr(hasYaml ? yamlInfo.Max : float.PositiveInfinity, 1f)
                        });
                    }
                    else if (propType == typeof(int))
                    {
                        def.parameters.Add(new ParamDefinition
                        {
                            name = prop.name,
                            type = ParamType.Float,
                            defaultFloat = vfx.GetInt(prop.name),
                            minFloat = FiniteOr(hasYaml ? yamlInfo.Min : float.NegativeInfinity, 0f),
                            maxFloat = FiniteOr(hasYaml ? yamlInfo.Max : float.PositiveInfinity, 100f)
                        });
                    }
                    else if (propType == typeof(Color) || propType == typeof(Vector4))
                    {
                        var v4 = vfx.GetVector4(prop.name);
                        def.parameters.Add(new ParamDefinition
                        {
                            name = prop.name,
                            type = ParamType.Color,
                            defaultColor = new Color(v4.x, v4.y, v4.z, v4.w)
                        });
                    }
                    else if (propType == typeof(bool))
                    {
                        def.parameters.Add(new ParamDefinition
                        {
                            name = prop.name,
                            type = ParamType.Bool,
                            defaultBool = vfx.GetBool(prop.name)
                        });
                    }
                    else
                    {
                        // Gradient等の非対応型はスキップ
                        Debug.LogWarning(
                            $"[ModuleDefinitionEditor] 非対応型スキップ: {prop.name} ({propType?.Name})");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(tempGo);
            }
        }

        /// <summary>
        /// .vfx YAMLから eventName フィールドを読み取ってイベントリストを同期する。
        /// Rector の VfxAssetReader.ReadEventNames に相当。
        /// </summary>
        private static void SyncEvents(ModuleDefinition def, VisualEffectAsset vfxAsset)
        {
            def.events.Clear();

            var assetPath = AssetDatabase.GetAssetPath(vfxAsset);
            if (string.IsNullOrEmpty(assetPath)) return;

            var text = File.ReadAllText(assetPath);

            // m_InitialEventName を検索（Spawnシステムのデフォルトイベント）
            var initMatch = Regex.Match(text, @"m_InitialEventName:\s*(.+)");
            if (initMatch.Success)
            {
                var initEvent = initMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(initEvent) && initEvent != "OnStop")
                    def.events.Add(initEvent);
            }

            // カスタム eventName フィールドを検索
            var matches = Regex.Matches(text, @"eventName:\s*(.+)");
            foreach (Match match in matches)
            {
                var eventName = match.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(eventName)) continue;
                if (eventName == "OnStop") continue;

                if (!def.events.Contains(eventName))
                    def.events.Add(eventName);
            }
        }

        /// <summary>
        /// .vfx YAMLの m_ParameterInfo からパラメータ名→min/maxの辞書を構築する。
        /// Rector の VfxAssetReader.ReadParameterInfo に相当（VYaml不使用、簡易パース）。
        /// </summary>
        private static Dictionary<string, YamlParamInfo> ReadParameterInfoFromYaml(
            VisualEffectAsset vfxAsset)
        {
            var result = new Dictionary<string, YamlParamInfo>();

            var assetPath = AssetDatabase.GetAssetPath(vfxAsset);
            if (string.IsNullOrEmpty(assetPath)) return result;

            var lines = File.ReadAllLines(assetPath);
            var inParameterInfo = false;
            string? currentName = null;
            string? currentRealType = null;
            var currentMin = float.NegativeInfinity;
            var currentMax = float.PositiveInfinity;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("m_ParameterInfo:"))
                {
                    inParameterInfo = true;
                    continue;
                }

                if (!inParameterInfo) continue;

                // m_ParameterInfo 配列の終了検出（インデント戻り）
                if (!line.StartsWith("  ") && !line.StartsWith("\t") && trimmed.Length > 0
                    && !trimmed.StartsWith("-") && !trimmed.StartsWith("name:"))
                {
                    FlushEntry(result, currentName, currentMin, currentMax, currentRealType);
                    break;
                }

                if (trimmed.StartsWith("- name:"))
                {
                    FlushEntry(result, currentName, currentMin, currentMax, currentRealType);

                    currentName = trimmed.Substring("- name:".Length).Trim();
                    currentRealType = null;
                    currentMin = float.NegativeInfinity;
                    currentMax = float.PositiveInfinity;
                }
                else if (trimmed.StartsWith("name:") && currentName == null)
                {
                    currentName = trimmed.Substring("name:".Length).Trim();
                    currentRealType = null;
                    currentMin = float.NegativeInfinity;
                    currentMax = float.PositiveInfinity;
                }
                else if (trimmed.StartsWith("realType:"))
                {
                    currentRealType = trimmed.Substring("realType:".Length).Trim();
                }
                else if (trimmed.StartsWith("min:"))
                {
                    if (float.TryParse(trimmed.Substring("min:".Length).Trim(), out var min))
                        currentMin = min;
                }
                else if (trimmed.StartsWith("max:"))
                {
                    if (float.TryParse(trimmed.Substring("max:".Length).Trim(), out var max))
                        currentMax = max;
                }
            }

            FlushEntry(result, currentName, currentMin, currentMax, currentRealType);

            return result;
        }

        private static void FlushEntry(
            Dictionary<string, YamlParamInfo> result,
            string? name, float min, float max, string? realType)
        {
            if (name == null) return;
            result[name] = new YamlParamInfo { Min = min, Max = max, RealType = realType };
        }

        /// <summary>有限値ならそのまま返し、Infinity/NaNならフォールバック値を返す。</summary>
        private static float FiniteOr(float value, float fallback)
            => float.IsFinite(value) ? value : fallback;

        private struct YamlParamInfo
        {
            public float Min;
            public float Max;
            public string? RealType;
        }
    }
}
