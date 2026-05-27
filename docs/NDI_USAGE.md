# NDI Usage Guide

rhizomode で他のマシン / アプリ (OBS NDI plugin / TouchDesigner / vMix / 別の Unity 等)
から NDI ストリームを受信して VR 空間に表示する方法。

## アーキテクチャ (Plan v0.3 ベース)

NDI 受信は **node = property 編集** と **window = 表示** が分離している:

```
NdiReceiver node                  ←  source name 選択 / 接続状態 / Active toggle
       │                             (NodeVisual panel、grabbable で普通のノードと同様)
       │ INdiViewWindowState (side-channel)
       ▼
NdiViewWindow (独立 GameObject)    ←  実際の NDI 映像を表示する Quad
       │                             grabbable + 2-hand scale + transform 永続化
       └─ WindowGrabHandle           1-hand: pos+yaw / 2-hand: uniform scale
```

## ノードを置いて NDI を受信する

1. **NDI source を起動**: ネットワーク上のどこか (例えば OBS Studio の NDI plugin) で
   NDI 出力を有効にする。`Klak.Ndi.NdiFinder` が同 LAN 上の NDI source を自動検出する
2. **rhizomode を Play / VR 起動**
3. **左 X ボタン → ノードメニュー** → `NDI Receiver` を選択して spawn
4. ノードが置かれた瞬間に、**HMD の正面 1.5m + cascade offset** に NDI 受信 window が出現
5. SourceName が空のままなら presenter が `NdiFinder` から auto-pick して表示開始
6. 既に他のノードが claim 済の source は重複しないよう除外される
7. 受信が始まれば window 上に映像が live blit される

## Window 操作

| 操作 | 動作 |
|---|---|
| 右 Grip + window collider にレイ命中 + Grip 押下 | **1-hand grab**: window pose を controller に追従。pitch ±60° clamp、roll は 0 lock、yaw 自由 |
| 1-hand grab 中に左 Grip + 左ray も window 命中 | **2-hand scale**: 両 controller の距離比で uniform scale (`MinScale=0.1 / MaxScale=4.0` clamp)、translate/rotate は凍結 |
| 両 Grip release | 最終 pose / scale が node の paramsJson に commit → cue save の対象になる |

## Cue 保存 / 復元

- Cue 保存時、各 NdiReceiver node の paramsJson に以下が含まれる:
  - `sourceName`
  - `windowPosition` / `windowEulerAngles` / `windowScale`
  - `hasExplicitWindowTransform` (旧 cue forward-compat フラグ)
  - `hideFromMirror` (mirror 出力に映すかどうか)
- Cue load 時に `hasExplicitWindowTransform = true` なら保存された transform で window 再配置
- `false` (旧 cue) なら HMD 正面 1.5m + cascade offset の default 位置にフォールバック
- 同じ nodeId は session 跨いで決定的な cascade slot に出現 (FNV-1a 32bit hash)

## Mirror / 配信に映すかどうか

Default では window は **mirror 出力に映る** (= Spout / NDI / desktop 配信に表示される)。

ノードの paramsJson 経由で `hideFromMirror: true` に設定すると、当該 window だけ MirrorHidden layer に置かれ、VR HMD では見えるが配信には映らなくなる (`MirrorHiddenLayer.ApplyRecursive`)。これは現状 inspector / 手動 JSON 編集でしか切替できない。

## 複数 NDI window の配置

複数の NdiReceiver node を spawn すると:

- 各 window は cascade offset (`1.2m` 横方向 × 8 slots + `0.3m` 奥行きずらし) で重ならない位置に出る
- 同じ node の reload は同じ slot に出る (deterministic、cue save → 別 session で load しても再現)
- 衝突を完全に避けたい場合は手動で grab 移動して位置を調整 → cue save で保存

## トラブルシューティング

| 症状 | 原因 / 対処 |
|---|---|
| Window は出るが映像が真っ黒 | NDI source が同 LAN 上で broadcast されているか確認 (`OBS NDI plugin` 等)。`Klak.Ndi.NdiFinder` のソース一覧を Unity Console で確認 |
| Window が消える | node が delete されたか、cue 切替で graph clear が走ったか確認。`NdiWindowsRoot.DestroyFor` のログ |
| grab しても window が動かない | `WindowGrabBootstrap` が SampleScene に居るか確認 (Inspector で確認)、VContainer の DI が走っているか console error チェック |
| Cue load 後 window が default 位置に戻る | 旧 cue (NDI 機能 v0.3 以前) で paramsJson に transform が無いケース。一度 grab + release で commit すれば次回以降は復元される |
| Bloom halo で window が眩しく見える | env scene の `SceneVolumeOverride` で Bloom intensity を下げる (Plan env-scene-isolation v0.3) |

## 関連ファイル

- 設計プラン: `docs/plans/ndi-view-window.md` (v0.3)
- ノード本体: `rhizomode/Assets/Runtime/Nodes/Video/NdiReceiverNode.cs`
- Presenter: `rhizomode/Assets/Runtime/UI/Presentation/NdiReceiverPresenter.cs`
- Window 本体: `rhizomode/Assets/Runtime/UI/Presentation/NdiViewWindow.cs`
- Window registry: `rhizomode/Assets/Runtime/UI/Presentation/NdiWindowsRoot.cs`
- Cascade math: `rhizomode/Assets/Runtime/UI/Contracts/NdiViewWindowMath.cs`
- Grab handle: `rhizomode/Assets/Runtime/Interaction/WindowGrabHandle.cs`
- Grab bootstrap: `rhizomode/Assets/Runtime/Interaction/WindowGrabBootstrap.cs`
- Tests: `rhizomode/Assets/Tests/Editor/{Core,UI,Interaction}/Ndi*` (41 件 PASS)
