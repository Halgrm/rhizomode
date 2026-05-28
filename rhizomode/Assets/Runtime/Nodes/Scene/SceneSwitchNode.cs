#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using Rhizomode.Scene.Contracts;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
namespace Rhizomode.Nodes.Scene
{
    /// <summary>
    /// Float入力 "Index" に応じて登録済みシーンをAdditive切り替えするノード。
    /// Rectorの BGSceneManager パターンに相当。ベースシーンは常に残り、
    /// 追加シーンだけが入れ替わる。
    /// </summary>
    [NodeType("SceneSwitch", "Scene Switch", NodeCategory.Scene)]
    public class SceneSwitchNode : NodeBase, ISceneLoaderConsumer
    {
        private ISceneLoader? _loader;
        private int _currentIndex = -1;

        /// <summary>
        /// 実行時にGameBootstrapから注入されるシーンローダー。
        /// </summary>
        public ISceneLoader? Loader
        {
            get => _loader;
            set => _loader = value;
        }

        public SceneSwitchNode(string id) : base(id, "SceneSwitch")
        {
            RegisterInput<float>("Index", ParamType.Float);
            RegisterInput<bool>("Unload", ParamType.Bool);
        }

        public override void Setup(GraphState context)
        {
            // Index入力: 値が変わったらシーン切り替え
            AddSubscription(
                context.GetInputObservable<float>(this, "Index")
                    .Subscribe(v =>
                    {
                        var index = Mathf.RoundToInt(v);
                        if (index == _currentIndex) return;
                        _currentIndex = index;

                        try
                        {
                            _loader?.LoadScene(index);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[SceneSwitchNode] LoadScene failed: {e.Message}");
                        }
                    }));

            // Unload入力: trueでアクティブシーンをアンロード
            AddSubscription(
                context.GetInputObservable<bool>(this, "Unload")
                    .Where(v => v)
                    .Subscribe(_ =>
                    {
                        try
                        {
                            _loader?.UnloadCurrentScene();
                            _currentIndex = -1;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[SceneSwitchNode] UnloadScene failed: {e.Message}");
                        }
                    }));
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new SceneSwitchParams
            {
                currentIndex = _currentIndex
            });
            return data;
        }

        public override void RestoreParamsFromJson(string paramsJson)
        {
            // cue ロード直後は additive env シーンが 1 つもロードされていない (base のみ)。
            // 保存済み currentIndex をそのまま復元すると、Index 入力が同じ値を再 emit しても
            // 「変化なし」と判定されて env が additive ロードされず、環境が出ない。
            // _currentIndex は常に -1 (= 未ロード) にリセットし、Setup 後の Index 入力 emit で
            // env を additive ロードし直す。AdditiveSceneLoader.LoadScene は同名なら no-op の
            // ため二重ロードにはならない。
            _currentIndex = -1;
        }

        [Serializable]
        private struct SceneSwitchParams
        {
            public int currentIndex;
        }
    }
}
