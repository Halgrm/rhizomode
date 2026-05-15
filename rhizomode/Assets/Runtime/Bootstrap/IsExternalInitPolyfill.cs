#nullable enable

// Plan v5.4 V-final (Vf-a): NodeSpawnService / SceneObjectRegistrationService が SpawnResult /
// InputSpawnResult / SceneObjectSpawnResult record を使うため IsExternalInit polyfill が必要。
// SharedKernel の polyfill は internal で別 asmdef からは参照不可なので Bootstrap asmdef にも置く。
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
