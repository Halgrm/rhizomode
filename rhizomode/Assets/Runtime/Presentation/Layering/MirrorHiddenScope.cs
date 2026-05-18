#nullable enable

using UnityEngine;

namespace Rhizomode.Presentation.Layering
{
    /// <summary>
    /// このコンポーネントを attach した GameObject とその全子孫を <c>MirrorHidden</c> Layer に
    /// 自動で揃える MonoBehaviour scope。Mirror カメラ (Spout / NDI / Desktop 配信先) から
    /// UI / コントローラー / preview Quad / path edit handle / look-at marker を隠すための
    /// 自動適用機構。
    /// </summary>
    /// <remarks>
    /// <para>適用タイミング:</para>
    /// <list type="bullet">
    ///   <item><c>Awake</c>: 静的に置かれた子孫を一括適用</item>
    ///   <item><c>OnEnable</c>: disable→enable の往復で初期化された場合の保険</item>
    ///   <item><c>OnTransformChildrenChanged</c>: runtime spawn (NodeVisual / EdgeVisual /
    ///     ScrollBar / Path/LookAt handle / XR Toolkit Controller Model 遅延スポーン等)
    ///     で子が追加された瞬間に発火</item>
    /// </list>
    /// <para>哲学:</para>
    /// 旧版では各 manager の spawn site で <see cref="MirrorHiddenLayer.ApplyRecursive"/> を
    /// 明示的に呼び出していたが、規約だけで「忘れたら mirror に漏れる」リスクが残った。
    /// この scope を root に attach するだけで「scope 配下に置けば自動で MirrorHidden 化」を
    /// 担保し、<c>RequireMirrorHiddenAttribute</c> + SceneValidator で「scope 漏れ = build fail」
    /// の自己宣言型プロトコルを実現する (cf. NodeBase の <c>[NodeType]</c> + Default SO)。
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class MirrorHiddenScope : MonoBehaviour
    {
        private void Awake()
        {
            MirrorHiddenLayer.ApplyRecursive(gameObject);
        }

        private void OnEnable()
        {
            MirrorHiddenLayer.ApplyRecursive(gameObject);
        }

        // Unity message: 自身の Transform の直接子が追加/削除されたときに呼ばれる。
        // 孫以下の追加では発火しないが、layer は再帰適用するため scope 1 つで深い孫まで揃う。
        private void OnTransformChildrenChanged()
        {
            MirrorHiddenLayer.ApplyRecursive(gameObject);
        }
    }
}
