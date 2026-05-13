# Codex Deferred Findings

Codex review loop で検出された軽微 (理論リスク / production-scale でのみ問題化 / overengineering 寄り) な指摘を集約。Phase 13 (final cleanup) または production scale issue が顕在化したタイミングで再評価する。

各エントリの形式:
- **検出 Phase / Loop**: いつ検出されたか
- **対象**: file:line
- **指摘内容**: Codex の判定
- **実害評価**: なぜ軽微と判断したか
- **将来 trigger**: いつ再評価すべきか

---

## Phase 7 (Persistence + Hydrator)

### F-7.1: SaveGraph tmp pattern over-match risk
- **検出**: Phase 7 Codex Loop 5 (`a42675e573993b329`)
- **対象**: `JsonGraphRepository.cs:39` SweepOrphanTmpFiles
- **指摘内容**: `*.tmp-*` glob がファイル名に `.tmp-` を含む正規セーブファイルを誤検出する理論上の可能性
- **実害評価**: 軽微。SaveGraph は `<file>.json.tmp-{guid}` のみ生成、ユーザーが `live_set_x.tmp-foo.json` のようなファイル名を意図的に作らない限り誤検出ゼロ。最終的に Phase 7 Loop 5 で `*.json.tmp-*` に precise pattern 化済 (commit `<NEXT>`)。
- **将来 trigger**: 既に Loop 5 で resolve 済 → このエントリは記録のみ。

### F-7.2: SweepOrphanTmpFiles 同期 + 件数上限なし
- **検出**: Phase 7 Codex Loop 5
- **対象**: `JsonGraphRepository.cs:39-40` SweepOrphanTmpFiles
- **指摘内容**: tmp ファイルが大量にある場合、ctor で同期 sweep がメインスレッドをブロックする。タイムアウト・件数上限なし。
- **実害評価**: 低。ローカル開発フェーズではセーブ数は数十件、tmp 残骸も数件レベル。ms オーダーで完了する。
- **将来 trigger**: 
  - Cloud sync / 共有ストレージで SaveDirectoryPath が複数プロセス共有になった場合
  - ユーザーが 1000+ セーブを保有するヘビーユースケース
  - 起動時間プロファイルで sweep が顕著に出始めたら

### F-7.3: ctor sweep と SaveGraph の race (silent data loss)
- **検出**: Phase 7 Codex Loop 5
- **対象**: `JsonGraphRepository.cs:39, 42, 63`
- **指摘内容**: ctor sweep が SaveGraph 進行中の tmp を削除し、catch スワローで silent data loss が発生する可能性
- **実害評価**: 極小。Repository ctor は MonoBehaviour.Awake で 1 度のみ呼ばれ、SaveGraph はユーザー UI 操作 (Start 後) でしか起動しない → 並列実行経路が存在しない。
- **将来 trigger**:
  - Phase 10 (Audio 細分化) で background thread SaveGraph が導入された場合
  - 複数 Repository instance が同一 SaveDirectoryPath を共有する設計に変わった場合 (Phase 8 Installer で 1 instance に固定する想定だが、要確認)

---

## 運用

- 新規エントリは `### F-<phase>.<番号>: <タイトル>` 形式で追加
- Phase 13 (final cleanup) で本ファイルを総点検し、各エントリの最終判断 (修正 / 永続 deferral / 削除) を決める
- "将来 trigger" が現実化したら速やかに当該エントリを修正対応に格上げする
