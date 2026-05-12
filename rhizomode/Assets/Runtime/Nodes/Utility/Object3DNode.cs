#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using UnityEngine;

namespace Rhizomode.Nodes.Utility
{
    /// <summary>
    /// VR空間に3Dオブジェクトを生成し、そのワールド座標とスケールを出力するノード。
    /// SceneObjectNodeの逆方向 — 物理的なオブジェクト操作が信号ソースになる。
    /// </summary>
    public class Object3DNode : NodeBase
    {
        private readonly string _prefabName;

        /// <summary>プレハブ名。シリアライズとファクトリ解決に使用。</summary>
        public string PrefabName => _prefabName;

        public Object3DNode(string id, string prefabName)
            : base(id, $"Object3D_{prefabName}")
        {
            _prefabName = prefabName;

            RegisterOutput<float>("PosX", ParamType.Float);
            RegisterOutput<float>("PosY", ParamType.Float);
            RegisterOutput<float>("PosZ", ParamType.Float);
            RegisterOutput<float>("Scale", ParamType.Float);
        }

        public override void Setup(GraphState context)
        {
            // Proxy注入はSetup後にGameBootstrapから行われる。
            // BindProxyObservablesで購読を開始する。
        }

        /// <summary>
        /// 3DオブジェクトのPosition/Scale Observableを購読し、出力ポートにEmitする。
        /// GameBootstrapからProxy注入時に呼ばれる。Object3DProxy型への直接依存を避けるため、
        /// Observable経由で受け取る。
        /// </summary>
        public void BindProxyObservables(
            GraphState context,
            Observable<Vector3> position,
            Observable<float> scale)
        {
            AddSubscription(
                position.Subscribe(pos =>
                {
                    context.SetOutput(this, "PosX", pos.x);
                    context.SetOutput(this, "PosY", pos.y);
                    context.SetOutput(this, "PosZ", pos.z);
                }));

            AddSubscription(
                scale.Subscribe(s => context.SetOutput(this, "Scale", s)));
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new Object3DParams
            {
                prefabName = _prefabName
            });
            return data;
        }

        public override void RestoreParamsFromJson(string paramsJson)
        {
            // ファクトリ側でprefabNameを復元してからnewするため、ここでは何もしない。
        }

        [Serializable]
        private struct Object3DParams
        {
            public string prefabName;
        }
    }
}
