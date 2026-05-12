# アーキテクチャとアセンブリ定義 (Architecture & Assembly Definitions)

<details>
<summary>関連ソースファイル</summary>

このWikiページの生成にあたって、以下のファイルがコンテキストとして使用されました：

- [CLAUDE.md](../../CLAUDE.md)
- [docs/CODING_GUIDELINES.md](../CODING_GUIDELINES.md)
- [docs/TECHNICAL_DESIGN.md](../TECHNICAL_DESIGN.md)
- [rhizomode/Assets/Runtime/UI/Rhizomode.UI.asmdef](../../rhizomode/Assets/Runtime/UI/Rhizomode.UI.asmdef)
- [rhizomode/Assets/Runtime/XR/Rhizomode.XR.asmdef](../../rhizomode/Assets/Runtime/XR/Rhizomode.XR.asmdef)

</details>



**rhizomode** システムは、VR上でリアルタイムにノードグラフを構築可能としつつ、関心の分離を厳格に保つために設計された、6つのアセンブリからなる階層アーキテクチャ上に構築されています [docs/TECHNICAL_DESIGN.md:18-40]()。Unity の Assembly Definition (`.asmdef`) を用いることで、本プロジェクトは単方向の依存フローを強制し、循環参照を防止するとともに、コアロジックを特定のハードウェアや UI 実装から疎結合に保ちます [docs/TECHNICAL_DESIGN.md:60-60]()。

## アセンブリ階層の内訳 (Assembly Layer Breakdown)

ライブパフォーマンスパイプラインにおいて、本システムは責務ごとに6つの主要アセンブリへ分割されています。

| アセンブリ | 責務 | 依存先 |
| :--- | :--- | :--- |
| `Rhizomode.Core` | 型システム、ポートインタフェース、グラフ管理、シリアライゼーション。 | なし (スタンドアロン) |
| `Rhizomode.Nodes` | math・logic・utility ノードの具象実装。 | `Core` |
| `Rhizomode.Modules` | VFX Graph や Shader コントローラなどの重い処理を行うモジュール。 | `Core` |
| `Rhizomode.Audio` | リアクティブシグナル向けの音声解析とデバイス管理。 | `Core` |
| `Rhizomode.UI` | VR のワールドスペースUI、ノードビジュアル、生成メニュー。 | `Core`, `Nodes` |
| `Rhizomode.XR` | コントローラ入力ルーティングとレイベースのインタラクションハンドラー。 | `Core`, `UI` |

**ソース:** [docs/TECHNICAL_DESIGN.md:42-51](), [CLAUDE.md:30-38]()

## 依存フローとルール (Dependency Flow & Rules)

本アーキテクチャは「ボトムアップ」型の依存ルールを厳格に守ります。`Rhizomode.XR` のような高レベルアセンブリは `Rhizomode.Core` のような低レベル基盤に依存しますが、コアはその上位レイヤーを一切認識しません [docs/TECHNICAL_DESIGN.md:53-58]()。

### アセンブリ依存図 (Assembly Dependency Diagram)
次の図は単方向の流れと、アセンブリ間のやり取りを示します。

```mermaid
graph TD
    subgraph ["高レベル (入力とプレゼンテーション)"]
        XR["Rhizomode.XR"]
        UI["Rhizomode.UI"]
    end

    subgraph ["中レベル (ロジックとコンテンツ)"]
        Nodes["Rhizomode.Nodes"]
        Modules["Rhizomode.Modules"]
        Audio["Rhizomode.Audio"]
    end

    subgraph ["基盤"]
        Core["Rhizomode.Core"]
    end

    XR --> UI
    XR --> Core
    UI --> Nodes
    UI --> Core
    Nodes --> Core
    Modules --> Core
    Audio --> Core

    style Core stroke-width:4px
```
**ソース:** [docs/TECHNICAL_DESIGN.md:54-58](), [rhizomode/Assets/Runtime/UI/Rhizomode.UI.asmdef:4-8](), [rhizomode/Assets/Runtime/XR/Rhizomode.XR.asmdef:4-11]()

