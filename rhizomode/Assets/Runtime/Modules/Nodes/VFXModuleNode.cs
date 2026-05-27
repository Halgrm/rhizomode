#nullable enable

using System.Collections.Generic;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Modules;
using UnityEngine;
using UnityEngine.VFX;

namespace Rhizomode.Nodes.Modules
{
    /// <summary>
    /// VFXモジュールをラップするノード。ModuleDefinition の登録パラメータに加え、
    /// VFX Graph アセットの Exposed プロパティを実行時に自動列挙してポート化する。
    /// "Active" Bool入力でVFXの表示/非表示を制御する。
    /// </summary>
    public class VFXModuleNode : ModuleNodeBase
    {
        private const string ActivePort = "Active";
        private readonly bool _activeFromDefinition;

        /// <summary>
        /// ModuleDefinition には未登録だが VFX アセットが公開しているため自動でポート化した
        /// パラメータ。<see cref="Setup"/> で個別に購読してモジュールへ転送する。
        /// </summary>
        private readonly List<(string name, ParamType type)> _autoParams = new();

        /// <summary>自動ポート名の集合。<see cref="ShouldAutoSpawnInputSource"/> の判定に使う。</summary>
        private readonly HashSet<string> _autoParamNames = new();

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
            // L1: ModuleDefinition.parameters 側で "Active" を Bool として定義しているケースでは
            // base.RegisterPortsFromDefinition が既に port + subscribe を済ませているため、ここで skip する。
            //
            // Codex re-review fix (FAIL 5): events に "Active" がある場合は base の subscribe が
            // 「true で SetParam("Active", true)」しか飛ばないため、false で VFX を Deactivate できない。
            // events 経由の Active は VFX セマンティクスに合わないため、parameters 側のみを skip 条件にする。
            _activeFromDefinition = definition.GetParam(ActivePort) != null;
            if (!_activeFromDefinition)
                RegisterInput<bool>(ActivePort, ParamType.Bool);

            DiscoverVfxProperties(definition);
        }

        /// <summary>
        /// definition.prefab の VisualEffect から VFX Graph の Exposed プロパティを列挙し、
        /// ModuleDefinition に未登録のものを入力ポートとして追加する。
        /// </summary>
        /// <remarks>
        /// ModuleDefinition のキュレーションを経ずに、VFX アセットが公開するプロパティを
        /// そのままノードへ出すための処理。Exposed プロパティは全て対象 (アンダースコア始まりも含む)。
        /// prefab / VisualEffect / VFX アセットが無い場合は何もしない (defensive)。
        /// </remarks>
        private void DiscoverVfxProperties(ModuleDefinition definition)
        {
            var prefab = definition.prefab;
            if (prefab == null) return;

            var vfx = prefab.GetComponent<VisualEffect>();
            var asset = vfx != null ? vfx.visualEffectAsset : null;
            if (asset == null) return;

            var props = new List<VFXExposedProperty>();
            asset.GetExposedProperties(props);

            foreach (var prop in props)
                RegisterAutoPortsFor(prop);
        }

        /// <summary>
        /// VFX の Exposed プロパティ 1 つを型に応じた入力ポートへ写像する。
        /// float/int → Float、Color/Vector4 → Color、bool → Bool。
        /// Vector3 は rhizomode に該当ポート型が無いため XYZ 3 つの Float ポートに分解し、
        /// VFXModule 側が " X"/" Y"/" Z" サフィックスを束ねて SetVector3 する。
        /// ParamType に写せない型は無視する。
        /// </summary>
        private void RegisterAutoPortsFor(VFXExposedProperty prop)
        {
            if (string.IsNullOrEmpty(prop.name)) return;

            var t = prop.type;
            if (t == typeof(float) || t == typeof(int))
                AddAutoPort(prop.name, ParamType.Float);
            else if (t == typeof(Color) || t == typeof(Vector4))
                AddAutoPort(prop.name, ParamType.Color);
            else if (t == typeof(bool))
                AddAutoPort(prop.name, ParamType.Bool);
            else if (t == typeof(Vector3))
            {
                AddAutoPort(prop.name + " X", ParamType.Float);
                AddAutoPort(prop.name + " Y", ParamType.Float);
                AddAutoPort(prop.name + " Z", ParamType.Float);
            }
        }

        /// <summary>
        /// 自動ポートを 1 つ登録する。既存ポート (ModuleDefinition / events / "Active" /
        /// 既に追加した自動ポート) と名前が衝突する場合は何もしない。
        /// </summary>
        private void AddAutoPort(string name, ParamType type)
        {
            if (InputPorts.ContainsKey(name)) return;

            switch (type)
            {
                case ParamType.Float: RegisterInput<float>(name, type); break;
                case ParamType.Color: RegisterInput<Color>(name, type); break;
                case ParamType.Bool: RegisterInput<bool>(name, type); break;
            }
            _autoParams.Add((name, type));
            _autoParamNames.Add(name);
        }

        /// <summary>
        /// 自動検出ポート (ModuleDefinition 未登録の VFX 公開プロパティ) では Const を自動 spawn しない。
        /// </summary>
        /// <remarks>
        /// 自動 spawn される ConstFloat の汎用既定値 (0.5) が VFX 作者の設定値を上書きしてしまう。
        /// 例: CubeLine の Vector3 プロパティ Angle=(0,1,0) が XYZ=各 0.5 で (0.5,0.5,0.5) に潰れ、
        /// 回転が壊れる。未接続のままにすれば VFX 側の値が保たれ、ユーザが任意に source を繋いだ
        /// 時だけ駆動する。ModuleDefinition 登録パラメータと "Active" は従来どおり自動 spawn する。
        /// </remarks>
        public override bool ShouldAutoSpawnInputSource(string portName)
            => !_autoParamNames.Contains(portName);

        /// <inheritdoc />
        public override void Setup(GraphState context)
        {
            base.Setup(context);

            // 自動ポートの購読は Active の早期 return より前に行う (definition 由来の Active でも貼る)
            SubscribeAutoParams(context);

            // L1: definition 由来の "Active" port は base 側で subscribe 済 — 二重購読を避ける
            if (_activeFromDefinition) return;

            AddSubscription(
                context.GetInputObservable<bool>(this, ActivePort)
                    .Subscribe(v => Module?.SetParam(ActivePort, v)));
        }

        /// <summary>自動ポート化したパラメータの入力 Observable を購読し、モジュールへ値を転送する。</summary>
        private void SubscribeAutoParams(GraphState context)
        {
            foreach (var (name, type) in _autoParams)
            {
                var paramName = name;
                switch (type)
                {
                    case ParamType.Float:
                        AddSubscription(context.GetInputObservable<float>(this, paramName)
                            .Subscribe(v => Module?.SetParam(paramName, v)));
                        break;
                    case ParamType.Color:
                        AddSubscription(context.GetInputObservable<Color>(this, paramName)
                            .Subscribe(v => Module?.SetParam(paramName, v)));
                        break;
                    case ParamType.Bool:
                        AddSubscription(context.GetInputObservable<bool>(this, paramName)
                            .Subscribe(v => Module?.SetParam(paramName, v)));
                        break;
                }
            }
        }
    }
}
