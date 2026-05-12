# rhizomode — デザイン依頼用ブリーフ

このドキュメントは「rhizomode の構造を説明する資料（スライド／インフォグラフィック／ポスター／PDF）」を Claude などにデザインさせるためのブリーフです。そのままコピペして「これをデザインして」と頼めます。

---

## 0. 制作物への希望（先に伝える前提）

- **目的**: rhizomode のシステム構造を、自分とコラボ相手が一目で理解できる資料にしたい。
- **トーン**: ライブパフォーマンス／VJ／クラブカルチャー寄り。テック資料すぎず、アート寄りすぎず。暗背景＋ネオン or ミニマル＋アクセントカラーが好み。
- **言語**: 日本語メイン、固有名詞・コードは英語のまま。
- **想定フォーマット**: A4縦のPDF、または横スライド（16:9）、または1枚インフォグラフィック。
- **読み手**: 自分（仕様確認用）／将来のコラボ相手／登壇資料の聴衆。

---

## 1. プロダクト一行要約

> **rhizomode** は、VR空間内でノードグラフをライブビルドし、リアルタイム3D演出を構築・制御するソロパフォーマンスツール。「**構築プロセスそのものが演出**」というコンセプトで、ノードを繋ぐ行為自体を観客に見せる。

- ユーザー: 1人（本人専用）
- デバイス: PCVR固定（Quest Link via SteamVR）
- エンジン: Unity 6 + URP
- 言語: C# 9（`#nullable enable` 全ファイル）
- リアクティブ基盤: R3（NuGetForUnity経由）
- ローンチ目標: 2026-05-16

---

## 2. キーメッセージ（資料の表紙〜冒頭で押し出したい言葉）

- 「ノードを繋ぐ瞬間が、演出に命を吹き込む瞬間」
- 「構築 = 演出（Construction as Performance）」
- 「VR空間で、ゼロからグラフを組み立てるライブ」
- 「ソロ・PCVR・90fps・絶対に止まらない映像」

---

## 3. 6層レイヤー構成（図にしてほしい）

上から下に重ねる積層図。**上ほどユーザー入力に近く、下ほど純粋ロジック**。依存は一方向（上→下）のみ。

1. **VR UI Layer** — Menu / Node Display / Status Panel
2. **XR Layer** — Controller Input / Ray Interaction
3. **Node Graph Layer** — NodeBase / Ports / Edges / GraphContext
4. **Performance Modules** — VFXModule / ShaderModule / …
5. **Audio Layer** — AudioAnalyzer / Device Selection
6. **Core Layer** — Type System / Serialization / Signal Flow

> デザイン指示: 各レイヤーを横長の帯で重ねる。下層ほど色を落ち着かせる。下向き矢印で依存方向を示す。

---

## 4. 7アセンブリ（依存グラフにしてほしい）

asmdef レベルで依存方向を強制し、循環参照を構造的に不可能にしている。

| アセンブリ | 役割 | 依存先 |
|---|---|---|
| Rhizomode.Core | 型システム（ParamType）、ポート、GraphContext、NodeBase、Edge、シリアライズ | （なし） |
| Rhizomode.Nodes | ノード実装（Input / Math / Time / Utility / Modules） | Core |
| Rhizomode.Modules | IPerformanceModule 実装（VFXModule, ShaderModule） | Core |
| Rhizomode.UI | VR内UI（メニュー、ノード表示、エッジ、ステータス） | Core, Nodes |
| Rhizomode.XR | コントローラー入力、レイ操作、各種ハンドラー | Core, UI |
| Rhizomode.Audio | AudioAnalyzer、デバイス管理 | Core |
| Rhizomode.ExternalInput | OSC（OscJack）/ MIDI（Minis） | Core |

依存方向の俯瞰図:

```
XR ──▶ UI ──▶ Nodes ──▶ Core
              Modules ──▶ Core
              Audio ──▶ Core
              ExternalInput ──▶ Core
```

