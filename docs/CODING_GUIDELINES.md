# rhizomode — Coding Guidelines

## 基本方針

1. **カスタマイズ性** — 次の開発に向けて堅牢なコードを組む
2. **安定性** — 破壊的変更はバグが見つからない限り行わない
3. **明瞭性** — 機能を最小に分け、ファイル群はわかりやすい命名をとる

---

## 1. カスタマイズ性：拡張に開き、変更に閉じる

### 1.1 インターフェースで境界を切る

モジュール間の接続は必ずインターフェース経由。具体型への依存を避ける。

```csharp
// ✅ Good: インターフェースに依存
public class GraphContext
{
    public bool TryConnect(IOutputPort output, IInputPort input) { ... }
}

// ❌ Bad: 具体型に依存
public class GraphContext
{
    public bool TryConnect(OutputPort<float> output, InputPort<float> input) { ... }
}
```

### 1.2 新しい型・ノード・モジュールの追加で既存コードを変更しない

- **新しいParamType追加**: IOutputPort/IInputPortのobject経由で型が流れるため、GraphContextの変更不要
- **新しいノード追加**: NodeBaseを継承してSetup()を実装するだけ。既存ノードに触らない
- **新しい演出モジュール追加**: IPerformanceModuleを実装してScriptableObjectを作るだけ

```csharp
// ノード追加の例: 既存コードへの変更ゼロ
public class ClampNode : NodeBase
{
    public override void Setup(GraphContext context)
    {
        var input = context.GetInputObservable<float>(this, "Input");
        var min = context.GetInputObservable<float>(this, "Min");
        var max = context.GetInputObservable<float>(this, "Max");

        input.CombineLatest(min, max, Mathf.Clamp)
             .Subscribe(v => context.SetOutput(this, "Result", v));
    }
}
```

### 1.3 設定値はハードコードしない

マジックナンバーはconstまたはScriptableObjectに外出しする。

```csharp
// ✅ Good
public static class AudioConfig
{
    public const int FFTSize = 1024;
    public const int SampleRate = 48000;
}

// ❌ Bad
source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris); // 1024がどこにもない
```

### 1.4 依存性注入を使う

MonoBehaviourの場合は`[SerializeField]`でInspector注入。純粋C#クラスはコンストラクタ注入。

```csharp
// ✅ Good: 外から注入
public class AudioTriggerNode : NodeBase
{
    private readonly AudioAnalyzer _analyzer;

    public AudioTriggerNode(AudioAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }
}

// ❌ Bad: 内部で直接取得
public class AudioTriggerNode : NodeBase
{
    private AudioAnalyzer _analyzer = AudioAnalyzer.Instance; // 隠れた依存
}
```

### 1.5 publicを最小にする

外部から触る必要がないものは全てprivateまたはinternal。asmdefの境界でinternalが効く。

```csharp
// ✅ Good
public class VFXModule : MonoBehaviour, IPerformanceModule
{
    [SerializeField] private VisualEffect vfx;
    private MaterialPropertyBlock _mpb;

    public void SetParam(string paramName, object value) { ... }  // インターフェース公開
    private void ApplyFloat(string name, float v) { ... }         // 内部実装
}
```

---

## 2. 安定性：壊さない、壊れない

### 2.1 破壊的変更の定義

以下に該当する変更を「破壊的変更」とする:

- インターフェースのシグネチャ変更（メソッド追加・削除・引数変更）
- publicクラスのpublicメソッドのシグネチャ変更
- シリアライズ形式の非互換変更（JSONフィールドの削除・リネーム）
- asmdefの依存方向の変更
- ノードのポート名変更（セーブデータとの互換性が壊れる）

### 2.2 破壊的変更の回避パターン

**インターフェースに機能を足したいとき:**

```csharp
// ❌ Bad: 既存インターフェースを変更
public interface IPerformanceModule
{
    void SetParam(string paramName, object value);
    void SetParamSmooth(string paramName, object value, float duration); // 破壊的追加
}

// ✅ Good: 新しいインターフェースを追加して、任意実装にする
public interface ISmoothableModule
{
    void SetParamSmooth(string paramName, object value, float duration);
}

// 呼び出し側
if (module is ISmoothableModule smoothable)
    smoothable.SetParamSmooth(name, value, 0.5f);
else
    module.SetParam(name, value);
```

**シリアライズフィールドを追加したいとき:**

