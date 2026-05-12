# rhizomode — Technical Design Document

## 0. Overview

**rhizomode** は、VR空間内でノードグラフをライブビルドし、リアルタイム3D演出を構築・制御するパフォーマンスツール。

- **コンセプト**: 構築プロセス自体が演出。ノードを繋ぐことで演出に命が通る瞬間を観客と共有する。
- **ユーザー**: 自分専用
- **パフォーマンス形態**: ライブビルド型（VR内でゼロからノードを組む）
- **ターゲットデバイス**: PCVR（Quest Link）固定
- **レンダリングパイプライン**: URP（Unity6）
- **Rectorとの関係**: ゼロから設計。Rectorはプロトタイプとしての学習資産。コード再利用はモジュール単位で判断。

---

## 1. System Architecture

### 1.1 レイヤー構成

```
┌─────────────────────────────────────────────┐
│                  VR UI Layer                │
│   (Menu, Node Display, Status Panel)        │
├─────────────────────────────────────────────┤
│                  XR Layer                   │
│   (Controller Input, Ray Interaction)       │
├─────────────────────────────────────────────┤
│               Node Graph Layer              │
│   (NodeBase, Ports, Edges, GraphContext)     │
├─────────────────────────────────────────────┤
│             Performance Modules             │
│   (VFX, Shader, Cinemachine, Environment)   │
├─────────────────────────────────────────────┤
│                Audio Layer                  │
│   (AudioAnalyzer, Device Selection)         │
├─────────────────────────────────────────────┤
│                  Core Layer                 │
│   (Type System, Serialization, Signal Flow) │
└─────────────────────────────────────────────┘
```

### 1.2 Assembly Definition

```
Rhizomode.Core           — 型システム、シリアライズ、基盤インターフェース
Rhizomode.Nodes          — 各ノード実装              (参照: Core)
Rhizomode.Modules        — IPerformanceModule実装     (参照: Core)
Rhizomode.UI             — VR内UI                    (参照: Core, Nodes)
Rhizomode.XR             — XR入力、コントローラー      (参照: Core, UI)
Rhizomode.Audio          — AudioAnalyzer、デバイス管理  (参照: Core)
Rhizomode.ExternalInput  — OSC/MIDI入力              (参照: Core)
```

依存方向:
```
XR → UI → Nodes → Core
              Modules → Core
              Audio → Core
              ExternalInput → Core
```

循環参照はasmdefレベルで禁止。

### 1.3 外部ライブラリ

| ライブラリ | 用途 | 初期スコープ |
|---|---|---|
| R3 | ノード信号フロー基盤 | ✅ |
| NuGetForUnity | R3の依存管理 | ✅ |
| XR Interaction Toolkit | VRコントローラー入力 | ✅ |
| VFX Graph | パーティクル演出モジュール | ✅ |
| Cinemachine | 観客カメラ制御 | ✅ |
| UIToolkit | VR内UI（WorldSpace） | ✅ |
| UniTask | 非同期処理 | ✅ |
| KlakSpout / KlakNDI | 映像外部出力 | 後回し |

シリアライズはUnity標準の JsonUtility で開始。多態対応が必要になったら Newtonsoft.Json に移行。

---

## 2. Type System

### 2.1 パラメータ型

```csharp
public enum ParamType
{
    Float,
    Color,
    Bool
}
```

- **Float** — 連続値。パラメータ制御全般。ConstFloatの範囲は 0〜1 固定、Remapで変換。
- **Color** — 色。HSVホイールで入力。
- **Bool** — トリガー/ゲート。VFX Graph SendEvent、Activate/Deactivate、条件分岐。
- **Vector3** — 後日追加予定。追加時はポートインターフェースへの変更不要（object経由）。

### 2.2 ポート設計

