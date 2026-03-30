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
            _subject.Dispose();
        }
    }
}
