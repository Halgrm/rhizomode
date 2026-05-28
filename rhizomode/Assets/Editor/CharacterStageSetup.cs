#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using Rhizomode.UI.Presentation.Character;
using UnityEditor;
using UnityEditor.Animations;
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
        private const string MaryciaMenuPath = "Rhizomode/Character/Setup Marycia Avatar";
        private const string MaryciaPrefabPath = "Assets/BeroarN/bn0010_Marycia/Prefab/bn0010_Marycia_Mobile_2P.prefab";
        private const string AnimatorDirectory = "Assets/Data/Animators";
        private const string IkControllerPath = AnimatorDirectory + "/VrHumanoidIK.controller";
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

        [MenuItem(MaryciaMenuPath)]
        public static void SetupMaryciaAvatar()
        {
            GameObject? prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MaryciaPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Marycia prefab not found: {MaryciaPrefabPath}");
                return;
            }

            RemoveLowPolyPlaceholder();

            // Idempotent: reuse an already-spawned Marycia (has VrHumanoidBinder) if present.
            VrHumanoidBinder? existing = UnityEngine.Object.FindFirstObjectByType<VrHumanoidBinder>(
                FindObjectsInactive.Include);
            GameObject avatar;
            if (existing != null)
            {
                avatar = existing.gameObject;
            }
            else
            {
                avatar = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                avatar.name = prefab.name;
                Undo.RegisterCreatedObjectUndo(avatar, "Create Marycia Avatar");
            }

            FixAvatarMaterials(avatar);

            Animator? animator = avatar.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("Marycia avatar has no Humanoid Animator. Aborting VR bind.");
                return;
            }

            // Humanoid IK requires a controller whose layer 0 has IK Pass enabled.
            animator.runtimeAnimatorController = LoadOrCreateIkController();

            VrHumanoidBinder binder = avatar.GetComponent<VrHumanoidBinder>()
                ?? avatar.AddComponent<VrHumanoidBinder>();
            WireHumanoidXrRig(binder);

            // 3rd-person camera follows the avatar head (RT output, never the HMD display).
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            CreateCharacterCamera(headBone != null ? headBone : avatar.transform);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Marycia avatar VR setup complete (materials + humanoid IK bind + head-follow camera).");
        }

        // The Marycia Mobile package ships with a VRChat toon shader that is absent from this
        // URP project, so every material falls back to Hidden/InternalErrorShader (pink). We
        // re-home them onto URP/Lit. Only the CosA color map shipped with the package; skin and
        // hair maps are missing, so those get sensible solid fallback colors. Idempotent.
        private static readonly Color SkinFallbackColor = new Color(0.96f, 0.82f, 0.74f, 1f);
        private static readonly Color HairFallbackColor = new Color(0.20f, 0.14f, 0.12f, 1f);

        private static void FixAvatarMaterials(GameObject avatar)
        {
            Shader? urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning("URP/Lit shader not found; avatar materials left as-is.");
                return;
            }

            var seen = new HashSet<Material>();
            foreach (Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || !seen.Add(material))
                    {
                        continue;
                    }

                    bool brokenShader = material.shader == null ||
                                        material.shader.name == "Hidden/InternalErrorShader";
                    if (!brokenShader && material.shader.name.StartsWith("Universal Render Pipeline"))
                    {
                        continue; // already healthy URP material
                    }

                    Texture? mainTex = ReadSavedMainTex(material);
                    material.shader = urpLit;

                    string lowerName = material.name.ToLowerInvariant();
                    if (mainTex != null && material.HasProperty("_BaseMap"))
                    {
                        material.SetTexture("_BaseMap", mainTex);
                        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                    }
                    else if (lowerName.Contains("hair"))
                    {
                        SetBaseColor(material, HairFallbackColor);
                    }
                    else if (lowerName.Contains("skin") || lowerName.Contains("face") || lowerName.Contains("body"))
                    {
                        SetBaseColor(material, SkinFallbackColor);
                    }
                    else
                    {
                        SetBaseColor(material, Color.white);
                    }

                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                    EditorUtility.SetDirty(material);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void SetBaseColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }

        // Reads the legacy _MainTex still present in m_SavedProperties even when the current
        // shader is the error shader (which exposes no properties through Material.GetTexture).
        private static Texture? ReadSavedMainTex(Material material)
        {
            var so = new SerializedObject(material);
            SerializedProperty? texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (texEnvs == null || !texEnvs.isArray)
            {
                return null;
            }

            for (int i = 0; i < texEnvs.arraySize; i++)
            {
                SerializedProperty entry = texEnvs.GetArrayElementAtIndex(i);
                SerializedProperty? first = entry?.FindPropertyRelative("first");
                SerializedProperty? nameProp = first?.FindPropertyRelative("name");
                if (nameProp == null || nameProp.stringValue != "_MainTex")
                {
                    continue;
                }

                SerializedProperty? second = entry!.FindPropertyRelative("second");
                SerializedProperty? texProp = second?.FindPropertyRelative("m_Texture");
                return texProp?.objectReferenceValue as Texture;
            }

            return null;
        }

        private static void RemoveLowPolyPlaceholder()
        {
            VrCharacterBinder[] placeholders = UnityEngine.Object.FindObjectsByType<VrCharacterBinder>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (VrCharacterBinder placeholder in placeholders)
            {
                Undo.DestroyObjectImmediate(placeholder.gameObject);
            }
        }

        private static void WireHumanoidXrRig(VrHumanoidBinder binder)
        {
            Transform? xrOrigin = FindXrOriginTransform();
            if (xrOrigin == null)
            {
                Debug.LogWarning("XROrigin not found. Marycia tracking fields left empty.");
                return;
            }

            Transform? cameraOffset = FindChildRecursive(xrOrigin, CameraOffsetName);
            Transform searchRoot = cameraOffset != null ? cameraOffset : xrOrigin;
            SetObjectReference(binder, "hmd", FindChildRecursive(searchRoot, MainCameraName));
            SetObjectReference(binder, "leftController", FindChildByNameContains(searchRoot, "Left"));
            SetObjectReference(binder, "rightController", FindChildByNameContains(searchRoot, "Right"));
        }

        private static RuntimeAnimatorController LoadOrCreateIkController()
        {
            AnimatorController? existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(IkControllerPath);
            if (existing != null)
            {
                EnsureIkPass(existing);
                return existing;
            }

            EnsureAnimatorDirectory();
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(IkControllerPath);
            EnsureIkPass(controller);
            return controller;
        }

        private static void EnsureIkPass(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length == 0)
            {
                return;
            }

            if (!layers[0].iKPass)
            {
                layers[0].iKPass = true;
                controller.layers = layers; // reassign so Unity serializes the change
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
            }
        }

        private static void EnsureAnimatorDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }

            if (!AssetDatabase.IsValidFolder(AnimatorDirectory))
            {
                AssetDatabase.CreateFolder("Assets/Data", "Animators");
            }
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
            Vector3 camOffset = new Vector3(0f, 0.3f, -2.0f);
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("cam").objectReferenceValue = camera;
            serializedController.FindProperty("target").objectReferenceValue = target;
            serializedController.FindProperty("offset").vector3Value = camOffset;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            // Place the camera at its follow position immediately so it is never left at the
            // spawn origin (which sits inside the avatar mesh in Edit mode → "camera clipping").
            if (target != null)
            {
                float yaw = target.eulerAngles.y;
                cameraObject.transform.position =
                    target.position + Quaternion.Euler(0f, yaw, 0f) * camOffset;
                cameraObject.transform.LookAt(target.position);
            }
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
