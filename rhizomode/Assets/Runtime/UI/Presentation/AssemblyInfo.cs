#nullable enable

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Rhizomode.UI.Tests")]
// Rhizomode.Interaction の WindowGrabHandle / WindowGrabBootstrap が NdiViewWindow の
// RaiseTransformChanged (internal) を呼ぶため公開対象に追加。
[assembly: InternalsVisibleTo("Rhizomode.Interaction")]
