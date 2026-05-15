#nullable enable

// Plan v5.4 V-final (Vf-a): NodeSpawnService が SpawnResult record を使うため IsExternalInit polyfill が必要。
// (F-Vf-a.1 Phase A 後: InputSpawnResult は UI.GraphAdapter へ移送済)
// (F-Vf-a.1 Phase C 後: SceneObjectSpawnResult は Scene.GraphAdapter へ移送済)
// SharedKernel の polyfill は internal で別 asmdef からは参照不可なので Bootstrap asmdef にも置く。
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