## Open/Closed の設計思想 (Open/Closed Design Philosophy)

「拡張には開いており、修正には閉じている」を担保するため、rhizomode はインタフェース境界に強く依存します [docs/CODING_GUIDELINES.md:11-15]()。

1.  **インタフェース境界**: モジュールは `IPerformanceModule` を通じて通信し、データは `IOutputPort` と `IInputPort` を通じて流れます。具象型がアセンブリ境界を越えて参照されることはありません [docs/CODING_GUIDELINES.md:15-29]()。
2.  **型の柔軟性**: 検証には `ParamType` 列挙体 (Float, Color, Bool) を使用しますが、内部的にはデータを `object` として渡すことで、既存ポートロジックを破壊せずに将来的な型 (Vector3 など) を追加できます [docs/TECHNICAL_DESIGN.md:83-96](), [docs/CODING_GUIDELINES.md:31-33]()。
3.  **防御的なランタイム**: 外部呼び出しとシグナル処理はすべて `try-catch` で包まれます。ノードが失敗した場合は `ParamDefaults` で定義された定数にフォールバックし、視覚パフォーマンスが決して停止しないよう保証します [docs/CODING_GUIDELINES.md:172-215]()。

### コードエンティティ関連図 (Code Entity Relationship Diagram)
この図は、概念的なレイヤーをコードベース上の具体的なクラスやインタフェースに結びつけます。

```mermaid
classDiagram
    class "GraphContext" {
        +TryConnect(string from, string to)
        +RegisterNode(NodeBase node)
        +Serialize() GraphData
    }
    class "NodeBase" {
        <<abstract>>
        +Setup(GraphContext ctx)
        +Dispose()
    }
    class "IPerformanceModule" {
        <<interface>>
        +SetParam(string name, object value)
        +Activate()
    }
    class "IOutputPort" {
        <<interface>>
        +ParamType Type
        +Subscribe(IInputPort input)
    }
    class "IInputPort" {
        <<interface>>
        +OnNext(object value)
    }

    NodeBase --> "1" GraphContext : 使用
    NodeBase "1" *-- "many" IOutputPort : 定義
    NodeBase "1" *-- "many" IInputPort : 定義
    "Rhizomode.Modules.VFXModule" ..|> IPerformanceModule : 実装
    "Rhizomode.Nodes.MultiplyNode" --|> NodeBase : 継承
    IOutputPort ..> IInputPort : データをプッシュ
```
**ソース:** [docs/TECHNICAL_DESIGN.md:99-134](), [docs/TECHNICAL_DESIGN.md:149-160](), [docs/TECHNICAL_DESIGN.md:169-190](), [docs/CODING_GUIDELINES.md:39-51](), [CLAUDE.md:41-45]()

## データフロー: リアクティブプッシュモデル (Data Flow: Reactive Push Model)

rhizomode は **R3** ライブラリを用いて、シグナルフローのリアクティブプッシュモデルを実現します [CLAUDE.md:41-41]()。

-   **静的ノード**: ほとんどのノードは入力値が変化したときにのみロジックを実行します (例: `MultiplyNode` [docs/TECHNICAL_DESIGN.md:258-261]())。
-   **駆動ノード (Driving Nodes)**: `LFO` や `Time` のような時間ベースのノードは `Observable.EveryUpdate()` を用い、毎フレーム値をグラフへ流し込みます [docs/TECHNICAL_DESIGN.md:213-217]()。
-   **購読 (Subscription) 管理**: `GraphContext` におけるすべてのエッジ接続は `IDisposable` な購読を生成します。ノードが削除されるかエッジが切断されると、これらの購読は破棄され、メモリリークを防ぎます [docs/TECHNICAL_DESIGN.md:192-209]()。

**ソース:** [docs/TECHNICAL_DESIGN.md:112-134](), [docs/TECHNICAL_DESIGN.md:211-218](), [CLAUDE.md:17-17]()

---
