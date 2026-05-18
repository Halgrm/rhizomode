#nullable enable

using System;

namespace Rhizomode.Presentation.Layering
{
    /// <summary>
    /// 「この MonoBehaviour 型が attach されている GameObject の祖先に
    /// <see cref="MirrorHiddenScope"/> が無ければ build を fail させる」ことを宣言する class 属性。
    /// </summary>
    /// <remarks>
    /// <para>使い方:</para>
    /// 各 visual / preview / panel manager の MonoBehaviour 定義に
    /// <c>[RequireMirrorHidden]</c> を付けると、Editor の <c>MirrorHiddenSceneValidator</c> が
    /// scene / prefab を走査して scope 漏れを検出する。
    /// <para>哲学:</para>
    /// NodeBase の <c>[NodeType("...")]</c> + Default SO + Validator と同じ自己宣言型プロトコル。
    /// 「忘れたら CI で落ちる」を pattern 化することで、新しい manager を追加する人が
    /// 規約を知らなくても安全になる。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class RequireMirrorHiddenAttribute : Attribute
    {
    }
}
