#nullable enable

using System;
using R3;

namespace Rhizomode.Core
{
    /// <summary>
    /// 出力ポートの具象実装。R3 Subject経由で値を発行し、入力ポートへ伝播する。
    /// </summary>
    public class OutputPort<T> : IOutputPort, IDisposable
    {
        private readonly Subject<T> _subject = new();
        private bool _disposed;

        public ParamType Type { get; }

        /// <summary>
        /// ノード内部でObservableチェーンを構築するためのストリーム。
        /// </summary>
        public Observable<T> Observable => _subject;

        public OutputPort(ParamType type)
        {
            Type = type;
        }

        /// <summary>
        /// 値を発行する。接続された全入力ポートに伝播する。
        /// </summary>
        public void Emit(T value)
        {
            // Dispose後の発行はクラッシュ防止のため無視する
            if (_disposed) return;
            _subject.OnNext(value);
        }

        public IDisposable Subscribe(IInputPort input)
        {
            return _subject.Subscribe(v => input.OnNext(v!));
        }

        public void Dispose()
        {
            // 多重Dispose防止（エッジ切断とノード削除が同時に走る場合がある）
            if (_disposed) return;
            _disposed = true;
            _subject.Dispose();
        }
    }
}
