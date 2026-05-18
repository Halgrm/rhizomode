#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Rhizomode.Graph.Mutation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rhizomode.UI
{
    /// <summary>
    /// 「シーン (Cue)」の保存 / 一覧 / 呼び出し / 削除 + Undo/Redo の facade。
    /// GraphSaveLoadManager / GraphCommandDispatcher / MirrorOutputController を組み合わせ、
    /// graph 再構築時に出力 RT を一時 freeze する。
    /// </summary>
    /// <remarks>
    /// Cue 一覧の実体は <c>Assets/Data/SavedGraphs/</c> の JSON ファイル群。本サービスは
    /// .json 拡張子を剥がして「cue name」として扱う。Freeze は graph 再構築中の glitch を
    /// 観客側 (Spout/NDI/DesktopBlitter) に見せないため。Unfreeze の遅延発火は呼び出し側
    /// (Panel Controller) の責務 (coroutine が必要なため)。
    ///
    /// Undo/Redo は <see cref="GraphCommandDispatcher.TryUndo"/>/<see cref="GraphCommandDispatcher.TryRedo"/>
    /// が GraphState を Snapshot から復元するが、visual / module の再構築は
    /// <see cref="GraphSaveLoadManager.OnGraphLoading"/>/<see cref="GraphSaveLoadManager.OnGraphLoaded"/>
    /// subscriber (GraphSaveLoadBootstrapWiring) に委譲する。
    /// </remarks>
    public sealed class CueLibraryService
    {
        private const string JsonExtension = ".json";
        private const string ThumbnailExtension = ".png";
        private const string DefaultNamePrefix = "cue_";
        private const int ThumbnailWidth = 256;
        private const int ThumbnailHeight = 144;

        private readonly GraphSaveLoadManager _saveLoad;
        private readonly GraphCommandDispatcher _dispatcher;
        private readonly MirrorOutputController? _mirrorOutput;

        public CueLibraryService(
            GraphSaveLoadManager saveLoad,
            GraphCommandDispatcher dispatcher,
            MirrorOutputController? mirrorOutput)
        {
            _saveLoad = saveLoad ?? throw new ArgumentNullException(nameof(saveLoad));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _mirrorOutput = mirrorOutput;
        }

        public bool CanUndo => _dispatcher.UndoStackCount > 0;
        public bool CanRedo => _dispatcher.RedoStackCount > 0;

        /// <summary>保存済み cue 名一覧 (拡張子なし、ファイル名順)。</summary>
        public IReadOnlyList<string> ListCues()
        {
            var files = _saveLoad.GetSaveFiles();
            var result = new List<string>(files.Length);
            foreach (var f in files)
            {
                if (string.IsNullOrEmpty(f)) continue;
                result.Add(StripExtension(f));
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        /// <summary>現在の graph を指定名で保存する。同名 cue は上書き。MirrorOutput RT をサムネとして同時保存。</summary>
        public bool SaveAs(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var ok = _saveLoad.SaveGraph(name);
            if (ok) TrySaveThumbnail(name);
            return ok;
        }

        /// <summary>新規 cue 用に未使用のデフォルト名を返す (cue_01, cue_02, ...)。</summary>
        public string SuggestNewName()
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in ListCues()) existing.Add(c);

            for (int i = 1; i < 1000; i++)
            {
                var candidate = $"{DefaultNamePrefix}{i:D2}";
                if (!existing.Contains(candidate)) return candidate;
            }
            return DefaultNamePrefix + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        /// <summary>出力 RT を一時 freeze する (cue 切替前に呼ぶ)。</summary>
        public void FreezeOutput() => _mirrorOutput?.Freeze();

        /// <summary>出力 RT の freeze を解除する (cue 切替後 数フレーム置いて呼ぶ)。</summary>
        public void UnfreezeOutput() => _mirrorOutput?.Unfreeze();

        /// <summary>指定 cue をロードする。同期完了。失敗時は false (graph は元状態にロールバック済)。</summary>
        public bool LoadCue(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _saveLoad.LoadGraph(name);
        }

        /// <summary>cue を削除する (サムネも同期削除)。</summary>
        public bool Delete(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            TryDeleteThumbnail(name);
            return _saveLoad.DeleteSave(name);
        }

        /// <summary>
        /// 指定 index (0-origin) の cue を recall する (1-9 quick-slot 用)。
        /// 該当 index が存在しなければ false。
        /// </summary>
        public bool LoadCueAt(int index)
        {
            if (index < 0) return false;
            var cues = ListCues();
            if (index >= cues.Count) return false;
            return LoadCue(cues[index]);
        }

        /// <summary>cue のサムネ画像を Texture2D で取得。存在しなければ null。呼び出し側で <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> 必須。</summary>
        public Texture2D? LoadThumbnail(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var path = ResolveThumbnailPath(name);
            if (!File.Exists(path)) return null;

            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (tex.LoadImage(bytes))
                {
                    tex.name = $"CueThumb_{name}";
                    return tex;
                }
                UnityEngine.Object.Destroy(tex);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CueLibrary] LoadThumbnail failed: {name} — {e.Message}");
            }
            return null;
        }

        private string ResolveThumbnailPath(string name) =>
            Path.Combine(_saveLoad.SaveDirectoryPath, name + ThumbnailExtension);

        /// <summary>
        /// F1 fix (Codex C-B FAIL): MirrorOutput RT を AsyncGPUReadback で非同期に CPU 取得し、
        /// 数フレーム後の callback で PNG エンコード + ファイル書き込みを行う。
        /// </summary>
        /// <remarks>
        /// 旧実装は <c>ReadPixels + EncodeToPNG + WriteAllBytes</c> を同期で実行しており、Save current
        /// 操作 1 回あたり VR 90fps フレームに数 ms 単位の hitch を発生させていた。AsyncGPUReadback は
        /// GPU→CPU 転送を fence で待たずに非同期にし、callback (main thread) 内でのみ Texture2D 経由
        /// PNG エンコードを行う。callback は Unity 既定で main thread 呼び出しのため Texture2D 操作は
        /// 安全。tmp RenderTexture は callback 完了時に解放する (Request 発行直後に解放すると
        /// readback が壊れる)。
        ///
        /// callback 内の <see cref="File.WriteAllBytes"/> は同期だが、サムネ size (256x144) なら
        /// 圧縮後 ~10-30KB で main thread block は無視できる範囲。完全非同期化は F8 (atomic write) で
        /// tmp + move 化する際に Task.Run でラップする予定。
        /// </remarks>
        private void TrySaveThumbnail(string name)
        {
            if (_mirrorOutput == null) return;
            var src = _mirrorOutput.OutputTexture;
            if (src == null) return;

            RenderTexture? tmp = null;
            try
            {
                tmp = RenderTexture.GetTemporary(ThumbnailWidth, ThumbnailHeight, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, tmp);

                var captured = tmp;
                var targetPath = ResolveThumbnailPath(name);
                AsyncGPUReadback.Request(captured, 0, TextureFormat.RGBA32, request =>
                    HandleThumbnailReadback(request, captured, targetPath, name));
                tmp = null; // ownership は callback (HandleThumbnailReadback) に移譲
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CueLibrary] SaveThumbnail request failed: {name} — {e.Message}");
            }
            finally
            {
                // 例外で AsyncGPUReadback.Request に到達できなかった場合のみ解放。
                if (tmp != null) RenderTexture.ReleaseTemporary(tmp);
            }
        }

        private static void HandleThumbnailReadback(
            AsyncGPUReadbackRequest request,
            RenderTexture tmp,
            string targetPath,
            string name)
        {
            Texture2D? tex = null;
            try
            {
                if (request.hasError)
                {
                    Debug.LogWarning($"[CueLibrary] SaveThumbnail readback error: {name}");
                    return;
                }

                var data = request.GetData<byte>();
                tex = new Texture2D(ThumbnailWidth, ThumbnailHeight, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(data);
                tex.Apply(false, false);

                var bytes = tex.EncodeToPNG();
                File.WriteAllBytes(targetPath, bytes);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CueLibrary] SaveThumbnail callback failed: {name} — {e.Message}");
            }
            finally
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
                RenderTexture.ReleaseTemporary(tmp);
            }
        }

        private void TryDeleteThumbnail(string name)
        {
            try
            {
                var path = ResolveThumbnailPath(name);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CueLibrary] DeleteThumbnail failed: {name} — {e.Message}");
            }
        }

        /// <summary>1 ステップ undo。GraphState を pre-snapshot に復元し、visual / module 再構築を駆動する。</summary>
        /// <remarks>
        /// <see cref="GraphCommandDispatcher.TryUndo"/> 自体は GraphState を巻き戻すだけで EventBus emit を行わない
        /// (RestoreFromSnapshot 仕様)。そこで Save/Load と同じ <see cref="GraphSaveLoadManager.OnGraphLoading"/> /
        /// <see cref="GraphSaveLoadManager.OnGraphLoaded"/> を raise し、旧 module CleanupAll + visual rebuild を
        /// 再利用する。Freeze は dispatcher 側で snapshot 数が無ければ no-op になる前に判定する。
        /// </remarks>
        public bool TryUndo()
        {
            if (!CanUndo) return false;
            return RunWithRebuild(() => _dispatcher.TryUndo());
        }

        /// <summary>1 ステップ redo。</summary>
        public bool TryRedo()
        {
            if (!CanRedo) return false;
            return RunWithRebuild(() => _dispatcher.TryRedo());
        }

        private bool RunWithRebuild(Func<bool> mutate)
        {
            FreezeOutput();
            try
            {
                _saveLoad.RaiseGraphLoading();
                var ok = mutate();
                // F3 fix (Codex review B): mutate() 失敗時も RaiseGraphLoaded を必ず raise する。
                // 旧版は mutate=false 時に RaiseGraphLoaded を skip していたが、その時点で
                // CleanupAll (OnGraphLoading subscriber) は完了済 → 旧 module 破棄 + graph 未変化で
                // visual + module の orphan 状態に陥っていた。失敗時も visual rebuild を発火させ、
                // graph state 全 node を Object3DProxyBindService + GraphLoadCoordinator で再描画する。
                _saveLoad.RaiseGraphLoaded();
                return ok;
            }
            finally
            {
                UnfreezeOutput();
            }
        }

        private static string StripExtension(string fileName)
        {
            if (fileName.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
                return fileName.Substring(0, fileName.Length - JsonExtension.Length);
            return fileName;
        }
    }
}
