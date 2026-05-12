# REPO_HEALTH

リポジトリ健全性の現状診断と、Launch (2026-05-16) 後に実施するクリーンアップ手順。

このドキュメントは `2026-05-12` 時点の対症療法と、根本対策の段取りを記録する。push 失敗 (HTTP 500) を契機に整備したもので、`Rhizomode.Bootstrap` リファクタ計画 (v4) とは独立して進められる作業。

---

## 1. 現状診断 (2026-05-12)

| 項目 | 値 |
|---|---|
| `.git` ディレクトリ | **4.7 GB** |
| loose objects (未パック) | **3.77 GiB** (7029 個) |
| pack 済み | 870 MiB |
| Library/Temp/obj/Logs/Build/ tracked | 無し ✓ |
| push 試行結果 | HTTP 500 (GitHub サーバー側 disconnect) |

### 肥大化の主因

過去 commit に巨大な Demo / Sample / 外部パッケージのアセットが含まれている:

| ディレクトリ | 推定サイズ | 種別 |
|---|---|---|
| `Assets/TerrainDemoScene_URP/` | 約 2-4 GB | URP Terrain Demo (Texture *.tif 130MB×多数、Terrain Data *.asset 50MB×16) |
| `Assets/AllSkyFree/` | 約 200 MB | Skybox Asset Store サンプル (Equirect PNG 50-80MB×多数) |
| `Assets/Samples/XR Interaction Toolkit/` | 数十 MB | XR Toolkit Sample (Package が持つので不要) |
| `Packages/*/Documentation~/Images/*.mp4` | 60 MB | UPM パッケージドキュメント動画 (異常コミット) |

### push 失敗の機序

過去 commit が大量のオブジェクトを含むため、`git push` 時に GitHub への HTTP POST が膨大になり、サーバー側で disconnect (HTTP 500)。`http.postBuffer = 524288000` (500MB) に拡大することで一時的に通る可能性はあるが、根本解決ではない。

---

## 2. 2026-05-12 の対症療法 (済)

### 2.1 `.gitignore` 強化
`rhizomode/.gitignore` に下記パターンを追加:

```gitignore
# Demo/Sample
/[Aa]ssets/TerrainDemoScene_URP/
/[Aa]ssets/AllSkyFree/
/[Aa]ssets/Samples/
/Packages/*/Documentation~/

# 大型バイナリ (将来 Git LFS 候補)
*.tif *.tiff *.hdr *.exr *.psd *.fbx *.blend
*.mp4 *.mov *.webm *.wav *.aif *.aiff
```

### 2.2 tracking から除外
`git rm --cached -r` で 2586 files を index から削除。working tree のファイルは原則残る (例外: `Packages/com.unity.visualeffectgraph/Samples~/` 配下の FBX/PSD/EXR は引数事故で working tree からも消えたが、UPM パッケージのサンプルテンプレートなので Unity Package Manager から再 import 可能)。

### 2.3 効果と限界
- **新規 commit のサイズ**: 大幅に縮小される (将来追加されるアセットが ignore される)
- **既存 history**: `.git` 内に loose objects として残るため、`.git` サイズも push サイズも即時には縮小しない
- **push 通過性**: 一度通れば次回以降の push は差分のみで軽い (今回 `e078d48a` の push 成功で実証)

---

## 3. 残課題 (Launch 後対応)

### 3.1 Git LFS 移行

大型バイナリ (テクスチャ、メッシュ、オーディオ、シーン) を Git LFS で管理する。

**対象パターン**:
```
*.png *.jpg *.tga *.tif *.tiff *.hdr *.exr *.psd
*.fbx *.obj *.blend
*.wav *.mp3 *.ogg *.aif *.aiff
*.mp4 *.mov *.webm
*.unity *.prefab *.asset (※大型のものに限る、判断要)
```

`*.unity` / `*.prefab` / `*.asset` を LFS に入れるかは要検討。YAML テキストとして人間が diff を見たいケースが多いため、現状の `*.unity merge=unityyamlmerge` 戦略との両立を考える必要がある。

**手順 (概略)**:
```bash
# 1. LFS インストール (各クローン作業者)
git lfs install

# 2. パターン追加
git lfs track "*.png" "*.psd" "*.fbx" "*.wav" "*.mp4"
git add .gitattributes
git commit -m "chore(lfs): track binary patterns"

# 3. 既存 history を rewrite (filter-repo or BFG)
# → 全クローンを再フェッチさせる force-push が必要
```

### 3.2 `git filter-repo` で history rewrite

過去 commit から不要ファイルを完全削除する。**破壊的操作のため Launch 前にはやらない**。

