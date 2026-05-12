#nullable enable

using UnityEngine;

namespace Rhizomode.UI
{
    /// <summary>
    /// 全ハンドラが共有するレイキャスト結果。毎フレーム1回だけPhysics.Raycastを実行する。
    /// </summary>
    [DefaultExecutionOrder(-10)] // 他のUpdateより先に実行
    public class SharedRaycastService : MonoBehaviour
    {
        [SerializeField, Range(1f, 20f), Tooltip("レイキャストの最大距離（メートル）")]
        private float maxRayDistance = 5f;

        [SerializeField, Tooltip("レイキャスト対象レイヤー（未設定時は全レイヤー）")]
        private LayerMask raycastLayerMask = ~0;

        private IRayProvider? _rayProvider;

        /// <summary>今フレームでレイがヒットしたか。</summary>
        public bool HasHit { get; private set; }

        /// <summary>今フレームのレイキャスト結果。HasHitがtrueの場合のみ有効。</summary>
        public RaycastHit CurrentHit { get; private set; }

        /// <summary>
        /// IRayProviderを設定する。
        /// </summary>
        public void Initialize(IRayProvider rayProvider)
        {
            _rayProvider = rayProvider;
        }

        private void Update()
        {
            if (_rayProvider == null)
            {
                HasHit = false;
                return;
            }

            var ray = new Ray(_rayProvider.RayOrigin, _rayProvider.RayDirection);
            HasHit = Physics.Raycast(ray, out var hit, maxRayDistance, raycastLayerMask);
            CurrentHit = hit;
        }
    }
}
