#nullable enable

// Phase 8 Round C: NodeSpawnService が SpawnResult / InputSpawnResult record を使うため
// IsExternalInit polyfill が必要。SharedKernel の polyfill と同じ最小定義。
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
