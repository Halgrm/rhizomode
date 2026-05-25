#nullable enable

using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Model
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
        public PortUnit unit;

        public PortDefinition(string name, ParamType type, PortDirection direction)
            : this(name, type, direction, PortUnit.None)
        {
        }

        public PortDefinition(string name, ParamType type, PortDirection direction, PortUnit unit)
        {
            this.name = name;
            this.type = type;
            this.direction = direction;
            this.unit = unit;
        }
    }
}