```csharp
public interface IOutputPort
{
    ParamType Type { get; }
    IDisposable Subscribe(IInputPort input);
}

public interface IInputPort
{
    ParamType Type { get; }
    void OnNext(object value);
}

public class OutputPort<T> : IOutputPort
{
    private readonly Subject<T> _subject = new();
    public ParamType Type { get; }
    public Observable<T> Observable => _subject;

    public IDisposable Subscribe(IInputPort input)
    {
        return _subject.Subscribe(v => input.OnNext(v));
    }

    public void Emit(T value) => _subject.OnNext(value);
}

public class InputPort<T> : IInputPort
{
    private readonly Subject<T> _subject = new();
    public ParamType Type { get; }
    public Observable<T> Observable => _subject;

    public void OnNext(object value) => _subject.OnNext((T)value);
}
```

### 2.3 接続ルール

- **出力 → 複数入力**: OK（分配）
- **入力 ← 複数出力**: OK（Merge、最後に発行された値が勝つ）
- **型不一致**: 接続時バリデーションで弾く
- **同一フレーム内の複数発行順序**: 不定。許容する。

---

## 3. Node System

### 3.1 抽象基底クラス

```csharp
public abstract class NodeBase
{
    public string Id { get; }
    public string NodeType { get; }
    public List<PortDefinition> Inputs { get; }
    public List<PortDefinition> Outputs { get; }
    public Vector3 Position { get; set; }

    public abstract void Setup(GraphContext context);
    public abstract void Dispose();
}
```

各ノードは `Setup()` 内でR3のObservableチェーンを構築する。

### 3.2 GraphContext

ノード間接続を仲介する中核クラス。

```csharp
public class GraphContext
{
    // ノード管理
    public void RegisterNode(NodeBase node);
    public void RemoveNode(string nodeId);

    // エッジ管理
    public bool TryConnect(string fromNodeId, string fromPort,
                           string toNodeId, string toPort);
    public void Disconnect(string fromNodeId, string fromPort,
                           string toNodeId, string toPort);

    // 信号取得
    public Observable<T> GetInputObservable<T>(NodeBase node, string portName);
    public void SetOutput<T>(NodeBase node, string portName, T value);

    // シリアライズ
    public GraphData Serialize();
    public void Deserialize(GraphData data);
}
```

### 3.3 エッジとSubscription管理

- エッジごとに `IDisposable` を保持
- エッジ切断時: `Subscription.Dispose()`
- ノード削除時: 関連する全エッジを列挙して各Dispose
- 入力ポートはSubject経由で差し替え可能（ノード再構築不要）

```csharp
public class Edge
{
    public string Id;
    public string FromNodeId;
    public string FromPort;
    public string ToNodeId;
    public string ToPort;
    public IDisposable Subscription;
}
```

### 3.4 評価モデル

**ハイブリッド（プッシュ型 + 毎フレーム発行）**

- 基本: R3のプッシュ型リアクティブフロー。入力変化が下流に自動伝播。
- Time系ノード（Time, LFO, Noise）: `Observable.EveryUpdate()` で毎フレーム値を発行。
- 変化のないパスは静止し、不要な計算が走らない。

---

## 4. Node Catalog

### 4.1 カテゴリと色分け

| カテゴリ | 色 | 内容 |
|---|---|---|
| 入力系 | 青 | AudioTrigger, BeatDetector, TapTempo, ConstFloat, ConstColor |
| 数学/信号処理系 | 緑 | Multiply, Add, Remap, Smooth, LFO, Noise |
| 演出モジュール系 | 紫 | VFXModule, ShaderModule, CinemachineModule, EnvironmentModule |
| 時間系 | 黄 | Time, Timer, Delay |
| ユーティリティ系 | 灰 | ColorToFloats, FloatsToColor, ColorToHSV, HSVToColor, Threshold, Toggle |

### 4.2 初期スコープ（5月16日）

