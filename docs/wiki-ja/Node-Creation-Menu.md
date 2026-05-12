# ノード作成メニュー (Node Creation Menu)

<details>
<summary>関連ソースファイル</summary>

このWikiページの生成にあたって、以下のファイルがコンテキストとして使用されました：

- [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs](../../rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs)
- [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs.meta](../../rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs.meta)
- [rhizomode/Assets/Runtime/UI/USS/NodeCreationMenu.uss](../../rhizomode/Assets/Runtime/UI/USS/NodeCreationMenu.uss)
- [rhizomode/Assets/Runtime/UI/UXML/NodeCreationMenu.uxml](../../rhizomode/Assets/Runtime/UI/UXML/NodeCreationMenu.uxml)
- [rhizomode/Assets/Runtime/UI/WorldPanelHost.cs](../../rhizomode/Assets/Runtime/UI/WorldPanelHost.cs)

</details>



ノード作成メニューは、グラフへノードをスポーンするための 2 段階 VR インタフェースを提供します。Unity UI Toolkit を `WorldPanelHost` 経由でワールドスペースにレンダリングし、`NodeTypeRegistry` に基づいて選択肢を動的に構築します。

## 概要と空間配置 (Overview and Spatial Positioning)

メニューは `NodeCreationMenuController` によって管理されます [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:15-16]()。呼び出された際にユーザーの頭部の真正面に表示されるよう設計されており、360 度の VR 環境においてアクセシビリティを確保します。

### 空間構成
本メニューは可読性と操作性のために特定の空間・解像度定数を使用します：
*   **MenuSpawnDistance**: ユーザーの頭部から 0.6m [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:17]()。
*   **MenuWorldWidth/Height**: 0.25m × 0.35m [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:18-19]()。
*   **MenuTextureWidth/Height**: 400px × 560px [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:20-21]()。

`Show()` 呼び出し時、コントローラは `headPosition + headForward * MenuSpawnDistance` に基づいて位置を計算し、`Quaternion.LookRotation` でメニューをユーザー方向に回転させます [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:68-70]()。

**ソース:**
* [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:15-21]()
* [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:68-70]()

## ナビゲーションロジック (Navigation Logic)

メニューは 2 段階ナビゲーションを実装します: **カテゴリリスト** → **ノードリスト**。

### Tier 1: カテゴリリスト
`NodeTypeRegistry` からユニークなカテゴリを識別します。少なくとも 1 つの登録済みノード型を含む各カテゴリに対してボタンを生成します [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:126-132]()。
*   **カテゴリ**: Input、Math/Signal、Modules、Time、Utility [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:114-121]()。
*   **スタイリング**: 各カテゴリボタンに専用 USS クラス (例: `category-button--input`) を割り当て、ノードヘッダーの色分けと整合 [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:135-144]()。

### Tier 2: ノードリスト
カテゴリ選択時、`ShowNodesForCategory` メソッドがビューをクリアし、特定のノード種別を投入 [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:150-156]()。
*   **戻るボタン**: 「← Back」ボタンによりカテゴリリストへ戻れる [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:159-164]()。
*   **ノードボタン**: そのカテゴリ向けにレジストリから見つかった各 `NodeTypeInfo` ごとにボタンを生成 [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:167-176]()。

### 選択イベント
ノードボタンクリック時、`OnNodeSelected` メソッドがトリガーされ、ノードの `typeName` 付きで `OnNodeTypeSelected` イベントを発火し、メニューを非表示にします [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:179-183]()。

### メニューの状態遷移
タイトル: 「NodeCreationMenuController のナビゲーション状態」
```mermaid
graph TD
    "Start" --> "SetVisualActive(false)"
    "Show()" --> "ShowCategories()"
    subgraph "Tier 1: カテゴリビュー"
        "ShowCategories()" --> "Query: NodeTypeRegistry"
        "Query: NodeTypeRegistry" --> "カテゴリボタンを生成"
    end
    "カテゴリボタンを生成" -- "カテゴリクリック" --> "ShowNodesForCategory()"
    subgraph "Tier 2: ノードビュー"
        "ShowNodesForCategory()" --> "ノードボタンを生成"
        "ShowNodesForCategory()" --> "戻るボタン追加"
        "戻るボタン追加" -- "Back クリック" --> "ShowCategories()"
    end
    "ノードボタンを生成" -- "ノードクリック" --> "OnNodeSelected()"
    "OnNodeSelected()" --> "OnNodeTypeSelected 発火"
    "OnNodeSelected()" --> "Hide()"
```
**ソース:**
* [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:106-148]()
* [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:150-183]()

