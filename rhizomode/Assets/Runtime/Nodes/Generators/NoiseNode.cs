#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using UnityEngine;

namespace Rhizomode.Nodes.Generators
{
    /// <summary>
    /// Perlinノイズによる連続的なランダム値を生成するノード。
    /// インスタンスごとに異なるシードを使用し、複数ノードで異なる出力を保証。
    /// </summary>
    public class NoiseNode : NodeBase
    {
        private const float DefaultSpeed = 1f;
        private const float DefaultAmplitude = 1f;

        private readonly OutputPort<float> _valueOut;
        private float _seed;
        private float _offset;
        private float _speed = DefaultSpeed;
        private float _amplitude = DefaultAmplitude;

        public NoiseNode(string id) : base(id, "Noise")
        {
            RegisterInput<float>("Speed", ParamType.Float);
            RegisterInput<float>("Amplitude", ParamType.Float);
            RegisterInput<float>("Seed", ParamType.Float);
            _valueOut = RegisterOutput<float>("Value", ParamType.Float);

            // 複数ノードで異なるノイズパターンを生成するためのシード
            _seed = UnityEngine.Random.Range(0f, 10000f);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "Speed")
                    .Subscribe(v => _speed = Mathf.Max(v, 0f)));

            AddSubscription(
                context.GetInputObservable<float>(this, "Amplitude")
                    .Subscribe(v => _amplitude = Mathf.Clamp01(v)));

            // Seed入力: 外部接続時は上書き、未接続時はコンストラクタで設定したランダム値を使用
            AddSubscription(
                context.GetInputObservable<float>(this, "Seed")
                    .Subscribe(v => _seed = v));

            AddSubscription(
                Observable.EveryUpdate()
                    .Subscribe(_ =>
                    {
                        _offset += _speed * UnityEngine.Time.deltaTime;
                        var raw = Mathf.PerlinNoise(_offset, _seed);
                        _valueOut.Emit(raw * _amplitude);
                    }));
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new NoiseParams
            {
                seed = _seed,
                speed = _speed,
                amplitude = _amplitude
            });
            return data;
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<NoiseParams>(paramsJson);
                _seed = p.seed;
                _speed = Mathf.Max(p.speed, 0f);
                _amplitude = Mathf.Clamp01(p.amplitude);
            }
            catch (Exception)
            {
                // 破損したJSONは無視、デフォルト値を維持
            }
        }

        [Serializable]
        private struct NoiseParams
        {
            public float seed;
            public float speed;
            public float amplitude;
        }
    }
}