```
[AudioTrigger]
  in:  FreqMin (float)        ← 帯域下限（Hz）
  in:  FreqMax (float)        ← 帯域上限（Hz）
  in:  Threshold (float)
  out: Level (float)
  out: Trigger (bool)

[BeatDetector]
  in:  Trigger (bool)         ← AudioTriggerから
  out: BPM (float)
  out: Phase (float, 0〜1)
  out: Beat (bool)

[TapTempo]
  (コントローラーボタンでタップ)
  out: BPM (float)
  out: Phase (float, 0〜1)
  out: Beat (bool)

[ConstFloat]
  out: Value (float)          ← スライダー内蔵、0〜1固定

[Multiply]
  in:  A (float)
  in:  B (float)
  out: Result (float)

[Smooth]
  in:  Input (float)
  in:  Damping (float)
  out: Value (float)
  ※ IInlineButtonによるモード切替トグル（Lerp / EaseOut）実装済み

[Time]
  out: Time (float)           ← 毎フレーム発行

[Threshold]
  in:  Value (float)
  in:  Threshold (float)
  out: Gate (bool)

[Toggle]
  in:  Trigger (bool)
  out: State (bool)

[VFXModule]
  in:  (ModuleDefinitionから動的生成)
  ※ パラメータ分のConstFloat/ConstColorが自動スポーン＋プリコネクト

[ShaderModule]
  in:  (ModuleDefinitionから動的生成)
  ※ VFXModuleと同様
```

### 4.3 後日追加

```
[ConstColor]
  out: Value (Color)          ← HSVホイール内蔵

[LFO]
  in:  Frequency (float)
  in:  Amplitude (float)
  out: Value (float, 0〜1)
  ※ 波形切替トグル（Sin / Saw / Square / Triangle）

[Noise]
  in:  Speed (float)
  in:  Amplitude (float)
  in:  Seed (float)           ← 未接続時はランダム初期値を維持
  out: Value (float, 0〜1)

[Add]
  in:  A (float)
  in:  B (float)
  out: Result (float)

[Remap]
  in:  Value (float)
  in:  InMin (float)
  in:  InMax (float)
  in:  OutMin (float)
  in:  OutMax (float)
  out: Result (float)

[Delay]
  in:  Input (float)
  in:  DelayTime (float)
  out: Value (float)
  ※ 現在はFloat型のみ。全型対応は後日予定

[ColorToFloats]
  in:  Color (Color)
  out: R, G, B, A (float)

[FloatsToColor]
  in:  R, G, B, A (float)
  out: Color (Color)

[ColorToHSV]
  in:  Color (Color)
  out: H, S, V, A (float)

[HSVToColor]
  in:  H, S, V, A (float)
  out: Color (Color)

[CinemachineModule]
  in:  (ModuleDefinitionから動的生成)

[EnvironmentModule]
  in:  (ModuleDefinitionから動的生成)
```

---

## 5. Performance Modules

### 5.1 インターフェース

```csharp
public interface IPerformanceModule
{
    string ModuleName { get; }
    IReadOnlyList<ParamDefinition> Params { get; }
    void SetParam(string paramName, object value);
    void Activate();
    void Deactivate();
}
```

### 5.2 ModuleDefinition（ScriptableObject）

```csharp
[CreateAssetMenu(menuName = "Rhizomode/ModuleDefinition")]
public class ModuleDefinition : ScriptableObject
{
    public string moduleName;
    public GameObject prefab;
    public List<ParamDefinition> parameters;
}

[System.Serializable]
public class ParamDefinition
{
    public string name;
    public ParamType type;
    public float defaultFloat;
    public Color defaultColor;
    public bool defaultBool;
    public bool isEvent;          // trueの場合、Bool=trueでSendEvent発火
}
```

### 5.3 VFXModule実装

```csharp
public class VFXModule : MonoBehaviour, IPerformanceModule
{
    [SerializeField] private VisualEffect vfx;
    [SerializeField] private ModuleDefinition definition;

    public void SetParam(string paramName, object value)
    {
        var param = definition.GetParam(paramName);
        switch (param.Type)
        {
            case ParamType.Float:
                vfx.SetFloat(paramName, (float)value);
                break;
            case ParamType.Color:
                vfx.SetVector4(paramName, (Vector4)(Color)value);
                break;
            case ParamType.Bool:
                if (param.isEvent && (bool)value)
                    vfx.SendEvent(paramName);
                else
                    vfx.SetBool(paramName, (bool)value);
                break;
        }
    }
}
```

