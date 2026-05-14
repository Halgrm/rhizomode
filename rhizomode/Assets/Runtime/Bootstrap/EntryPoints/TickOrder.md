# EntryPoints Tick Order

Plan v5.4 §15。`RootLifetimeScope` が `EntryPointsInstaller` 経由で登録する VContainer
`ITickable` adapter の駆動順と理由。**新規 ITickable を追加したら必ず本ファイルを更新すること。**

VContainer は `RegisterEntryPoint` の登録順に `Tick()` を呼ぶ。`EntryPointsInstaller.Install`
の登録順がそのまま下記の順序になる。

| # | Adapter | 駆動対象 | 理由 |
|---|---|---|---|
| 1 | `MainThreadCommandTicker` | `MainThreadGraphCommandQueue.Tick()` | バックグラウンドスレッド (Audio 解析 / OSC・MIDI 受信等) から enqueue されたグラフコマンドを、他のどの tick よりも先にメインスレッドへ反映する。後続の adapter が「同じフレーム内で最新のグラフ状態」を前提にできる。 |
| 2 | `AudioDriverHostTickAdapter` | `AudioDriverHost.Tick()` | audio frame を解析しグラフのオーディオ駆動ノードへ流す。コマンド反映 (#1) の後に走ることで、当該フレームで追加されたばかりのノードにも値が届く。`AudioDriverBehaviour` 未配置時は登録されない。 |
| 3 | `HealthAggregatorTickAdapter` | `HealthAggregator.Tick()` | 各 system の health monitor を polling する。グラフ状態にもオーディオにも影響を与えない純粋な観測処理なので最後。`CurrentSnapshot` の alloc を避けるため adapter 内で 30 フレーム間隔に throttle (90fps で約 3Hz)。 |

## 補足

- host インスタンス (queue / AudioDriverBehaviour / HealthAggregator) の所有権は V1 時点では
  GameBootstrap 側にある。`RegisterInstance` された外部インスタンスを VContainer は Dispose
  しないため、二重 Dispose は起きない。
- V2 以降で per-bounded-context Installer が host を直接構築するようになっても、本 tick 順序は
  維持すること。順序を変える場合は理由を本ファイルに記録する。
