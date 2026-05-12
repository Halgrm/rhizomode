#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using UnityEngine;

namespace Rhizomode.Nodes.Math
{
    /// <summary>
    /// 入力値を[InMin,InMax]から[OutMin,OutMax]へ線形リマップする。
    /// クランプなし（範囲外の外挿を許可）。
    /// </summary>
    public class RemapNode : NodeBase
    {
        private readonly OutputPort<float> _resultOut;
        private float _input;
        private float _inMin;
        private float _inMax = 1f;
        private float _outMin;
        private float _outMax = 1f;

        public RemapNode(string id) : base(id, "Remap")
        {
            RegisterInput<float>("Input", ParamType.Float);
            RegisterInput<float>("InMin", ParamType.Float);
            RegisterInput<float>("InMax", ParamType.Float);
            RegisterInput<float>("OutMin", ParamType.Float);
            RegisterInput<float>("OutMax", ParamType.Float);
            _resultOut = RegisterOutput<float>("Result", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<float>(this, "Input")
                    .Subscribe(v =>
                    {
                        _input = v;
                        _resultOut.Emit(Compute());
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "InMin")
                    .Subscribe(v =>
                    {
                        _inMin = v;
                        _resultOut.Emit(Compute());
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "InMax")
                    .Subscribe(v =>
                    {
                        _inMax = v;
                        _resultOut.Emit(Compute());
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "OutMin")
                    .Subscribe(v =>
                    {
                        _outMin = v;
                        _resultOut.Emit(Compute());
                    }));

            AddSubscription(
                context.GetInputObservable<float>(this, "OutMax")
                    .Subscribe(v =>
                    {
                        _outMax = v;
                        _resultOut.Emit(Compute());
                    }));
        }

        /// <summary>
        /// 線形リマップを計算する。InMin == InMaxの場合はOutMinを返す（ゼロ除算防止）。
        /// </summary>
        private float Compute()
        {
            float range = _inMax - _inMin;
            if (Mathf.Abs(range) < Mathf.Epsilon) return _outMin;
            return _outMin + (_input - _inMin) / range * (_outMax - _outMin);
        }
    }
}