### 5.4 ShaderModule実装

```csharp
public class ShaderModule : MonoBehaviour, IPerformanceModule
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private ModuleDefinition definition;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    public void SetParam(string paramName, object value)
    {
        var param = definition.GetParam(paramName);
        switch (param.Type)
        {
            case ParamType.Float:
                _mpb.SetFloat(paramName, (float)value);
                break;
            case ParamType.Color:
                _mpb.SetColor(paramName, (Color)value);
                break;
            case ParamType.Bool:
                _mpb.SetFloat(paramName, (bool)value ? 1f : 0f);
                break;
        }
        targetRenderer.SetPropertyBlock(_mpb);
    }
}
```

MaterialPropertyBlock使用。マテリアルインスタンス生成なし、メモリリークの心配なし。SRP Batcherは効かなくなるが、パフォーマンス規模では問題なし。SetPropertyBlockはdirtyフラグ＋LateUpdateバッチ方式で、1フレーム内の複数SetParam呼び出しを1回の適用にまとめる。

### 5.5 モジュールノード生成時の動作

演出モジュールノード生成時、ModuleDefinitionに定義された全パラメータ分のConstFloat/ConstColorノードが自動スポーンし、プリコネクトされた状態で出現。

```
[ConstFloat] ──→ ParticleBloom.SpawnRate
[ConstFloat] ──→ ParticleBloom.Size
[ConstColor] ──→ ParticleBloom.BaseColor
```

ライブ中にBeatSyncを繋ぎたければ、ConstFloatのエッジを外して差し替える。

---

## 6. Audio System

### 6.1 AudioAnalyzer（シングルトン）

```csharp
public class AudioAnalyzer : MonoBehaviour
{
    private float[] _spectrum = new float[1024];
    private AudioSource _source;

    public void Initialize(string deviceName)
    {
        _source.clip = Microphone.Start(deviceName, true, 1, 48000);
        _source.loop = true;
        _source.Play();
    }

    private void Update()
    {
        _source.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
    }

    public float GetBandLevel(float freqMin, float freqMax)
    {
        int minBin = FreqToBin(freqMin);
        int maxBin = FreqToBin(freqMax);
        float sum = 0f;
        for (int i = minBin; i <= maxBin; i++)
            sum += _spectrum[i];
        return sum / (maxBin - minBin + 1);
    }

    private int FreqToBin(float freq)
    {
        return Mathf.Clamp(
            Mathf.RoundToInt(freq / (48000f / 1024f)),
            0, _spectrum.Length - 1);
    }
}
```

- FFTサイズ: **1024**
- サンプリングレート: 48kHz
- 帯域抽出は汎用。各AudioTriggerノードがFreqMin/FreqMaxで帯域を指定。
- 複数AudioTriggerが存在してもGetSpectrumDataは毎フレーム1回のみ。

### 6.2 オーディオデバイス選択

`Microphone.devices` で全入力デバイスを列挙。VR内のプロパティパネル（ノード生成メニュー内）からリスト選択。マイク、VoiceMeeter仮想デバイス、Stereo Mix等すべて同じ `Microphone.Start(deviceName)` で統一。

---

## 7. VR Interaction

### 7.1 コントローラーマッピング

| ボタン | 機能 |
|---|---|
| 右手トリガー | レイ選択 / エッジドラッグ / UI操作 |
| 右手グリップ | ノードのグラブ移動 |
| 右手A | ノード削除 |
| 右手B | エッジ切断 |
| 左手X | ノード生成メニュー呼び出し |
| 左手サムスティック | スムース移動 |
| 右手サムスティック | スナップターン |
| 左手トリガー | 巻物メニュー操作（ドラッグ展開 / ボタンクリック） |
| 左手グリップ | ノードのグラブ移動（左手） |
| 左手Y | 未割り当て（拡張用） |

