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
        // cue ロードで復元したい env index。RestoreParamsFromJson で saved 値を受け取り、
        // Setup で additive ロードを再発火するために保持する (fresh spawn 時は -1 のまま)。
        private int _restoredIndex = -1;

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

            // cue ロード復元: 保存済 env index を additive で再ロードする。
            // Index 入力の再 emit はグラフロード時の reactive 順序に依存して取りこぼし得る
            // (env が出ない原因) ため、ここで明示的に駆動する。Loader は
            // SceneLoaderLifecycleProcessor.BeforeSetup で Setup 前に注入済。
            // AdditiveSceneLoader.LoadScene は同名 no-op なので Index 入力経路と二重実行に
            // なっても安全。fresh spawn 時は _restoredIndex = -1 で何もしない。
            if (_restoredIndex >= 0 && _loader != null)
            {
                try
                {
                    _loader.LoadScene(_restoredIndex);
                    _currentIndex = _restoredIndex;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SceneSwitchNode] Restore LoadScene({_restoredIndex}) failed: {e.Message}");
                }
            }
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
            // _currentIndex は -1 (= 未ロード) に保ち、保存済 index は _restoredIndex に退避して
            // Setup で additive ロードを再発火する (env を確実に復元する)。
            _currentIndex = -1;
            _restoredIndex = -1;
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<SceneSwitchParams>(paramsJson);
                _restoredIndex = p.currentIndex;
            }
            catch (Exception)
            {
                // 破損 JSON は無視 (env 復元はスキップ、グラフロードは続行)。
            }
        }

        [Serializable]
        private struct SceneSwitchParams
        {
            public int currentIndex;
        }
    }
}
