#nullable enable

using R3;
using Rhizomode.Graph.Model;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.SharedKernel;
using UnityEngine;

namespace Rhizomode.Nodes.Scene
{
    /// <summary>
    /// Exposes graph-controlled glitch post-effect amount for mirror output.
    /// </summary>
    [NodeType("Glitch", "Glitch", NodeCategory.Utility)]
    public sealed class GlitchNode : NodeBase, INodeParamAccessor
    {
        private const float DefaultAmount = 1.0f;

        private bool _active;
        private float _amount = DefaultAmount;

        /// <summary>Effective glitch amount after Active gate and clamping.</summary>
        public float CurrentAmount => _active ? SanitizeAmount(_amount) : 0f;

        public GlitchNode(string id) : base(id, "Glitch")
        {
            RegisterInput<bool>("Active", ParamType.Bool);
            RegisterInput<float>("Amount", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<bool>(this, "Active")
                    .Subscribe(value => _active = value));

            AddSubscription(
                context.GetInputObservable<float>(this, "Amount")
                    .Subscribe(value => _amount = value));
        }

        bool INodeParamAccessor.TrySetParam(string paramName, ParamValue value)
        {
            if (paramName != "Amount" || value.Type != ParamType.Float)
                return false;

            _amount = SanitizeAmount(value.AsFloat);
            return true;
        }

        bool INodeParamAccessor.TryGetParam(string paramName, out ParamValue value)
        {
            if (paramName == "Amount")
            {
                value = ParamValue.Float(_amount);
                return true;
            }

            value = default;
            return false;
        }

        private static float SanitizeAmount(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
                return 0f;
            return Mathf.Clamp01(amount);
        }
    }
}
