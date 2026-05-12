#nullable enable

using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.Nodes.Modules
{
    /// <summary>
    /// VFXモジュールをラップするノード。ModuleDefinitionから入力ポートを動的生成する。
    /// "Active" Bool入力でVFXの表示/非表示を制御する。
    /// </summary>
    public class VFXModuleNode : ModuleNodeBase
    {
        private const string ActivePort = "Active";

        /// <param name="id">ノードID</param>
        /// <param name="definition">VFXモジュールのパラメータ定義</param>
        public VFXModuleNode(string id, ModuleDefinition definition)
            : base(id, $"VFX_{definition.moduleName}", definition)
        {
            RegisterInput<bool>(ActivePort, ParamType.Bool);
        }

        /// <inheritdoc />
        public override void Setup(GraphContext context)
        {
            base.Setup(context);

            AddSubscription(
                context.GetInputObservable<bool>(this, ActivePort)
                    .Subscribe(v => Module?.SetParam(ActivePort, v)));
        }
    }
}