### 7.2 移動方式

スムース移動。3D空間にノードを散らす設計のため、滑らかな移動で空間把握を維持。

### 7.3 ノード生成メニュー

- 呼び出し: 左手Xボタンでカテゴリバー表示。左手トリガーでバーをドラッグして巻物展開。
- 表示方式: プレイヤーの腰付近に追従する巻物メニュー（カテゴリバー + スクロールパネル）
- 操作: 左手トリガーで展開、左右どちらのトリガーでもボタン選択可、Xボタンで閉じ

```
[ノード生成メニュー]
├─ 入力
├─ 数学/信号処理
├─ 演出モジュール
├─ 時間
├─ ユーティリティ
└─ プロパティ（設定）
    ├─ オーディオデバイス選択
    ├─ グラフ セーブ/ロード
    ├─ AudioTrigger閾値（※実際はConstFloat経由）
    ├─ ミラーカメラ ダンピング
    └─ モジュールセット切り替え
```

### 7.4 ノード操作

- **配置**: 完全手動3D配置。ノードの空間構成自体が演出。
- **サイズ**: 基本中型（20cm×12cm）。パラメータ数やインラインUIに応じて動的リサイズ（UIToolkit flexbox）。
- **視覚的区別**: カテゴリごとの背景色分け。
- **見失い対策**: 全ノード一覧ボタン（ノード名リスト表示 → 選択でハイライト＋視線誘導）＋個別位置リセットボタン。

### 7.5 エッジ接続

- **方式**: 2クリックステートマシン方式（Rector踏襲）。出力ポート付近をトリガー → プレビューライン表示 → 入力ポート付近をトリガー → 接続完了。ドラッグ不要。
- **ポート自動選択**: 最近接ポートを閾値内で自動選択（`portSelectThreshold`）
- **型バリデーション**: 互換入力ポートのみハイライト。型不一致はTryConnect時にバリデーションで弾く。
- **キャンセル**: 空中クリック or 同一ノードクリック → Idle復帰
- **切断**: エッジにレイを当てる → ハイライト → 右手B

### 7.6 ノード削除

選択 → 右手A → 即削除（確認なし）。Undoなし。

### 7.7 ワイヤー表現

直線（LineRenderer）。マテリアル差し替え可能。後からシェーダーで見た目を拡張できる設計。

### 7.8 カラーピッカー

HSVホイール＋明度スライダー。ConstColorノード内にインライン埋め込み。レイキャストで色相を指定。

---

## 8. Viewer Output

### 8.1 ミラー出力

VR HMDの視点をCinemachineダンピング付きで追従するカメラの映像を、RenderTexture（1920×1080）に出力。

```csharp
// Cinemachine標準機能で実装
// Transposer: Damping X/Y/Z で位置追従速度
// Composer: Damping で回転追従速度
```

MirrorOutputControllerがVR HMDの位置・回転をLerp/Slerpダンピングで追従するカメラを管理。RenderTexture（1920×1080）に出力。

### 8.2 Spout / NDI

RenderTextureの出力先としてSpout/NDI送信を実装済み。`SpoutSenderController` / `NdiSenderController` がMirrorOutputControllerのRenderTextureを受け取って送信。KlakSpout / KlakNDI パッケージ未インストール時は `#if` で無効化。

---

## 9. Serialization

### 9.1 グラフデータ構造

```json
{
  "version": "1.0",
  "nodes": [
    {
      "id": "n1",
      "type": "BeatDetector",
      "position": [0, 1.5, 0],
      "params": {},
      "groupId": null
    },
    {
      "id": "n2",
      "type": "VFXModule",
      "position": [1.5, 1.5, 0],
      "params": { "prefabId": "ParticleBloom" },
      "groupId": null
    }
  ],
  "edges": [
    {
      "id": "e1",
      "from": "n1",
      "fromPort": "Beat",
      "to": "n2",
      "toPort": "SpawnRate"
    }
  ]
}
```

