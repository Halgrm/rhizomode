#nullable enable

using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Modules;
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
        private readonly bool _activeFromDefinition;

        /// <summary>
        /// 旧 2-arg ctor (後方互換)。typeName は "VFX_{moduleName}" にフォールバック。
        /// 新規呼び出しは 3-arg 版を推奨 (M3 canonical typeName 対応)。
        /// </summary>
        public VFXModuleNode(string id, ModuleDefinition definition)
            : this(id, $"VFX_{definition.moduleName}", definition)
        {
        }

        /// <param name="id">ノードID</param>
        /// <param name="typeName">登録 typeName (orchestrator が legacy alias でも canonical を渡す)</param>
        /// <param name="definition">VFXモジュールのパラメータ定義</param>
        public VFXModuleNode(string id, string typeName, ModuleDefinition definition)
            : base(id, typeName, definition)
        {
            // L1: ModuleDefinition 側で "Active" を param/event として定義しているケースでは
            // base.RegisterPortsFromDefinition が既に port を作っているため、ここでの追加 register を skip する。
            _activeFromDefinition =
                definition.GetParam(ActivePort) != null || definition.IsEvent(ActivePort);
            if (!_activeFromDefinition)
                RegisterInput<bool>(ActivePort, ParamType.Bool);
        }

        /// <inheritdoc />
        public override void Setup(GraphState context)
        {
            base.Setup(context);

            // L1: definition 由来の "Active" port は base 側で subscribe 済 — 二重購読を避ける
            if (_activeFromDefinition) return;

            AddSubscription(
                context.GetInputObservable<bool>(this, ActivePort)
                    .Subscribe(v => Module?.SetParam(ActivePort, v)));
        }
    }
}