## 技術的実装 (Technical Implementation)

### ワールドスペース統合
コントローラは `WorldPanelHost` コンポーネントを要求します [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:14]()。初回の `Show()` 呼び出し時、`NodeCreationMenu.uxml` および `NodeCreationMenu.uss` でホストを初期化します [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:60-65]()。

`WorldPanelHost` は以下を担当します：
1.  **RenderTexture 生成**: UI のターゲットテクスチャを生成 [rhizomode/Assets/Runtime/UI/WorldPanelHost.cs:92-99]()。
2.  **メッシュ生成**: テクスチャを表示するための Quad メッシュを生成 [rhizomode/Assets/Runtime/UI/WorldPanelHost.cs:169-198]()。
3.  **マテリアル設定**: 透過対応の `Universal Render Pipeline/Unlit` シェーダーを使用 [rhizomode/Assets/Runtime/UI/WorldPanelHost.cs:146-155]()。

### データフロー
タイトル: 「メニューデータフロー: レジストリから UI へ」
```mermaid
graph LR
    subgraph "データレイヤー"
        "NodeTypeRegistry" -- "GetByCategory()" --> "NodeTypeInfo"
    end
    subgraph "コントローラレイヤー"
        "NodeCreationMenuController" -- "Initialize()" --> "NodeTypeRegistry"
        "NodeCreationMenuController" -- "CacheElements()" --> "VisualElement: category-list"
        "NodeCreationMenuController" -- "CacheElements()" --> "VisualElement: node-list"
    end
    subgraph "UI レイヤー"
        "VisualElement: category-list" -- "Add(Button)" --> "ユーザーインタフェース"
        "NodeCreationMenu.uxml" -- "UIDocument" --> "WorldPanelHost"
        "WorldPanelHost" -- "テクスチャ" --> "MeshRenderer"
    end
```

**ソース:**
* [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:47-50]()
* [rhizomode/Assets/Runtime/UI/NodeCreationMenuController.cs:97-104]()
* [rhizomode/Assets/Runtime/UI/WorldPanelHost.cs:58-66]()

## UI 構造とスタイリング (UI Structure and Styling)

### UXML 構造
UI は `NodeCreationMenu.uxml` で定義されます。`DisplayStyle.Flex` と `DisplayStyle.None` で切り替えられる 2 つのコンテナを持つシンプルな垂直レイアウトを採用 [rhizomode/Assets/Runtime/UI/UXML/NodeCreationMenu.uxml:1-7]()。
*   `menu-root`: `creation-menu` クラスを持つメインコンテナ。
*   `category-list`: Tier 1 ボタン用コンテナ。
*   `node-list`: Tier 2 ボタンと戻るボタン用コンテナ。

### USS スタイリング
スタイリングは `NodeCreationMenu.uss` で定義され、VR での可読性を高める高コントラストデザインを採用：
*   **背景**: 暗めの半透明グレー (`rgba(20, 20, 20, 0.95)`) [rhizomode/Assets/Runtime/UI/USS/NodeCreationMenu.uss:4]()。
*   **タイポグラフィ**: VR ヘッドセットの解像度を考慮した大きめのフォント (タイトル 26px、カテゴリボタン 20px) [rhizomode/Assets/Runtime/UI/USS/NodeCreationMenu.uss:11, 33]()。
*   **インタラクション**: `:hover` 状態で透明度や背景色を変化させ、VR レイインタラクターに視覚フィードバックを提供 [rhizomode/Assets/Runtime/UI/USS/NodeCreationMenu.uss:38-40, 74-76]()。

**ソース:**
* [rhizomode/Assets/Runtime/UI/UXML/NodeCreationMenu.uxml:1-7]()
* [rhizomode/Assets/Runtime/UI/USS/NodeCreationMenu.uss:1-91]()

---
