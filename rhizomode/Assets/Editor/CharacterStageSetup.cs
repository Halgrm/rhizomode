#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.UI.Presentation.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rhizomode.Editor.Character
{
    public static class CharacterStageSetup
    {
        private const string CharacterPath = "Assets/Charactor/final low poly character  rigged.fbx";
        private const string MaterialDirectory = "Assets/Data/Materials";
        private const string MaterialPath = MaterialDirectory + "/AudioOutline.mat";
        private const string RenderTexturePath = MaterialDirectory + "/CharacterCameraRT.renderTexture";
        private const string ShaderName = "Rhizomode/AudioOutlineUnlit";
        private const int CharacterRtWidth = 1920;
        private const int CharacterRtHeight = 1080;
        private const string MenuPath = "Rhizomode/Character/Setup Stage";
        private const string CameraOffsetName = "Camera Offset";
        private const string MainCameraName = "Main Camera";

        [MenuItem(MenuPath)]
        public static void SetupStage()
        {
            GameObject? prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            if (prefab == null)
            {
                Debug.LogError($"Character FBX not found: {CharacterPath}");
                return;
            }

            Material? material = LoadOrCreateMaterial();
            if (material == null)
            {
                return;
            }

            // Idempotent: if a character (with binder) already exists in the scene, reuse it
            // instead of spawning a duplicate every time Setup is re-run.
            VrCharacterBinder? existingBinder = UnityEngine.Object.FindFirstObjectByType<VrCharacterBinder>(
                FindObjectsInactive.Include);
            GameObject character;
            if (existingBinder != null)
            {
                character = existingBinder.gameObject;
            }
            else
            {
                character = UnityEngine.Object.Instantiate(prefab);
                character.name = prefab.name;
                Undo.RegisterCreatedObjectUndo(character, "Create VR Character");
            }

            SkinnedMeshRenderer? body = AssignRenderers(character, material, out MeshRenderer[] attachments);
            VrCharacterBinder binder = character.AddComponent<VrCharacterBinder>();
            WireXrRig(binder);
            WireBones(character.transform, binder);

            AudioOutlineDriver outlineDriver = character.AddComponent<AudioOutlineDriver>();
            SerializedObject outlineObject = new SerializedObject(outlineDriver);
            outlineObject.FindProperty("body").objectReferenceValue = body;
            SetObjectArray(outlineObject.FindProperty("attachments"), attachments);
            outlineObject.ApplyModifiedPropertiesWithoutUndo();

            CreateCharacterCamera(character.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"Character stage setup complete. Body: {body != null}, attachments: {attachments.Length}");
        }

        private static Material? LoadOrCreateMaterial()
        {
            Shader? shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Shader not found: {ShaderName}");
                return null;
            }

            EnsureMaterialDirectory();
            Material? material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
                return material;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void EnsureMaterialDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }

            if (!AssetDatabase.IsValidFolder(MaterialDirectory))
            {
                AssetDatabase.CreateFolder("Assets/Data", "Materials");
            }
        }

        private static SkinnedMeshRenderer? AssignRenderers(
            GameObject character,
            Material material,
            out MeshRenderer[] attachments)
        {
            SkinnedMeshRenderer? body = FindSkinnedRenderer(character.transform, "body");
            if (body != null)
            {
                body.sharedMaterial = material;
            }
            else
            {
                Debug.LogWarning("SkinnedMeshRenderer named body was not found.");
            }

            attachments = character.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer attachment in attachments)
            {
                attachment.sharedMaterial = material;
            }

            return body;
        }

        private static void WireXrRig(VrCharacterBinder binder)
        {
            Transform? xrOrigin = FindXrOriginTransform();
            if (xrOrigin == null)
            {
                Debug.LogWarning("XROrigin was not found. Character tracking fields were left empty.");
                return;
            }

            Transform? cameraOffset = FindChildRecursive(xrOrigin, CameraOffsetName);
            Transform searchRoot = cameraOffset != null ? cameraOffset : xrOrigin;
            SetObjectReference(binder, "xrOrigin", xrOrigin);
            SetObjectReference(binder, "hmd", FindChildRecursive(searchRoot, MainCameraName));
            SetObjectReference(binder, "leftController", FindChildByNameContains(searchRoot, "Left"));
            SetObjectReference(binder, "rightController", FindChildByNameContains(searchRoot, "Right"));
        }

        private static Transform? FindXrOriginTransform()
        {
            Type? xrOriginType = FindXrOriginType();
            if (xrOriginType == null || !typeof(Component).IsAssignableFrom(xrOriginType))
            {
                return null;
            }

            UnityEngine.Object[] origins = UnityEngine.Object.FindObjectsByType(
                xrOriginType,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (origins.Length == 0)
            {
                return null;
            }

            return ((Component)origins[0]).transform;
        }

        private static Type? FindXrOriginType()
        {
            return Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils")
                ?? Type.GetType("UnityEngine.XR.Interaction.Toolkit.XROrigin, Unity.XR.Interaction.Toolkit");
        }

        private static void WireBones(Transform characterRoot, VrCharacterBinder binder)
        {
            SetObjectReference(binder, "rootBone", FindChildRecursive(characterRoot, "hips"));
            SetObjectReference(binder, "headBone", FindChildRecursive(characterRoot, "head"));
            SetObjectReference(binder, "leftHandBone", FindChildRecursive(characterRoot, "hand.L"));
            SetObjectReference(binder, "rightHandBone", FindChildRecursive(characterRoot, "hand.R"));
        }

        private static void CreateCharacterCamera(Transform target)
        {
            // Reuse an existing camera if Setup is re-run (idempotent), otherwise create one.
            GameObject? cameraObject = GameObject.Find("Character Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Character Camera");
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Character Camera");
            }

            Camera camera = cameraObject.GetComponent<Camera>() ?? cameraObject.AddComponent<Camera>();

            // CRITICAL: the 3rd-person camera must NOT take over the HMD stereo display.
            // Without these two lines it renders into both eyes on top of the XR Main Camera
            // (depth 0 > -1), replacing the head-tracked view. Render to an offscreen RT and
            // opt out of VR stereo so the HMD viewpoint keeps tracking.
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.targetTexture = LoadOrCreateCharacterRenderTexture();
            camera.tag = "Untagged";
            camera.depth = -10;

            CharacterCameraController controller =
                cameraObject.GetComponent<CharacterCameraController>()
                ?? cameraObject.AddComponent<CharacterCameraController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("cam").objectReferenceValue = camera;
            serializedController.FindProperty("target").objectReferenceValue = target;
            serializedController.FindProperty("offset").vector3Value = new Vector3(0f, 1.6f, -2.5f);
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RenderTexture LoadOrCreateCharacterRenderTexture()
        {
            EnsureMaterialDirectory();
            RenderTexture? existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (existing != null)
            {
                return existing;
            }

            RenderTexture rt = new RenderTexture(CharacterRtWidth, CharacterRtHeight, 24)
            {
                name = "CharacterCameraRT",
            };
            AssetDatabase.CreateAsset(rt, RenderTexturePath);
            AssetDatabase.SaveAssets();
            return rt;
        }

        private static SkinnedMeshRenderer? FindSkinnedRenderer(Transform root, string name)
        {
            SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (string.Equals(renderer.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return renderer;
                }
            }

            return null;
        }

        private static Transform? FindChildRecursive(Transform root, string name)
        {
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform? match = FindChildRecursive(child, name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform? FindChildByNameContains(Transform root, string value)
        {
            if (root.name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform? match = FindChildByNameContains(child, value);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object? value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(
            SerializedProperty property,
            IReadOnlyList<UnityEngine.Object> values)
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
#endif
