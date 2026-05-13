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
            // Codex loop 4 fix: 前回起動中のクラッシュ等で残った unique tmp ファイル
            // (例: foo.json.tmp-{guid}) を起動時に sweep する。
            // 失敗してもアプリ起動は続行 (defensive runtime 原則)。
            SweepOrphanTmpFiles();
        }

        private void SweepOrphanTmpFiles()
        {
            try
            {
                if (!Directory.Exists(_pathProvider.SaveDirectoryPath)) return;
                var orphans = Directory.GetFiles(_pathProvider.SaveDirectoryPath, "*.tmp-*");
                foreach (var f in orphans)
                {
                    try { File.Delete(f); }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[JsonGraphRepository] Failed to sweep tmp file: {f} — {e.Message}");
                    }
                }
                if (orphans.Length > 0)
                    Debug.Log($"[JsonGraphRepository] Swept {orphans.Length} orphan tmp file(s).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonGraphRepository] Tmp sweep failed — {e.Message}");
            }
        }

        public bool SaveGraph(string fileName, GraphData data)
        {
            var path = GetFilePath(fileName);
            // Codex review fix #7: atomic write — tmp に書き終えてから rename で原子的に置換。
            // WriteAllText 途中 (例: 電源断 / プロセスクラッシュ) で既存セーブが破損するのを防ぐ。
            // Codex re-review fix #5: 並列 SaveGraph で tmp 奪い合いを避けるため Guid 付与で unique 化。
            var tmpPath = path + ".tmp-" + System.Guid.NewGuid().ToString("N");
            try
            {
                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(tmpPath, json);

                // Codex re-review #7: File.Delete → File.Move の 2 段階だと delete 成功 + move 失敗で
                // 既存セーブが失われる failure window があった。File.Replace は同一 volume で atomic swap、
                // backup ファイルも自動生成されるため復旧可能。
                // 初回 SaveGraph (path 未存在) は Replace が IOException になるので Move に分岐。
                if (File.Exists(path))
                {
                    File.Replace(tmpPath, path, path + ".bak");
                }
                else
                {
                    File.Move(tmpPath, path);
                }
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
                // Codex re-review fix #6: 対応する .bak ファイルも best-effort で削除 (storage 上の orphan
                // 累積防止)。.bak は SaveGraph の File.Replace 第3引数で自動生成される backup ファイル。
                var bakPath = path + ".bak";
                try { if (File.Exists(bakPath)) File.Delete(bakPath); } catch { /* ignore */ }

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
