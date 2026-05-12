# プロジェクト監査レポート — 2026-04-04

## エグゼクティブサマリー

4チーム（シニアエンジニア・バグチェッカー・デザイナー・リサーチャー）による全コードベースの包括的監査を実施。

**現状**: Week 3（ノード操作）まで完了、Week 4（ノード実装 + Audio）準備段階。  
**スケジュール**: 2026-05-16ライブ目標（あと43日）  
**達成可能性**: **75-80%**  
**主要リスク**: VR統合テスト未実施、Week 4-5の実装密度が高い

---

## 1. バグ監査結果

### 1.1 修正済み（今回のスプリントで対応完了）

| # | ファイル | 修正内容 | 重要度 |
|---|---|---|---|
| 1 | `GraphContext.cs` | `Deserialize`/`MergePreset` で `paramsJson` 復元 + version チェック | Critical |
| 2 | `NodeBase.cs` | `RestoreParamsFromJson()` 仮想メソッド追加 | Critical |
| 3 | `ConstFloatNode.cs` | RestoreParamsFromJson オーバーライド | Critical |
| 4 | `ConstColorNode.cs` | RestoreParamsFromJson オーバーライド | Critical |
| 5 | `LfoNode.cs` | RestoreParamsFromJson オーバーライド | Critical |
| 6 | `ControllerInputRouter.cs` | SerializeField上書き修正 + クローン再生成 + デバッグログ削除 | Critical |
| 7 | `GameBootstrap.cs` | `RegisterCinemachineModules()` 呼出し + SceneObject ファクトリ登録 | Critical |
| 8 | `CinemachineModule.cs` | struct `Lens` のコピー→修正→書き戻し | Critical |
| 9 | `WorldPanelHost.cs` | `_pendingStyleSheet` で遅延適用 | Critical |
| 10 | `BeatDetectorNode.cs` | `_beatEmitted` フラグで次フレーム false 発行 | High |
| 11 | `EdgeDragHandler.cs` | 入力ポート選択に距離閾値追加 | High |
| 12 | `AudioAnalyzer.cs` | `_pendingDevice` でマイクバッファ待ち | High |
| 13 | `VFXModule.cs`/`ShaderModule.cs` | `static readonly EmptyParams` で毎回 new List 回避 | High |
| 14 | `SharedRaycastService.cs` | `LayerMask` 追加 | High |
| 15 | `SpoutSenderController.cs`/`NdiSenderController.cs` | 未使用 `_sourceTexture` 削除 | Low |
| 16 | `EdgeGlow.shader` | SPI用 `UNITY_TRANSFER_INSTANCE_ID` 追加 | Low |
| 17 | `Edge.cs` | `Subscription` を `internal set` に変更 | Low |

### 1.2 未修正の残存バグ（要対応）

#### Critical

| # | ファイル | 問題 | 修正方針 |
|---|---|---|---|
| C1 | `SceneObjectNode.cs` | Position/Rotation の `var pos`/`var rot` がクロージャで共有。3つのSubscriptionが同じVector3を参照し競合 | `_posX`/`_posY`/`_posZ` フィールドに分離 |
| C2 | `TapTempoNode.cs` | BeatDetectorNode と同じ Beat=true スティック問題 | `_beatEmitted` フラグ追加 |
| C3 | `ModuleNodeBase.cs` | `ToNodeData()` で moduleName をシリアライズするが `RestoreParamsFromJson` がない | オーバーライド追加 |

#### High

| # | ファイル | 問題 | 修正方針 |
|---|---|---|---|
| H1 | `LfoNode.cs` | 負の周波数で phase が不正にラップ | `((_phase + delta) % 1f + 1f) % 1f` |
| H2 | `GraphContext.cs` | `Disconnect()` の `List.Find()` が O(n) | Edge辞書索引の追加を検討 |
| H3 | `ModuleDefinition.cs` | `GetParam()` の `List.Find()` がホットパスで O(n) | `Dictionary<string, ParamDefinition>` キャッシュ |
| H4 | `ScrollMenuVisualController.cs` | アーク半径等の定数がハードコード | SerializeField化 |
| H5 | `EdgeDragHandler.cs` | プレビューラインの色が `EdgeVisualManager` と重複定義 | 共有ユーティリティに統合 |

#### Medium

| # | ファイル | 問題 |
|---|---|---|
| M1 | `NodeVisualController.cs` | Monitor更新で毎フレーム string 比較（GC圧力） |
| M2 | `WorldPanelHost.cs` | `RenderTexture.Release()` が LOD 一斉変更時にフレーム落ち |
| M3 | `GraphSaveLoadManager.cs` | `JsonUtility.FromJson` の null 参照 |
| M4 | `TimeNode.cs` | 複数インスタンスで EveryUpdate() 重複購読 |

---

## 2. デザイナー向け改善（今回のスプリントで対応完了）

