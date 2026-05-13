#nullable enable

using System;
using System.IO;
using Rhizomode.Graph.Serialization;
using Rhizomode.Persistence.Contracts;
using UnityEngine;

namespace Rhizomode.Persistence.Json
{
    /// <summary>
    /// <see cref="GraphData"/> を JSON ファイルとして永続化する <see cref="IGraphRepository"/>。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 7: <c>GraphSaveLoadManager</c> から GraphData の Serialize/Hydrate 責務を
    /// 引き剥がし、本クラスは「I/O のみ」に限定する。<see cref="ISavePathProvider"/> 経由で
    /// 保存先ディレクトリを取得 (テスト時の差し替えを容易にする)。
    /// </remarks>
    public sealed class JsonGraphRepository : IGraphRepository
    {
        private const string FileExtension = ".json";

        private readonly ISavePathProvider _pathProvider;

        public JsonGraphRepository(ISavePathProvider pathProvider)
        {
            _pathProvider = pathProvider;
        }

        public bool SaveGraph(string fileName, GraphData data)
        {
            var path = GetFilePath(fileName);
            // Codex review fix #7: atomic write — tmp に書き終えてから rename で原子的に置換。
            // WriteAllText 途中 (例: 電源断 / プロセスクラッシュ) で既存セーブが破損するのを防ぐ。
            var tmpPath = path + ".tmp";
            try
            {
                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(tmpPath, json);

                // .NET Standard 2.1 では File.Move(overwrite:) overload なし。
                // Replace は別 volume だと失敗するため、existing を消してから rename する 2 段階。
                // 失敗ウィンドウ (delete 成功 + move 失敗) では tmp が残るが、次回 SaveGraph で上書きされる。
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmpPath, path);
                Debug.Log($"[JsonGraphRepository] Saved: {path}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonGraphRepository] Save failed: {path} — {e.Message}");
                // 失敗時の tmp 残骸を best-effort で削除 (失敗してもセーブ自体は既に失敗してるので無視)。
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { /* ignore */ }
                return false;
            }
        }

        public GraphData? LoadGraph(string fileName)
        {
            var path = GetFilePath(fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[JsonGraphRepository] File not found: {path}");
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<GraphData>(json);
                if (data == null)
                {
                    Debug.LogError($"[JsonGraphRepository] JSON parse returned null: {path}");
                    return null;
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonGraphRepository] Load failed: {path} — {e.Message}");
                return null;
            }
        }

        public string[] GetSaveFiles()
        {
            try
            {
                if (!Directory.Exists(_pathProvider.SaveDirectoryPath))
                    return Array.Empty<string>();

                var files = Directory.GetFiles(_pathProvider.SaveDirectoryPath, "*" + FileExtension);
                var names = new string[files.Length];
                for (var i = 0; i < files.Length; i++)
                    names[i] = Path.GetFileName(files[i]);

                Array.Sort(names, StringComparer.OrdinalIgnoreCase);
                Array.Reverse(names);
                return names;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonGraphRepository] GetSaveFiles failed — {e.Message}");
                return Array.Empty<string>();
            }
        }

        public bool DeleteSave(string fileName)
        {
            var path = GetFilePath(fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[JsonGraphRepository] File not found for deletion: {path}");
                return false;
            }

            try
            {
                File.Delete(path);
                Debug.Log($"[JsonGraphRepository] Deleted: {path}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonGraphRepository] Delete failed: {path} — {e.Message}");
                return false;
            }
        }

        private string GetFilePath(string fileName)
        {
            var resolved = fileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)
                ? fileName : fileName + FileExtension;
            return Path.Combine(_pathProvider.SaveDirectoryPath, resolved);
        }
    }
}
