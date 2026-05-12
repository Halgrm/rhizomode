#nullable enable

using System;
using R3;
using UnityEngine;

namespace Rhizomode.Core
{
    /// <summary>
    /// 入力ポートの具象実装。OnNextで受けた値をR3 Subject経由で下流に流す。
    /// </summary>
    public class InputPort<T> : IInputPort, IDisposable
    {
        private readonly Subject<T> _subject = new();
        private bool _disposed;

        public ParamType Type { get; }

        /// <summary>
        /// ノードのSetup()内でObservableチェーンを構築するためのストリーム。
        /// </summary>
        public Observable<T> Observable => _subject;

        public InputPort(ParamType type)
        {
            Type = type;
        }

        public void OnNext(object value)
        {
            // Dispose後の受信はクラッシュ防止のため無視する
            if (_disposed) return;
            try
            {
                _subject.OnNext((T)value);
            }
            catch (InvalidCastException)
            {
                Debug.LogWarning($"[InputPort] Type mismatch: expected {typeof(T).Name}, got {value?.GetType().Name}");
            }
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
