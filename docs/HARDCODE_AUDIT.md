# Hardcode Audit

数値・文字列定数の分類監査ログ。Phase 13 (final cleanup) で初回 sweep を実施。

## 分類ポリシー (3 分類)

1. **SO 化対象** — デザイナー/パフォーマーが触る値、ライブ前リハで調整したい値、環境差分
   (PCVR vs デスクトップ)、演目ごとの差分。`Assets/Data/Config/` 配下の SO に置く。
2. **const のまま** — 数学定数、アルゴリズム不変量、シリアライズ契約、UI レイアウト計算式。
   名前付き `const` / `static readonly` で宣言すれば「分類済」とみなす。
3. **理由付き例外** — SO でも const でもない場合、行末に `// HARDCODE-OK: <理由>` コメント必須。

「全ハードコード値を一掃して全部 SO 化」はやりすぎ (可読性が下がる)。用途で線引きする。

## Phase 13 初回 sweep の所見

`Assets/Runtime/` 全体を Explore agent で監査。**コードベースは成熟しており、大半の定数は
既に `const` / `static readonly` / `[SerializeField]` で適切に分離済**。残存する真の inline
magic number は ~24 個。launch (2026-05-16) 直前のため、**低リスクな const 昇格のみ実施**し、
SO 候補は post-launch に記録。

### この pass で修正済 (const 昇格)

| file | 旧 inline 値 | 新 const | 意味 |
|---|---|---|---|
| `Input/Desktop/DesktopInputRouter.cs` | `0.1f` ×2 | `MouseLookDegreesPerPixel` | マウス delta(px) → 回転角(度) スケール |
| `Input/Desktop/DesktopInputRouter.cs` | `0.001f` | `ScrollMovePerUnit` | スクロール値 → 前後移動距離(m) スケール |
| `Interaction/Object3DGrabHandler.cs` | `0.1f` | `StickDeadzone` | 右スティック Y のデッドゾーン |
| `Ableton/GraphAdapter/ClipObject.cs` | `0.4f` | `SteadyEmissionStrength` | 再生中の定常 emission 強度 (pulse 下限) |
| `Ableton/GraphAdapter/ClipObject.cs` | `1.6f` | `PulseMaxEmissionStrength` | pulse フレームの emission 上限 |

### post-launch 対応候補 (今回は据え置き)

launch 直前の大規模変更リスクを避けるため、以下は分類のみ記録し修正は post-launch:

**SO 化候補 (ライブ調整・テーマ性のある値):**
- UI テーマカラー群 — `EdgeDragHandler.Preview.cs:73-76` (パラメータ型別プレビュー線色)、
  `NodeVisualController.Painter.cs:29,40,48,81,96` (波形/スペクトラム描画色)、
  `ScrollMenuVisualController.Bars.cs` / `.cs:51,56` (カテゴリ色)、`AbletonSetupPanel.cs:80-81`
  (ステータス色判定閾値)。→ 色定義を `NodeCategoryColors` のような統一スタイル SO に集約するのが
  本筋。inline `new Color(...)` を `static readonly Color` 名前付きに昇格するだけでも可読性は上がる。
- Ableton UI レイアウト定数 — `GameBootstrap.Ableton.cs:110,299,304,324,336` (パネル spawn 距離
  0.8f、パネル幅最小値 0.4f、ギャップ係数 0.6f 等)。→ `AbletonUiConfig` SO 候補。

**const 昇格候補 (アルゴリズム/レイアウト不変量、未着手):**
- `ScrollMenuVisualController.Bars.cs` のラベルスケール
  (`0.001f` uniform scale、`0.05f`/`0.002f` 位置オフセット)、`NodePanelLOD.cs` の LOD 距離閾値群。

**自明値 (対応不要):**
- `(min + max) * 0.5f` 形の中点計算、Quad UV の `±0.5f`、`1f` 単位元等は文脈で自明なため
  named const にしない (memory `feedback_so_vs_const` の「やりすぎ回避」)。

**判断保留:**
- `GameBootstrap.Ableton.cs:375` の Render Queue 値 `2990` — レンダーパイプライン依存。
  URP の透明描画順序に関わるため、変更時は描画順序の理解が必要。`// HARDCODE-OK` + 詳細コメント
  または ShaderConfig SO 化を post-launch で検討。

## 運用

- 新しい数値リテラル/文字列を書くたびに 3 分類で判定する (ongoing discipline)。
- inline magic number を見つけたら本ファイルに追記し、const 昇格 / SO 化 / `// HARDCODE-OK` の
  いずれかで処理する。
- post-launch の最初の cleanup phase で「post-launch 対応候補」セクションを再評価する。