```json
// ✅ Good: フィールド追加のみ。既存フィールドは触らない。デフォルト値で安全にデシリアライズ。
{
  "id": "n1",
  "type": "VFXModule",
  "position": [0, 1.5, 0],
  "groupId": null,
  "tags": []          // ← 新規追加。古いセーブデータにはないがデフォルト空配列で安全
}
```

### 2.3 deprecatedパターン

廃止したいメソッドは即削除せず、まずObsoleteを付ける。

```csharp
[System.Obsolete("Use SetParam(string, object) instead. Will be removed in v0.5.0")]
public void SetValue(string paramName, float value)
{
    SetParam(paramName, value);
}
```

### 2.4 防御的プログラミング

ライブ中にクラッシュさせない。

```csharp
// ✅ Good: 失敗しても止まらない
public void SetParam(string paramName, object value)
{
    try
    {
        var param = definition.GetParam(paramName);
        if (param == null)
        {
            Debug.LogWarning($"[{ModuleName}] Unknown param: {paramName}");
            return;
        }
        ApplyParam(param, value);
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[{ModuleName}] SetParam failed: {paramName} — {e.Message}");
    }
}

// ❌ Bad: 例外が上に抜ける
public void SetParam(string paramName, object value)
{
    var param = definition.GetParam(paramName); // nullならNullReferenceException
    ApplyParam(param, value);                    // キャスト失敗でInvalidCastException
}
```

### 2.5 フォールバック値

型ごとのデフォルト値を定数で定義。エラー時はこれに戻す。

```csharp
public static class ParamDefaults
{
    public const float Float = 0f;
    public static readonly Color Color = UnityEngine.Color.black;
    public const bool Bool = false;
}
```

---

## 3. 明瞭性：読めば意図がわかるコード

### 3.1 1ファイル = 1責務

1つのC#ファイルに1つのクラス/インターフェース。ネストクラスは原則禁止（private実装の小さなヘルパーは例外）。

```
// ✅ Good
Runtime/Core/
├─ IOutputPort.cs
├─ IInputPort.cs
├─ OutputPort.cs
├─ InputPort.cs
├─ GraphContext.cs
├─ NodeBase.cs
├─ Edge.cs
└─ ParamType.cs

// ❌ Bad
Runtime/Core/
├─ Ports.cs              // IOutputPort + IInputPort + OutputPort + InputPort が全部入ってる
└─ Graph.cs              // GraphContext + Edge + NodeBase が全部入ってる
```

### 3.2 ファイル名 = クラス名

例外なし。ファイル名を見ればクラス名がわかり、クラス名を見ればファイルが見つかる。

```
IPerformanceModule.cs  → public interface IPerformanceModule
VFXModule.cs           → public class VFXModule
BeatDetectorNode.cs    → public class BeatDetectorNode
ModuleDefinition.cs    → public class ModuleDefinition
```

### 3.3 命名で意図を伝える

#### メソッド名

```csharp
// ✅ Good: 何をするかが名前でわかる
public bool TryConnect(...)     // 失敗する可能性があることが明白
public void Activate()          // 副作用があることが明白
public Observable<T> GetInputObservable<T>(...) // 戻り値の型が名前に含まれる

// ❌ Bad
public bool Connect(...)        // 失敗時にどうなるか不明（例外？false？）
public void Do()                // 何をするか不明
public object Get(...)          // 何を返すか不明
```

#### 変数名

```csharp
// ✅ Good: 役割が明確
private float freqMin;
private float freqMax;
private IDisposable edgeSubscription;
private Subject<float> beatSignal;

// ❌ Bad: 抽象的すぎる
private float a;
private float b;
private IDisposable sub;
private Subject<float> s;
```

#### bool変数・メソッド

```csharp
// ✅ Good: is/has/can で始める
public bool IsActive { get; }
public bool HasConnections(string nodeId);
public bool CanConnect(IOutputPort output, IInputPort input);

// ❌ Bad
public bool Active { get; }
public bool Connections(string nodeId);  // 何を返すのか不明
```

### 3.4 コメントの方針

「何をしているか」ではなく「なぜそうしているか」を書く。

