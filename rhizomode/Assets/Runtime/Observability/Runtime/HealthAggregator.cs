#nullable enable

using System;
using System.Collections.Generic;
using R3;
using Rhizomode.Observability.Contracts;

namespace Rhizomode.Observability.Runtime
{
    /// <summary>
    /// 複数の <see cref="IHealthMonitor"/> を集約し、tick ごとに状態を polling して
    /// 変化があった system の <see cref="HealthSnapshot"/> を発火する。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10: pure C# class (noEngineReferences=true な
    /// Observability.Runtime asmdef 配下)。<c>Tick()</c> は public method で、
    /// Bootstrap の <c>HealthAggregatorTickAdapter</c> (VContainer ITickable) が wrap して
    /// 呼ぶ前提。直接 MonoBehaviour にしないことで unit test 可能。
    ///
    /// Tick 順序 (Plan v5.3 line 327):
    /// 1. MainThreadGraphCommandQueueTickAdapter (graph mutation 反映)
    /// 2. AudioDriverHostTickAdapter (AudioFrame 構築 + IAudioDrivenNode 駆動)
    /// 3. HealthAggregatorTickAdapter (各 system の状態を集約 ← 本 class)
    /// </remarks>
    public sealed class HealthAggregator : IDisposable
    {
        private readonly List<IHealthMonitor> _monitors = new();
        private readonly Dictionary<string, HealthSnapshot> _last = new();
        private readonly Subject<HealthSnapshot> _onHealthChange = new();

        // Codex Phase 10 review Axis 2 fix: Tick 中の Register/Unregister による IndexOutOfRange を防ぐため
        // 反復前に snapshot を取る。allocation を抑えるため reusable buffer を使い回す。
        private IHealthMonitor[] _tickSnapshot = System.Array.Empty<IHealthMonitor>();
        private bool _isDisposed;

        /// <summary>監視対象 system の状態変化を発火する Observable。</summary>
        public Observable<HealthSnapshot> OnHealthChange => _onHealthChange;

        /// <summary>現在の全 system snapshot (read-only view)。</summary>
        public IReadOnlyDictionary<string, HealthSnapshot> CurrentSnapshots => _last;

        /// <summary>登録済の monitor 数。</summary>
        public int MonitorCount => _monitors.Count;

        /// <summary>monitor を登録する。</summary>
        public void Register(IHealthMonitor monitor)
        {
            if (_isDisposed) return;
            if (monitor == null) return;
            _monitors.Add(monitor);
        }

        /// <summary>monitor を登録解除する。</summary>
        public bool Unregister(IHealthMonitor monitor)
        {
            if (_isDisposed) return false;
            return _monitors.Remove(monitor);
        }

        /// <summary>
        /// 全 monitor を polling し、状態変化があった system について OnHealthChange を発火する。
        /// </summary>
        /// <remarks>
        /// Bootstrap の HealthAggregatorTickAdapter が ITickable.Tick() で本 method を呼ぶ。
        /// monitor の例外は捕捉して該当 system を Failed として記録 (fail-open で aggregator 自体は止めない)。
        /// </remarks>
        public void Tick()
        {
            if (_isDisposed) return;

            // Codex Phase 10 Axis 2 fix: snapshot iteration で Tick 中の Register/Unregister 由来の
            // List 変更を安全化する。本 tick では snapshot 時点のリストのみが対象、追加/削除は次 tick に反映。
            var count = _monitors.Count;
            if (_tickSnapshot.Length < count)
                _tickSnapshot = new IHealthMonitor[count];
            for (int i = 0; i < count; i++)
                _tickSnapshot[i] = _monitors[i];

            for (int i = 0; i < count; i++)
            {
                var monitor = _tickSnapshot[i];
                HealthSnapshot snapshot;
                try
                {
                    snapshot = monitor.CurrentSnapshot();
                }
                catch (Exception e)
                {
                    snapshot = new HealthSnapshot(
                        monitor.SystemId,
                        HealthStatus.Failed,
                        $"Monitor threw: {e.Message}");
                }

                if (!_last.TryGetValue(snapshot.SystemId, out var prev) || !Equals(prev, snapshot))
                {
                    _last[snapshot.SystemId] = snapshot;
                    _onHealthChange.OnNext(snapshot);
                }

                // 本 tick 内で snapshot buffer の slot 解放 (referenced object retention 防止)
                _tickSnapshot[i] = null!;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _onHealthChange.Dispose();
            _monitors.Clear();
            _last.Clear();
        }
    }
}