| ファイル | 改善内容 |
|---|---|
| `EdgeVisualManager.cs` | 色・幅・閾値・グロー → `[SerializeField]` + Inspector属性 |
| `EdgeDragHandler.cs` | プレビュー幅・距離・閾値 → `[SerializeField]` |
| `SharedRaycastService.cs` | 最大距離 + LayerMask → `[SerializeField]` |
| `NodeVisualManager.cs` | ノードサイズ定数8個 → `[SerializeField]` |
| `AudioAnalyzer.cs` | FFTサイズ・サンプルレート → `[SerializeField]` |
| `ParamDefinition.cs` | 全フィールドに `[Header]`/`[Tooltip]`/`[ColorUsage]` |
| `WorldPanelHost.cs` | Shader → `[SerializeField]` 直接参照 |
| `NodeCategoryColors.cs` | static class → `ScriptableObject` + `[CreateAssetMenu]` |
| `NodeGrabHandler.cs` | 左手レイ距離 → `[SerializeField]` |
| `NodePanelLOD.cs` | LOD距離閾値7個 → `[SerializeField]` |

### デザイナー向け追加推奨

| 優先度 | 項目 | 工数 |
|---|---|---|
| **Must** | ハプティックフィードバック（エッジ接続・削除・グラブ） | 0.5日 |
| **Must** | `ScrollMenuVisualController` の定数 SerializeField化 | 0.5日 |
| **Must** | FPS警告の色分け表示（StatusPanel） | 0.5日 |
| Nice | 色覚バリアフリーモード切替 | 1日 |
| Nice | LODデバッグオーバーレイ | 1日 |
| Nice | ノードオブジェクトプール | 1日 |
| Nice | 左手レイキャスト統合（SharedRaycastService） | 0.5日 |

---

## 3. スケジュール分析

### 実績 vs. 計画

| Week | 計画 | 実装状況 | 完成度 |
|---|---|---|---|
| 1 | Core基盤 | ✅ 完了 | 100% |
| 2 | XR + 最小UI | ✅ 完了 | 100% |
| 3 | ノード操作 | ⚠️ VR検証未 | 90% |
| 4 | ノード + Audio | 🔧 準備中 | 30% |
| 5 | 統合 + 観客出力 | 📋 計画段階 | 5% |
| 6-6.5 | バッファ + 本番 | 📋 未着手 | 0% |

### 実装済みノード（17個/計画25個）

```
✅ 完成: ConstFloat, ConstColor, AudioTrigger, BeatDetector, TapTempo,
         Multiply, Smooth, Time, LFO, Noise, Threshold, Toggle,
         FloatMonitor, BoolMonitor, ColorMonitor, OscReceiver, MidiCC,
         SceneObject
❌ 未実装: Add, Remap, Delay, Timer, ColorToFloats, FloatsToColor,
           ColorToHSV, HSVToColor
```

---

## 4. 残り6週間のロードマップ

### Phase A: 統合・検証（Week 4前半、4日）
1. **VR実機テスト** — 全操作フローをQuest Linkで検証
2. **AudioAnalyzer 実機検証** — FFT精度・デバイス互換性
3. **Week 3 リバイス** — VR操作性修正

### Phase B: ノード実装（Week 4後半 + Week 5前半、6-7日）
4. **不足ノード実装** — Add, Remap, Delay, Timer, Color変換系（26h）
5. **VFXModule 統合テスト** — Audio → VFX 縦スライス完成
6. **ShaderModule 実装** — テスト用シェーダー + ノード動作確認

### Phase C: 観客出力統合（Week 5中盤、2-3日）
7. **ミラー出力統合** — MirrorOutputController + Cinemachine
8. **オーディオデバイス UI** — AudioDeviceSelector をメニューに統合
9. **縦スライスデモ統合テスト** — 全フロー VR確認

### Phase D: バッファ＆ポーランド（Week 5後半 + Week 6、5-6日）
10. **バグ修正・最適化** — メモリリーク、フレームレート改善
11. **VFX/Shader アセット制作** — ライブ用リアルアセット
12. **ステータスパネル完成** — UI接続確認
13. **リハーサル** — 30分通し稼働テスト
14. **v0.3.0 リリース** — 5月16日本番デプロイ

---

## 5. リスク軽減：スコープ削減候補

**時間が足りない場合にカット可能な項目：**
- ❌ ColorToHSV / HSVToColor（後日実装で十分）
- ❌ Delay ノード（シンプルなパフォーマンスでは不要）
- ❌ プリセット機能（グラフ保存/ロードのみでOK）
- ❌ CinemachineModule（環境制御は後回し）
- ❌ Spout/NDI 出力（v0.3.0 以降）

**最小限のライブ実現に必要な機能：**
```
✅ ノード生成・削除・エッジ接続
✅ ConstFloat + Time + Multiply
✅ AudioTrigger + BeatDetector
✅ VFXModule（1個）
✅ ミラー出力（60fps）
✅ ステータスパネル
```

---

## 6. 品質チェックポイント

### Weekly Review（毎金曜）
- [ ] VR実機でのコントローラー入力応答性
- [ ] 新実装ノードの信号伝播
- [ ] Profiler データ（FPS・メモリ）

### リリース前チェックリスト（5/14）
- [ ] 60ノード以上のグラフが安定動作
- [ ] 30分連続稼働でFPS dropなし
- [ ] ノード削除後のメモリ適切解放
- [ ] VRメニューの全ボタン反応
- [ ] AudioAnalyzer のデバイス切替