```csharp
// ✅ Good: 理由が書いてある
// MaterialPropertyBlockを使用。マテリアルインスタンスの生成を避けてメモリリークを防止。
// SRP Batcherは効かなくなるが、演出モジュール数（最大10）では影響なし。
private MaterialPropertyBlock _mpb;

// ✅ Good: 非自明なロジックの意図
// 同一フレーム内で複数ソースが発行した場合、最後の値が勝つ（仕様上許容）
return Observable.Merge(sources);

// ❌ Bad: コードを読めばわかることを書いている
// floatを設定する
vfx.SetFloat(paramName, value);

// ❌ Bad: コメントなしで意図不明
private const float Threshold = 0.003f;  // なぜ0.003？
```

### 3.5 XMLドキュメントコメント

publicなインターフェース・クラス・メソッドには必ず付ける。

```csharp
/// <summary>
/// ノード間のエッジ接続を試行する。型が不一致の場合はfalseを返す。
/// </summary>
/// <returns>接続成功でtrue。型不一致でfalse。</returns>
public bool TryConnect(string fromNodeId, string fromPort,
                       string toNodeId, string toPort)
```

private メンバーには不要。名前で意図が伝わらない場合のみ // コメントを付ける。

### 3.6 メソッドの長さ

1メソッド30行以内を目安。超えたら分割を検討する。

```csharp
// ✅ Good: 処理が分割されている
public override void Setup(GraphContext context)
{
    var input = BindInput(context);
    var damping = BindDamping(context);
    SubscribeSmooth(context, input, damping);
}

// ❌ Bad: Setupに全処理が詰まっている（50行超）
public override void Setup(GraphContext context)
{
    // ... 50行のObservableチェーン ...
}
```

### 3.7 regions禁止

`#region` は使わない。メソッドが多すぎてregionで整理したくなったら、クラスを分割すべきサイン。

### 3.8 マジックナンバー禁止

```csharp
// ✅ Good
private const float SnapRadius = 0.1f;        // 10cm
private const float PortDiameter = 0.03f;      // 3cm
private const int MaxNodes = 60;

// ❌ Bad
if (distance < 0.1f) { ... }
```

---

## 4. ディレクトリとファイルの命名規則

### 4.1 ディレクトリ名

PascalCase。機能ドメインを表す名前。

```
Runtime/Core/
Runtime/Nodes/Input/
Runtime/Nodes/Math/
Runtime/Nodes/Modules/
Runtime/Nodes/Time/
Runtime/Nodes/Utility/
Runtime/Modules/
Runtime/UI/
Runtime/XR/
Runtime/Audio/
Data/ModuleDefinitions/
Data/Environments/
Data/SavedGraphs/
```

### 4.2 ファイル配置の原則

「このクラスはどこにある？」と思ったとき、2秒以内に見つかる構成にする。

- ノードの実装 → `Runtime/Nodes/{カテゴリ}/`
- モジュールの実装 → `Runtime/Modules/`
- Core基盤 → `Runtime/Core/`
- UIコンポーネント → `Runtime/UI/`

迷ったら「このクラスが依存しているasmdefはどれか」で判断する。

### 4.3 テストファイル

```
Tests/
├─ Editor/
│   ├─ Core/
│   │   ├─ GraphContextTests.cs
│   │   ├─ PortConnectionTests.cs
│   │   └─ SerializationTests.cs
```

テストクラス名 = テスト対象クラス名 + `Tests`。

---

## 5. Gitコミットの規則

### 5.1 コミットメッセージ

```
feat: BeatDetectorNode実装
fix: AudioTrigger閾値が反映されない問題を修正
refactor: GraphContext.TryConnectの型チェックをIOutputPortに移動
docs: TECHNICAL_DESIGN.mdにEnvironmentModule仕様を追記
test: PortConnectionTestsにColor型のテストを追加
chore: asmdef参照設定を修正
```

prefix必須: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`

### 5.2 コミット粒度

1コミット = 1つの論理的変更。「ノード追加＋バグ修正＋リファクタ」を1コミットにしない。

---

## 6. チェックリスト

新しいコードを書く前に確認:

- [ ] 既存のインターフェースを変更していないか？
- [ ] 既存のpublicメソッドのシグネチャを変更していないか？
- [ ] シリアライズ形式の互換性を壊していないか？
- [ ] asmdefの依存方向を守っているか？
- [ ] 1ファイル1クラスになっているか？
- [ ] ファイル名とクラス名が一致しているか？
- [ ] マジックナンバーがないか？
- [ ] publicは最小限か？
- [ ] try-catchで囲むべき箇所を囲んでいるか？
- [ ] コミットメッセージにprefixが付いているか？
