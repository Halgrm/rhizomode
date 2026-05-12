#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.UI
{
    /// <summary>
    /// プリセット（部分グラフテンプレート）の保存・読込・一覧を管理する。
    /// プリセットはpersistentDataPath/Presets/にJSON形式で保存される。
    /// </summary>
    public class PresetManager : MonoBehaviour
    {
        private const string PresetDirectoryName = "Presets";
        private const string PresetExtension = ".json";

        private string _presetDirectory = "";

        /// <summary>プリセット保存時に発火。引数はファイル名。</summary>
        public event Action<string>? OnPresetSaved;

        /// <summary>プリセット読込時に発火。</summary>
        public event Action? OnPresetLoaded;

        private void Awake()
        {
            _presetDirectory = Path.Combine(Application.persistentDataPath, PresetDirectoryName);
            EnsureDirectory();
        }

        /// <summary>
        /// 現在のグラフ全体をプリセットとして保存する。
        /// </summary>
        public void SavePreset(GraphState context, string presetName)
        {
            try
            {
                EnsureDirectory();

                var preset = new PresetData
                {
                    presetName = presetName,
                    graphData = context.Serialize()
                };

                // ノード位置をセントロイド基準に正規化
                NormalizePositions(preset.graphData);

                var json = JsonUtility.ToJson(preset, true);
                var filename = SanitizeFilename(presetName) + PresetExtension;
                var path = Path.Combine(_presetDirectory, filename);

                File.WriteAllText(path, json);
                Debug.Log($"[PresetManager] Saved preset: {path}");
                OnPresetSaved?.Invoke(filename);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PresetManager] Save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// プリセットを読み込んで既存グラフに統合する。
        /// </summary>
        /// <returns>追加されたノードIDのリスト。失敗時は空リスト。</returns>
        public List<string> LoadPreset(GraphState context, string filename, Vector3 spawnOffset)
        {
            try
            {
                var path = Path.Combine(_presetDirectory, filename);
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[PresetManager] Preset not found: {path}");
                    return new List<string>();
                }

                var json = File.ReadAllText(path);
                var preset = JsonUtility.FromJson<PresetData>(json);
                if (preset?.graphData == null)
                {
                    Debug.LogWarning($"[PresetManager] Invalid preset data: {filename}");
                    return new List<string>();
                }

                var addedIds = context.MergePreset(preset.graphData, spawnOffset);
                Debug.Log($"[PresetManager] Loaded preset '{preset.presetName}': {addedIds.Count} nodes");
                OnPresetLoaded?.Invoke();
                return addedIds;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PresetManager] Load failed: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// 保存済みプリセットのファイル名一覧を返す。
        /// </summary>
        public string[] ListPresets()
        {
            try
            {
                EnsureDirectory();
                var files = Directory.GetFiles(_presetDirectory, $"*{PresetExtension}");
                var names = new string[files.Length];
                for (var i = 0; i < files.Length; i++)
                    names[i] = Path.GetFileName(files[i]);
                return names;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PresetManager] List failed: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// プリセットを削除する。
        /// </summary>
        public bool DeletePreset(string filename)
        {
            try
            {
                var path = Path.Combine(_presetDirectory, filename);
                if (!File.Exists(path)) return false;

                File.Delete(path);
                Debug.Log($"[PresetManager] Deleted preset: {filename}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PresetManager] Delete failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ノード位置をセントロイド基準に正規化する（ロード時のスポーン位置計算用）。
        /// </summary>
        private static void NormalizePositions(GraphData data)
        {
            if (data.nodes.Count == 0) return;

            // セントロイド計算
            float cx = 0f, cy = 0f, cz = 0f;
            foreach (var node in data.nodes)
            {
                var pos = node.ToVector3();
                cx += pos.x;
                cy += pos.y;
                cz += pos.z;
            }

            var count = data.nodes.Count;
            cx /= count;
            cy /= count;
            cz /= count;

            // セントロイドからの相対位置に変換
            foreach (var node in data.nodes)
            {
                var pos = node.ToVector3();
                node.position = new[]
                {
                    pos.x - cx,
                    pos.y - cy,
                    pos.z - cz
                };
            }
        }

        private static string SanitizeFilename(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private void EnsureDirectory()
        {
            if (!Directory.Exists(_presetDirectory))
                Directory.CreateDirectory(_presetDirectory);
        }
    }
}