- フラットなノード＋エッジリスト（隣接リスト表現）
- `groupId`: サブグラフ拡張用の予約フィールド。初期はnull。
- セーブファイル命名: `snake_case.json`（例: `live_set_20260401.json`）
- 保存先: `Assets/Data/SavedGraphs/`

### 9.2 セーブ/ロード

- 初期スコープ: グラフ全体のセーブ/ロード
- 後日: サブグラフ（部品テンプレート）対応。groupIdで部分グラフを抽出・挿入。

---

## 10. Error Handling

### 10.1 接続時バリデーション

- 型チェック: `output.Type != input.Type` で弾く
- ループ検出: 初期スコープでは実装しない（R3のプッシュ型で無限ループはスタックオーバーフローになるため、後日対応）

### 10.2 ランタイムフォールバック

- NaN伝播: 検出したらデフォルト値（float=0, Color=black, bool=false）にフォールバック
- null参照: nullable有効 + try-catchでフォールバック
- パフォーマンスモジュール異常: SetParamをtry-catchで囲み、例外時はスキップ
- **映像は絶対に止めない**

### 10.3 視覚フィードバック

- エラー状態のノード: 赤色にハイライト
- エラー状態のエッジ: 赤色 or 点線表示

---

## 11. Viewer Status Panel

ワールド空間固定のUIToolkit WorldSpaceパネル。

表示項目:
```
Nodes: 23
Edges: 31
Active Modules: ParticleBloom, RaymarchDistortion
BPM: 128
FPS: 90 / 60 (VR / Mirror)
Audio Device: Scarlett 2i2
```

ログ出力は `Debug.Log` のみ。カスタムロガーは不要。

---

## 12. Environment

### 12.1 環境アセット

3D環境をアセットとして事前に作成し、パフォーマンスごとに読み込む。Scale、位置、マテリアル等のプロパティを公開。

### 12.2 EnvironmentModule

`IPerformanceModule` として実装。ノードグラフからScale、Rotation、Materialプロパティを制御可能。ビートに合わせて空間が脈動する等の演出ができる。

---

## 13. Project Structure

### 13.1 フォルダ構成

```
Assets/
├─ Runtime/
│   ├─ Core/                  # 型システム、信号フロー基盤、シリアライズ
│   ├─ Nodes/                 # 各ノード実装
│   │   ├─ Input/
│   │   ├─ Math/
│   │   ├─ Modules/
│   │   ├─ Time/
│   │   └─ Utility/
│   ├─ Modules/               # IPerformanceModule実装＋Prefab
│   ├─ UI/                    # VR内UI（メニュー、カラーピッカー等）
│   ├─ XR/                    # XR入力、レイキャスト、コントローラー管理
│   └─ Audio/                 # AudioAnalyzer、AudioTrigger、デバイス選択
├─ Data/
│   ├─ ModuleDefinitions/     # ScriptableObject（モジュール定義）
│   ├─ Environments/          # 環境アセット
│   └─ SavedGraphs/           # セーブデータ（JSON）
├─ Shaders/                   # カスタムシェーダー、ワイヤー用等
├─ VFX/                       # VFX Graphアセット
└─ Scenes/
    └─ Main.unity
```

### 13.2 命名規則

| 対象 | 規則 | 例 |
|---|---|---|
| クラス名 | PascalCase | `BeatDetectorNode` |
| インターフェース | I + PascalCase | `IPerformanceModule` |
| ScriptableObject | PascalCase + Definition | `ModuleDefinition` |
| フィールド | camelCase | `spawnRate` |
| SerializeField | camelCase | `[SerializeField] private VisualEffect vfxGraph` |
| 定数 | PascalCase | `DefaultBPM` |
| ノードタイプ名 | PascalCase文字列 | `"BeatDetector"` |
| SOアセット名 | PascalCase | `ParticleBloom.asset` |
| セーブデータ | snake_case.json | `live_set_20260401.json` |