> デザイン指示: Core を中央下に置いて、矢印が全部 Core に流れ込む放射状図がわかりやすい。

---

## 5. キーとなる設計パターン（6つ、カード形式で）

1. **Reactive Push モデル**
   ノードは `Setup(GraphContext)` 内で R3 Observable チェーンを構築。値が変わったときだけ下流が発火。Update ループは Time / LFO / Noise だけが持つ。

2. **インターフェース境界**
   モジュール通信は `IPerformanceModule`、ポートは `IOutputPort` / `IInputPort`。asmdef を越えて具象型に依存しない。

3. **object 経由の型柔軟性**
   ポート内部値は `object`。型チェックは接続時に `ParamType` enum（Float / Color / Bool）で行う。後から Vector3 等を増やしてもポートインターフェースは変えない。

4. **Defensive Runtime**
   外部呼び出しは全て try-catch、NaN / null はデフォルト値にフォールバック。**映像は絶対に止めない**がパフォーマンス上の鉄則。

5. **ModuleDefinition（ScriptableObject）**
   演出モジュールのパラメータ定義。モジュールノードを生成すると ConstFloat / ConstColor が自動スポーンされ、全パラメータに事前接続される。= ライブ中にゼロ接続のモジュールが存在しない。

6. **モジュールライフサイクル分離**
   Factory はノードオブジェクトを作るだけ。プレハブ生成と IPerformanceModule の注入は別タイミング（メニュー作成時 = `InjectModuleIfNeeded` / グラフロード時 = `ReinjectModulesAfterLoad`）。

> デザイン指示: 6枚のカードを2×3または3×2グリッドで。各カードに短いタイトル＋1〜2行説明。アイコンを添えると映える。

---

## 6. VR UI パイプライン（フロー図にしてほしい）

VRコントローラーのレイがUIToolkitのWorldSpaceパネルを操作する流れ。Unity 6 のUIToolkitはWorldSpace未対応のため、RenderTexture + reflection でイベント注入している。

```
ControllerInputRouter (IRayProvider + IControllerInput)
       │ RayOrigin / RayDirection
       ▼
SharedRaycastService (毎フレーム Physics.Raycast、結果を共有)
       │ RaycastHit
       ▼
WorldPanelRayBridge (reflection で UIToolkit イベント注入)
       │ PointerDown / PointerUp / Hover
       ▼
UIToolkit Panel (WorldPanelHost 上の RenderTexture)
```

注意点:
- ノードは MeshCollider 付き Quad。前面のみレイキャスト可（プレイヤー方向を向いて生成）。
- PanelSettings はテーマ付きテンプレートからクローン（Unity 6 要件）。
- メニュー非表示は `SetActive(false)` ではなく `MeshRenderer/MeshCollider.enabled` トグル（UIDocument破壊防止）。

> デザイン指示: 縦4段のフロー図。各段に簡潔なラベルと「何を渡すか」を矢印に書く。

---

## 7. 型システム（小さい表で）

| 型 | 用途 | 備考 |
|---|---|---|
| Float | 連続値。パラメータ制御全般 | ConstFloat は 0〜1 固定、Remap で変換 |
| Color | 色 | HSVホイール入力 |
| Bool | トリガー / ゲート | VFX SendEvent、Activate / Deactivate、条件分岐 |
| Vector3 | 後日追加予定 | object 経由なのでI/F変更不要 |

---

## 8. 実装状況（タイムライン or プログレスバーで）

| フェーズ | 内容 | 状態 |
|---|---|---|
| Week 1 | Core / asmdef / R3 / シリアライズ | 完了 |
| Week 2 | XR入力 / 巻物メニュー / WorldSpace ノード | 完了 |
| Week 3 | エッジ接続・切断 / ノード削除・グラブ | 完了 |
| Week 4 | 全24ノード実装 / Audio / OSC・MIDI | 完了 |
| Week 5 | セーブロード / ミラー出力 / Spout・NDI | 完了 |
| Week 6 | バグ修正 / モジュール自動スポーン | 完了 |
| Week 6.5〜 | 演出VFX/Shader制作 / 通しリハ / v0.3.0タグ | 進行中 |

