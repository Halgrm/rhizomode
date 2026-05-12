#nullable enable

using System;
using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// Bool入力 "Trigger" の立ち上がりで、固定インデックスのシーンをAdditiveロードするノード。
    /// シーンごとに1ノードとしてメニューに登録される（例: "Dark", "White", "Nature"）。
    /// </summary>
    public class SceneTriggerNode : NodeBase
    {
        private readonly int _sceneIndex;
        private ISceneLoader? _loader;

        /// <summary>
        /// 実行時にGameBootstrapから注入されるシーンローダー。
        /// </summary>
        public ISceneLoader? Loader
        {
            get => _loader;
            set => _loader = value;
        }

        /// <summary>対象シーンインデックス。</summary>
        public int SceneIndex => _sceneIndex;

        public SceneTriggerNode(string id, string nodeType, int sceneIndex)
            : base(id, nodeType)
        {
            _sceneIndex = sceneIndex;
            RegisterInput<bool>("Trigger", ParamType.Bool);
        }

        public override void Setup(GraphContext context)
        {
            AddSubscription(
                context.GetInputObservable<bool>(this, "Trigger")
                    .Where(v => v)
                    .Subscribe(_ =>
                    {
                        try
                        {
                            _loader?.LoadScene(_sceneIndex);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[SceneTriggerNode] LoadScene({_sceneIndex}) failed: {e.Message}");
                        }
                    }));
        }
    }
}
