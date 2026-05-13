#nullable enable

using R3;
using UnityEngine;

using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.NodeCatalog.Runtime;
using Rhizomode.Input.Contracts;

namespace Rhizomode.Modules
{
    /// <summary>
    /// 3Dオブジェクトプレハブにアタッチされるコンポーネント。
    /// VRグラブで移動・スケール変更された座標をReactivePropertyで公開する。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Object3DProxy : MonoBehaviour
    {
        private readonly ReactiveProperty<Vector3> _position = new(Vector3.zero);
        private readonly ReactiveProperty<float> _scale = new(1f);

        /// <summary>ワールド座標。グラブ移動で更新される。</summary>
        public ReadOnlyReactiveProperty<Vector3> Position => _position;

        /// <summary>ユニフォームスケール。グラブ中のスティック操作で更新される。</summary>
        public ReadOnlyReactiveProperty<float> Scale => _scale;

        /// <summary>紐づくノードID。GameBootstrapが設定する。</summary>
        public string? NodeId { get; set; }

        private void Update()
        {
            var pos = transform.position;
            if (pos != _position.Value)
                _position.Value = pos;

            var s = transform.localScale.x;
            if (!Mathf.Approximately(s, _scale.Value))
                _scale.Value = s;
        }

        /// <summary>スケールを設定する。Object3DGrabHandlerから呼ばれる。</summary>
        public void SetScale(float uniformScale)
        {
            uniformScale = Mathf.Max(0.01f, uniformScale);
            transform.localScale = Vector3.one * uniformScale;
            _scale.Value = uniformScale;
        }

        private void OnDestroy()
        {
            _position.Dispose();
            _scale.Dispose();
        }
    }
}
