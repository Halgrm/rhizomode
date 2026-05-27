#nullable enable

using System.Runtime.CompilerServices;

// internal CameraOverrideSession / SceneVolumeOverride.Apply 等を
// EditMode test (Rhizomode.Scene.Tests) から呼ぶための friend assembly 宣言。
[assembly: InternalsVisibleTo("Rhizomode.Scene.Tests")]
