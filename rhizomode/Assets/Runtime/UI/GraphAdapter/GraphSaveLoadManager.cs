#nullable enable

using System;
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
    /// グラフのセーブ/ロードを管理する。persistentDataPath配下にJSON形式で保存。
    /// </summary>
    public class GraphSaveLoadManager : MonoBehaviour
    {
        private const string SaveDirectory = "SavedGraphs";
        private const string FileExtension = ".json";
        private const string DefaultPrefix = "live_set_";

        private GraphContextBehaviour? _graphContext;
        private string _saveDirectoryPath = "";

        /// <summary>
        /// グラフ保存完了時に発火するイベント。引数はファイル名。
        /// </summary>
        public event Action<string>? OnGraphSaved;

        /// <summary>
        /// グラフ読み込み完了時に発火するイベント。NodeVisualManagerがビジュアル再構築に使用。
        /// </summary>
        public event Action? OnGraphLoaded;

        /// <summary>
        /// セーブディレクトリの絶対パス。
        /// </summary>
        public string SaveDirectoryPath => _saveDirectoryPath;

        /// <summary>
        /// 初期化。GameBootstrapから呼び出される。
        /// </summary>
        /// <param name="graphContext">グラフコンテキストのMonoBehaviourラッパー</param>
        public void Initialize(GraphContextBehaviour graphContext)
        {
            _graphContext = graphContext;
            _saveDirectoryPath = Path.Combine(Application.persistentDataPath, SaveDirectory);
            EnsureSaveDirectory();
        }

        /// <summary>
        /// 現在のグラフをJSON形式で保存する。
        /// </summary>
        /// <param name="fileName">ファイル名（拡張子なし可）。nullまたは空文字の場合はタイムスタンプで自動生成。</param>
        /// <returns>保存成功でtrue。</returns>
        public bool SaveGraph(string? fileName = null)
        {
            if (_graphContext == null)
            {
                Debug.LogError("[GraphSaveLoadManager] Not initialized. Call Initialize() first.");
                return false;
            }

            var resolvedName = ResolveFileName(fileName);
            var filePath = GetFilePath(resolvedName);

            try
            {
                var graphData = _graphContext.Context.Serialize();
                var json = JsonUtility.ToJson(graphData, true);
                File.WriteAllText(filePath, json);

                Debug.Log($"[GraphSaveLoadManager] Saved: {filePath}");
                OnGraphSaved?.Invoke(resolvedName);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphSaveLoadManager] Save failed: {filePath} — {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// JSONファイルからグラフを復元する。既存グラフはクリアされる。
        /// </summary>
        /// <param name="fileName">ファイル名（拡張子なし可）。</param>
        /// <returns>読み込み成功でtrue。</returns>
        public bool LoadGraph(string fileName)
        {
            if (_graphContext == null)
            {
                Debug.LogError("[GraphSaveLoadManager] Not initialized. Call Initialize() first.");
                return false;
            }

            var resolvedName = EnsureExtension(fileName);
            var filePath = GetFilePath(resolvedName);

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[GraphSaveLoadManager] File not found: {filePath}");
                return false;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var graphData = JsonUtility.FromJson<GraphData>(json);

                if (graphData == null)
                {
                    Debug.LogError($"[GraphSaveLoadManager] Failed to parse JSON: {filePath}");
                    return false;
                }

                // デシリアライズ: 既存グラフをクリアしてノード・エッジを復元
                _graphContext.Context.Deserialize(graphData);

                Debug.Log($"[GraphSaveLoadManager] Loaded: {filePath} ({graphData.nodes.Count} nodes, {graphData.edges.Count} edges)");

                // ビジュアル再構築のためにイベント発火
                OnGraphLoaded?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphSaveLoadManager] Load failed: {filePath} — {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 保存済みファイル名一覧を取得する（拡張子付き）。
        /// </summary>
        /// <returns>ファイル名の配列。ディレクトリが存在しない場合は空配列。</returns>
        public string[] GetSaveFiles()
        {
            EnsureSaveDirectory();

            try
            {
                var files = Directory.GetFiles(_saveDirectoryPath, "*" + FileExtension);
                var fileNames = new string[files.Length];
                for (var i = 0; i < files.Length; i++)
                {
                    fileNames[i] = Path.GetFileName(files[i]);
                }

                // 新しい順にソート（ファイル名にタイムスタンプが含まれる想定）
                Array.Sort(fileNames, StringComparer.OrdinalIgnoreCase);
                Array.Reverse(fileNames);
                return fileNames;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphSaveLoadManager] GetSaveFiles failed — {e.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// 保存済みファイルを削除する。
        /// </summary>
        /// <param name="fileName">ファイル名（拡張子なし可）。</param>
        /// <returns>削除成功でtrue。</returns>
        public bool DeleteSave(string fileName)
        {
            var resolvedName = EnsureExtension(fileName);
            var filePath = GetFilePath(resolvedName);

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[GraphSaveLoadManager] File not found for deletion: {filePath}");
                return false;
            }

            try
            {
                File.Delete(filePath);
                Debug.Log($"[GraphSaveLoadManager] Deleted: {filePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphSaveLoadManager] Delete failed: {filePath} — {e.Message}");
                return false;
            }
        }

        private void EnsureSaveDirectory()
        {
            if (!string.IsNullOrEmpty(_saveDirectoryPath) && !Directory.Exists(_saveDirectoryPath))
            {
                Directory.CreateDirectory(_saveDirectoryPath);
            }
        }

        /// <summary>
        /// ファイル名を解決する。空の場合はタイムスタンプで自動生成。
        /// </summary>
        private string ResolveFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                // タイムスタンプベースの自動ファイル名: live_set_20260403_1430.json
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
                return DefaultPrefix + timestamp + FileExtension;
            }

            return EnsureExtension(fileName!);
        }

        private static string EnsureExtension(string fileName)
        {
            if (!fileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return fileName + FileExtension;
            }
            return fileName;
        }

        private string GetFilePath(string fileName)
        {
            return Path.Combine(_saveDirectoryPath, fileName);
        }
    }
}
