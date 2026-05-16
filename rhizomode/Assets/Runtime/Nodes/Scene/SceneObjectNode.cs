#nullable enable

using System;
using R3;
using Rhizomode.SharedKernel;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using UnityEngine;

namespace Rhizomode.Nodes.Scene
{
    /// <summary>
    /// シーン上のGameObjectのTransformプロパティをノードグラフから制御する。
    /// Rector方式: AddComponentするだけでノードが生成され、パラメータを接続できる。
    /// </summary>
    public class SceneObjectNode : NodeBase
    {
        private Transform? _target;
        private readonly string _objectName;
        private readonly bool _exposePosition;
        private readonly bool _exposeRotation;
        private readonly bool _exposeScale;

        // Rector方式: Size(uniform) × Scale(per-axis) で最終スケールを決定
        private float _size = 1f;
        private Vector3 _baseScale;

        // クロージャ競合防止: 各軸の値を個別フィールドで保持
        private float _posX, _posY, _posZ;
        private float _rotX, _rotY, _rotZ;

        /// <summary>表示名（GameObject名）。</summary>
        public string ObjectName => _objectName;

        public SceneObjectNode(string id, string objectName,
            bool exposePosition, bool exposeRotation, bool exposeScale)
            : base(id, "SceneObject")
        {
            _objectName = objectName;
            _exposePosition = exposePosition;
            _exposeRotation = exposeRotation;
            _exposeScale = exposeScale;

            if (_exposeScale)
            {
                RegisterInput<float>("Size", ParamType.Float);
                RegisterInput<float>("ScaleX", ParamType.Float);
                RegisterInput<float>("ScaleY", ParamType.Float);
                RegisterInput<float>("ScaleZ", ParamType.Float);
            }

            if (_exposePosition)
            {
                RegisterInput<float>("PosX", ParamType.Float);
                RegisterInput<float>("PosY", ParamType.Float);
                RegisterInput<float>("PosZ", ParamType.Float);
            }

            if (_exposeRotation)
            {
                RegisterInput<float>("RotX", ParamType.Float);
                RegisterInput<float>("RotY", ParamType.Float);
                RegisterInput<float>("RotZ", ParamType.Float);
            }

            RegisterInput<bool>("Active", ParamType.Bool);
        }

        /// <summary>
        /// 制御対象のTransformを設定する。初期値をキャプチャする。
        /// </summary>
        public void SetTarget(Transform target)
        {
            _target = target;
            _baseScale = target.localScale;
        }

        public override void Setup(GraphState context)
        {
            if (_target == null) return;

            var initialPos = _target.localPosition;
            var initialRot = _target.localEulerAngles;

            if (_exposeScale)
            {
                // Rector方式: Size × Scale per-axis。初期値 Size=1, Scale=元のスケール
                SeedInput(context, "Size", 1f);
                SeedInput(context, "ScaleX", _baseScale.x);
                SeedInput(context, "ScaleY", _baseScale.y);
                SeedInput(context, "ScaleZ", _baseScale.z);

                AddSubscription(
                    context.GetInputObservable<float>(this, "Size")
                        .Subscribe(v =>
                        {
                            _size = Mathf.Max(0.01f, v);
                            ApplyScale();
                        }));
                AddSubscription(
                    context.GetInputObservable<float>(this, "ScaleX")
                        .Subscribe(v => { _baseScale.x = v; ApplyScale(); }));
                AddSubscription(
                    context.GetInputObservable<float>(this, "ScaleY")
                        .Subscribe(v => { _baseScale.y = v; ApplyScale(); }));
                AddSubscription(
                    context.GetInputObservable<float>(this, "ScaleZ")
                        .Subscribe(v => { _baseScale.z = v; ApplyScale(); }));
            }

            if (_exposePosition)
            {
                _posX = initialPos.x;
                _posY = initialPos.y;
                _posZ = initialPos.z;
                SeedInput(context, "PosX", _posX);
                SeedInput(context, "PosY", _posY);
                SeedInput(context, "PosZ", _posZ);

                AddSubscription(
                    context.GetInputObservable<float>(this, "PosX")
                        .Subscribe(v => { _posX = v; ApplyPosition(); }));
                AddSubscription(
                    context.GetInputObservable<float>(this, "PosY")
                        .Subscribe(v => { _posY = v; ApplyPosition(); }));
                AddSubscription(
                    context.GetInputObservable<float>(this, "PosZ")
                        .Subscribe(v => { _posZ = v; ApplyPosition(); }));
            }

            if (_exposeRotation)
            {
                _rotX = initialRot.x;
                _rotY = initialRot.y;
                _rotZ = initialRot.z;
                SeedInput(context, "RotX", _rotX);
                SeedInput(context, "RotY", _rotY);
                SeedInput(context, "RotZ", _rotZ);

                AddSubscription(
                    context.GetInputObservable<float>(this, "RotX")
                        .Subscribe(v => { _rotX = v; ApplyRotation(); }));
                AddSubscription(
                    context.GetInputObservable<float>(this, "RotY")
                        .Subscribe(v => { _rotY = v; ApplyRotation(); }));
                AddSubscription(
                    context.GetInputObservable<float>(this, "RotZ")
                        .Subscribe(v => { _rotZ = v; ApplyRotation(); }));
            }

            // Active: 初期値true
            SeedInput(context, "Active", true);
            AddSubscription(
                context.GetInputObservable<bool>(this, "Active")
                    .Subscribe(v => { if (_target != null) _target.gameObject.SetActive(v); }));
        }

        private void ApplyPosition()
        {
            if (_target != null)
                _target.localPosition = new Vector3(_posX, _posY, _posZ);
        }

        private void ApplyRotation()
        {
            if (_target != null)
                _target.localRotation = Quaternion.Euler(_rotX, _rotY, _rotZ);
        }

        private void ApplyScale()
        {
            if (_target == null) return;
            _target.localScale = new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(_baseScale.x)) * _size,
                Mathf.Max(0.01f, Mathf.Abs(_baseScale.y)) * _size,
                Mathf.Max(0.01f, Mathf.Abs(_baseScale.z)) * _size);
        }

        /// <summary>
        /// 接続がないとき初期値を流すために、InputPortにOnNextで初期値を注入する。
        /// </summary>
        private void SeedInput<T>(GraphState context, string portName, T value)
        {
            var port = GetInputPort(portName);
            port?.OnNext(value!);
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new PersistedParams
            {
                objectName = _objectName,
                exposePosition = _exposePosition,
                exposeRotation = _exposeRotation,
                exposeScale = _exposeScale
            });
            return data;
        }

        /// <summary>
        /// JSON 復元処理。port 構成は constructor で確定するため、factory 側
        /// (<c>SceneObjectRegistrationService.RegisterTypeAndFactory</c>) で paramsJson を pre-parse して
        /// 正しい引数で <c>new SceneObjectNode</c> する責務を持つ (N1 fix)。
        /// 本メソッドは追加の internal state 復元のための hook (現状は no-op)。
        /// </summary>
        public override void RestoreParamsFromJson(string paramsJson)
        {
            // ports / objectName / expose flags はすべて constructor で確定済 (factory が pre-parse して渡す)。
            // 追加の internal state 復元が必要になったらここに足す (例: _size の persisted 初期値 等)。
        }

        /// <summary>
        /// SceneObjectNode の paramsJson 表現。factory 側で pre-parse するため公開する (N1 fix)。
        /// </summary>
        [Serializable]
        public struct PersistedParams
        {
            public string objectName;
            public bool exposePosition;
            public bool exposeRotation;
            public bool exposeScale;
        }
    }
}