### 13.3 C#設定

- C# 9（Unity6デフォルト）
- `#nullable enable`（全ファイル）

---

## 14. Git Strategy

### 14.1 ブランチ戦略

- **main**: 常に動く状態を保証
- **feature/xxx**: 機能ごとにブランチ。動いたらmainにマージ。

### 14.2 リリース管理

機能が揃った時点でタグを打つ。通しテストはタグとは独立して実施。

```
v0.1.0 — 縦のスライス（BeatSync → VFXModule 1本接続）
v0.2.0 — 全初期ノード＋VR UI完成
v0.3.0 — ミラー出力＋Audio完成（5月16日ライブ目標）
```

---

## 15. Testing

### 15.1 方針

Coreのみユニットテスト（EditMode）。

テスト対象:
- ノードの信号フロー評価（接続→値伝播→Dispose）
- 型バリデーション（不正接続の拒否）
- シリアライズ/デシリアライズ（JSON往復の整合性）

個別ノードの動作はVR内で手動確認。PlayModeテスト不要。

---

## 16. Performance Budget

### 16.1 ノード規模

- 演出モジュール: 5〜10個
- 数学/制御ノード: 20〜50個
- 合計ノード上限目安: 60個

### 16.2 フレームレート目標

- VR側: 90fps（必須）
- ミラー出力: 60fps（RenderTexture Blit）

### 16.3 レンダリング

- VR: Single Pass Instanced（URP）
- ミラー: VR映像のBlit + リサイズ（追加レンダリングなし）

---

## 17. Development Schedule

### Week 1（〜4/6）: 基盤
- プロジェクト作成、asmdef設定、Git初期化
- Core信号フロー（R3ベース、Node → Port → Edge データ構造）
- IPerformanceModule定義、ModuleDefinition ScriptableObject設計
- ノードの型システム（Float + Color + Bool 構造定義）

### Week 2（〜4/13）: XR + 最小UI
- XR Interaction Toolkit設定、コントローラー入力
- ノード生成メニュー（ポップアップ → ワールド固定）
- ノードのWorldSpace表示（色分け）

### Week 3（〜4/20）: ノード操作
- エッジ接続（ドラッグ + スナップ + 型ハイライト）
- エッジ切断
- ノード削除
- ノードのグラブ移動

### Week 4（〜4/27）: ノード実装 + Audio
- AudioTrigger, BeatDetector 実装
- ConstFloat（スライダー付き）
- Multiply, Smooth, Time, Threshold, Toggle
- VFXModule実装 + テスト用VFX Graphアセット1個

### Week 5（〜5/4）: 統合 + 観客出力
- 縦のスライス結合（VRでBeatSync → VFXModule動作確認）
- ShaderModule実装
- ミラー出力 + Cinemachineダンピング
- オーディオデバイス選択UI

### Week 6〜6.5（〜5/16）: バッファ + 演出準備
- バグ修正
- パフォーマンス用VFX Graph / Shaderアセット制作
- ステータスパネル実装
- 通しリハーサル
- タグ打ち（v0.3.0）

---

## 18. Future Scope（5月16日以降）

### 実装済み（当初は後日予定だった機能）
- ✅ ConstColor + HSVホイール
- ✅ LFO (4波形切替), Noise (Seed入力), Add, Remap, Delay (Float only)
- ✅ ColorToFloats / FloatsToColor / ColorToHSV / HSVToColor
- ✅ セーブ/ロード (GraphSaveLoadManager)
- ✅ Spout / NDI出力 (SpoutSenderController, NdiSenderController)
- ✅ OSC / MIDI入力 (OscServer + OscReceiverNode, MidiServer + MidiCCNode)

### 未実装（ライブ後に検討）
- Vector3型追加
- CinemachineModule, EnvironmentModule
- Delay全型対応（Color/Bool対応、現在はFloat only）
- サブグラフ（部品テンプレート）— PresetManagerでプリセット機能は実装済み
- ループ検出