```bash
# git-filter-repo インストール (Python)
pip install git-filter-repo

# fresh clone を別ディレクトリに作る (バックアップとして)
git clone --mirror https://github.com/Halgrm/rhizomode.git rhizomode-backup.git

# rewrite 実行
cd rhizomode-backup.git
git filter-repo --invert-paths \
    --path rhizomode/Assets/TerrainDemoScene_URP \
    --path rhizomode/Assets/AllSkyFree \
    --path rhizomode/Assets/Samples \
    --path-glob 'rhizomode/Packages/*/Documentation~' \
    --path-glob '*.tif' --path-glob '*.tiff' \
    --path-glob '*.hdr' --path-glob '*.exr' --path-glob '*.psd' \
    --path-glob '*.fbx' --path-glob '*.blend' \
    --path-glob '*.mp4' --path-glob '*.mov' --path-glob '*.webm' \
    --path-glob '*.wav' --path-glob '*.aif' --path-glob '*.aiff'

# サイズ確認後、force push
git push --mirror --force https://github.com/Halgrm/rhizomode.git
```

### 3.3 影響範囲

- **既存 clone**: 全 contributor が再 clone する必要 (履歴の SHA が変わるため)
- **既存 PR**: open PR は base commit が消えるため close + 再作成必要
- **既存 tag**: 履歴上の SHA が変わるので再付与必要

### 3.4 Launch 前にやらない理由

1. 演者リハーサル中に repo 構造を壊すリスク
2. force push 中の事故で main branch が一時的に壊れる
3. v4 リファクタ実装と並行すると merge conflict 必至

→ **Launch (2026-05-16) 後、最初の 1 週間で実施するのが推奨**。

---

## 4. 推奨スケジュール

| 時期 | 作業 | 担当 | 完了条件 |
|---|---|---|---|
| 2026-05-12 (済) | `.gitignore` 強化 + `git rm --cached` | Claude | tracked から 2586 files 除外 |
| 2026-05-12 (進行中) | 過去 commits を分割 push | ユーザー手動 | 全 12 commits push 完了 |
| 2026-05-16 | v0.3.0 Launch | ユーザー | launch 成功 |
| 2026-05-18〜20 | Git LFS 導入 + `.gitattributes` 整備 | 別 PR | LFS で新規バイナリが扱える |
| 2026-05-20〜25 | `git filter-repo` で history rewrite | ユーザー | `.git` < 500 MB |
| 2026-05-25 | 全 clone 作業者へ通知 + 再 clone 案内 | ユーザー | 全員が新 history で fetch |
| 2026-06-01〜 | CI で repo サイズ監視 (GitHub Actions) | 別 PR | 毎週 size report が PR コメント |

---

## 5. モニタリング (Launch 後)

### 5.1 定期チェック (週次)
```bash
git count-objects -vH
du -sh .git
```

閾値: `.git > 1 GB` で警告、`> 2 GB` で対処必須。

### 5.2 commit 時の事前チェック (pre-commit hook)
1 file > 10 MB の commit を block する pre-commit hook を `.git/hooks/pre-commit` に置く:

```bash
#!/bin/sh
# 大型ファイル commit ブロック
threshold=$((10 * 1024 * 1024))
for f in $(git diff --cached --name-only --diff-filter=A); do
  size=$(wc -c < "$f" 2>/dev/null || echo 0)
  if [ "$size" -gt "$threshold" ]; then
    echo "ERROR: $f is $((size/1024/1024)) MB (>10 MB). Use Git LFS or .gitignore."
    exit 1
  fi
done
```

これを `.husky/pre-commit` か `Tools/git-hooks/pre-commit` 等に置いて、新規 clone 時に自動セットアップする仕組みを別途用意。

### 5.3 CI での Validation (将来)
GitHub Actions の workflow で:
- PR 時に追加されたファイルサイズの累計をチェック
- LFS 対象パターンが LFS に入っているか確認
- `.git/objects/pack/` のサイズを定期報告

---

## 6. 参考

- [Git LFS 公式ドキュメント](https://git-lfs.com/)
- [git-filter-repo](https://github.com/newren/git-filter-repo) - history rewrite の推奨ツール
- [BFG Repo-Cleaner](https://rtyley.github.io/bfg-repo-cleaner/) - filter-repo の代替 (Java)
- [Unity .gitignore template](https://github.com/github/gitignore/blob/main/Unity.gitignore) - 標準テンプレート

## 7. 関連ドキュメント

- `CLAUDE.md` — プロジェクト全体のコーディング規約 (CODING_GUIDELINES.md と併読)
- `docs/CODING_GUIDELINES.md` — Unity / C# のコーディング標準
- `docs/TECHNICAL_DESIGN.md` — システム設計仕様
- `~/.claude/plans/eager-conjuring-eich.md` — v4 リファクタ計画 (Bootstrap/Catalog/Interaction asmdef 新設、Graph Command 層、HealthMonitor 等)
