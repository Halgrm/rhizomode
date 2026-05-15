#nullable enable

// C# 9 record / init-only setters polyfill for this asmdef (compiler looks per-assembly).
// Plan v5.4 §15 F-Vf-a.1 Phase A: InputSpawnResult record を Bootstrap から移送するため必要。

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
