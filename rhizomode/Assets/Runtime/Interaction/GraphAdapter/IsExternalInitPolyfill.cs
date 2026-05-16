#nullable enable

// C# 9 record / init-only setters polyfill for this asmdef (compiler looks per-assembly).
// F-Vf-d.2: NodeSpawnService.SpawnResult record を Interaction.GraphAdapter へ移送したため必要。

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
