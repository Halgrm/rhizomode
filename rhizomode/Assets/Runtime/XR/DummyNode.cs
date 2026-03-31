#nullable enable

using Rhizomode.Core;

namespace Rhizomode.XR
{
    /// <summary>
    /// Week 2テスト用のダミーノード。実ノード実装(Week 4)まで使用する。
    /// 指定されたノードタイプ名でポート構成を自動設定する。
    /// </summary>
    internal class DummyNode : NodeBase
    {
        public DummyNode(string id, string nodeType) : base(id, nodeType)
        {
            SetupPortsForType(nodeType);
        }

        public override void Setup(GraphContext context)
        {
            // ダミー: 信号フローなし
        }

        private void SetupPortsForType(string nodeType)
        {
            switch (nodeType)
            {
                case "ConstFloat":
                    RegisterOutput<float>("Value", ParamType.Float);
                    break;
                case "AudioTrigger":
                    RegisterInput<float>("FreqMin", ParamType.Float);
                    RegisterInput<float>("FreqMax", ParamType.Float);
                    RegisterInput<float>("Threshold", ParamType.Float);
                    RegisterOutput<float>("Level", ParamType.Float);
                    RegisterOutput<bool>("Trigger", ParamType.Bool);
                    break;
                case "BeatDetector":
                    RegisterInput<bool>("Trigger", ParamType.Bool);
                    RegisterOutput<float>("BPM", ParamType.Float);
                    RegisterOutput<float>("Phase", ParamType.Float);
                    RegisterOutput<bool>("Beat", ParamType.Bool);
                    break;
                case "TapTempo":
                    RegisterOutput<float>("BPM", ParamType.Float);
                    RegisterOutput<float>("Phase", ParamType.Float);
                    RegisterOutput<bool>("Beat", ParamType.Bool);
                    break;
                case "Multiply":
                    RegisterInput<float>("A", ParamType.Float);
                    RegisterInput<float>("B", ParamType.Float);
                    RegisterOutput<float>("Result", ParamType.Float);
                    break;
                case "Smooth":
                    RegisterInput<float>("Input", ParamType.Float);
                    RegisterInput<float>("Damping", ParamType.Float);
                    RegisterOutput<float>("Value", ParamType.Float);
                    break;
                case "Time":
                    RegisterOutput<float>("Time", ParamType.Float);
                    break;
                case "Threshold":
                    RegisterInput<float>("Value", ParamType.Float);
                    RegisterInput<float>("Threshold", ParamType.Float);
                    RegisterOutput<bool>("Gate", ParamType.Bool);
                    break;
                case "Toggle":
                    RegisterInput<bool>("Trigger", ParamType.Bool);
                    RegisterOutput<bool>("State", ParamType.Bool);
                    break;
                default:
                    RegisterInput<float>("Input", ParamType.Float);
                    RegisterOutput<float>("Output", ParamType.Float);
                    break;
            }
        }
    }
}