実装済24ノード（参考、小さく載せる）:
ConstFloat, ConstColor, AudioTrigger, BeatDetector, TapTempo, OscReceiver, MidiCC, Multiply, Add, Remap, Smooth, Time, Timer, Delay, LFO, Noise, Threshold, Toggle, ColorToFloats, FloatsToColor, ColorToHSV, HSVToColor, SceneObject, VFXModuleNode, ShaderModuleNode, FloatMonitor, BoolMonitor, ColorMonitor。

> デザイン指示: 横並びのタイムラインバー。Week 6.5 だけ「進行中」マーカー。ローンチ 2026-05-16 を最右に大きく。

---

## 9. パフォーマンス予算（数字を強調）

- ノード上限 **約60個**（モジュール5〜10 + Math/Control 20〜50）
- VR **90fps 必達** / ミラー出力 **60fps**
- VRレンダリング: **Single Pass Instanced**（URP）
- ShaderModule は **MaterialPropertyBlock** 使用（マテリアル複製なし）、LateUpdate でバッチ化

> デザイン指示: 90fps / 60fps / 60nodes を大きい数字で並べるブロック。

---

## 10. 破壊的変更ルール（赤字 or 警告ボックスで）

クリティカルバグ修正以外で禁止:
- インターフェースシグネチャの追加・削除・変更
- public メソッドのシグネチャ変更
- シリアライズJSONフィールド名 / ノードポート名のリネーム・削除
- asmdef 依存方向の変更

拡張は「新インターフェース追加 + `is` キャスト」、またはシリアライズフィールドの「末尾追加 + 安全なデフォルト」で対応する。

---

## 11. デザイン上の指示まとめ

- **配色案**: ベース `#0e0f14`（暗）/ アクセント `#7af7c8`（ネオングリーン）or `#ff5dab`（マゼンタ）/ 文字 `#e8e9ef`。Core は寒色、UI/XR は暖色。
- **タイポ**: 日本語は Noto Sans JP / Inter（英）。コードは JetBrains Mono / Source Code Pro。
- **アイコン**: 線画ベース、塗りつぶしすぎない。ノード＝丸、エッジ＝細線、モジュール＝六角形 のメタファーを使うと統一感が出る。
- **レイアウト優先順位**: ① 表紙キーメッセージ → ② レイヤー図 → ③ asmdef依存図 → ④ 設計パターン6枚 → ⑤ VR UIパイプライン → ⑥ 進捗 / 数字。型表とノード一覧は脇役で良い。
- **避けたいもの**: 業務スライドっぽい青基調、クリップアート、過剰なグラデーション、3D影付きアイコン。

---

## 12. Claudeへの依頼テンプレ（コピペ用）

> このブリーフを元に、rhizomode のシステム構造を説明する **A4縦1ページのインフォグラフィック** を作って。HTML+CSS（印刷時にA4にきれいに収まること）で、暗背景＋ネオン緑アクセント、Noto Sans JP / JetBrains Mono を使用。レイヤー積層図、asmdef依存放射状図、設計パターン6カード、VR UIパイプラインのフロー図を含めて。表紙のキーメッセージは「構築プロセスそのものが演出」。

> または: このブリーフを元に、**16:9のスライド10枚**（表紙 / 概要 / レイヤー / asmdef / 設計パターン×2 / VR UI / 型 / 進捗 / クロージング）を Reveal.js で。

---

（このブリーフ自体は `docs/rhizomode_structure_brief.md` に保存されています。元データは `docs/TECHNICAL_DESIGN.md` と `CLAUDE.md`。）
