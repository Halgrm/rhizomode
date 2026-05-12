#nullable enable

namespace Rhizomode.Core
{
    /// <summary>
    /// ポートの定義情報。シリアライズおよびUI表示用。
    /// </summary>
    [System.Serializable]
    public struct PortDefinition
    {
        public string name;
        public ParamType type;
        public PortDirection direction;

        public PortDefinition(string name, ParamType type, PortDirection direction)
        {
            this.name = name;
            this.type = type;
            this.direction = direction;
        }
    }
}
