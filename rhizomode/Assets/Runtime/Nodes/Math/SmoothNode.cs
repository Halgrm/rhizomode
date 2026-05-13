#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Math
{
    /// <summary>
    /// 入力値をスムージングして出力する。Damping値で追従速度を制御。
    /// Lerp / EaseOut の2モード切替対応。
    /// </summary>
    [NodeType("Smooth", "Smooth", NodeCategory.Math)]
    public class SmoothNode : NodeBase, IInlineButton
    {
        private const float DefaultDamping = 0.1f;
        private const int ModeCount = 2;

        private readonly OutputPort<float> _valueOut;
        private float _target;
        private float _current;
        private float _damping = DefaultDamping;
        private int _modeIndex;

        private static readonly string[] ModeNames = { "Lerp", "EaseOut" };

        /// <inheritdoc />
        string IInlineButton.ButtonLabel => ModeNames[_modeIndex];

        /// <inheritdoc />
        void IInlineButton.OnButtonPressed()
        {
            _modeIndex = (_modeIndex + 1) % ModeCount;
        }

        public SmoothNode(string id) : base(id, "Smooth")
        {
            RegisterInput<float>("Input", ParamType.Float);
            RegisterInput<float>("Damping", ParamType.Float);
            _valueOut = RegisterOutput<float>("Value", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "Input")
                    .Subscribe(v => { _target = v; }));

            AddSubscription(
                context.GetInputObservable<float>(this, "Damping")
                    .Subscribe(v => { _damping = Mathf.Clamp(v, 0.001f, 1f); }));

            // 毎フレーム補間（フレームレート非依存）
            AddSubscription(
                Observable.EveryUpdate()
                    .Subscribe(_ =>
                    {
                        var prev = _current;
                        _current = _modeIndex switch
                        {
                            0 => SmoothLerp(_current, _target, _damping),
                            1 => SmoothEaseOut(_current, _target, _damping),
                            _ => _current
                        };
                        // 値が変化しない場合はEmitをスキップ（GC・下流負荷削減）
                        if (Mathf.Abs(_current - prev) > 1e-6f)
                            _valueOut.Emit(_current);
                    }));
        }

        /// <summary>
        /// Lerp方式: 一定割合で目標に接近。フレームレート非依存化。
        /// </summary>
        private static float SmoothLerp(float current, float target, float damping)
        {
            return Mathf.Lerp(current, target,
                1f - Mathf.Pow(1f - damping, UnityEngine.Time.deltaTime * 60f));
        }

        /// <summary>
        /// EaseOut方式: 残差を指数的に減衰。高dampingで素早く収束。
        /// </summary>
        private static float SmoothEaseOut(float current, float target, float damping)
        {
            var speed = damping * 10f;
            return Mathf.Lerp(current, target,
                1f - Mathf.Exp(-speed * UnityEngine.Time.deltaTime));
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new SmoothParams
            {
                damping = _damping,
                mode = _modeIndex
            });
            return data;
        }

        /// <inheritdoc />
        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<SmoothParams>(paramsJson);
                _damping = Mathf.Clamp(p.damping, 0.001f, 1f);
                _modeIndex = Mathf.Clamp(p.mode, 0, ModeCount - 1);
            }
            catch (Exception)
            {
                // 破損したJSONは無視、デフォルト値を維持
            }
        }

        [Serializable]
        private struct SmoothParams
        {
            public float damping;
            public int mode;
        }
    }
}
