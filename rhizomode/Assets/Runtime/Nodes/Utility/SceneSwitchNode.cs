#nullable enable

using System;
using R3;
using Rhizomode.Core;
using UnityEngine;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// Float入力 "Index" に応じて登録済みシーンをAdditive切り替えするノード。
    /// Rectorの BGSceneManager パターンに相当。ベースシーンは常に残り、
    /// 追加シーンだけが入れ替わる。
    /// </summary>
    public class SceneSwitchNode : NodeBase
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

        public override void Setup(GraphContext context)
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
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<SceneSwitchParams>(paramsJson);
                _currentIndex = p.currentIndex;
            }
            catch (Exception)
            {
                // 破損したJSONは無視
            }
        }

        [Serializable]
        private struct SceneSwitchParams
        {
            public int currentIndex;
        }
    }
}
