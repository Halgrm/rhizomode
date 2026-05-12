#nullable enable

using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rhizomode.Cameras.Editor
{
    internal static class PathDefaultKnotsTool
    {
        [MenuItem("Tools/Rhizomode/Path Camera/Initialize Empty Paths")]
        private static void InitializeEmptyPaths()
        {
            var controllers = Object.FindObjectsByType<PathCameraController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int touched = 0;
            foreach (var pc in controllers)
            {
                var container = pc.GetComponent<SplineContainer>();
                if (container == null) continue;
                if (container.Spline.Count > 0) continue;

                Undo.RecordObject(container, "Initialize Path");
                var spline = container.Spline;
                spline.Add(new BezierKnot(new float3(-3f, 1.5f, 2f)));
                spline.Add(new BezierKnot(new float3(0f, 2.5f, 4f)));
                spline.Add(new BezierKnot(new float3(3f, 1.5f, 2f)));
                spline.Closed = false;
                EditorUtility.SetDirty(container);
                touched++;
            }

            Debug.Log($"[PathCamera] Initialized {touched} empty path(s).");
        }
    }
}
