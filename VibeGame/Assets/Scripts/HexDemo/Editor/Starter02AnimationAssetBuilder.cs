using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HexDemo.Editor
{
    public static class Starter02AnimationAssetBuilder
    {
        private const string PrefabPath = "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_02/Starter_02.prefab";
        private const string QuaterniusFbxPath = "Assets/ThirdParty/Quaternius/UniversalAnimationLibraryStandard/Unity/UAL1_Standard.fbx";
        private const string OutputFolder = "Assets/Animations/Starter02";
        private const string IdleClipPath = OutputFolder + "/Starter_02_Idle.anim";
        private const string WalkClipPath = OutputFolder + "/Starter_02_Walk.anim";
        private const string ControllerPath = OutputFolder + "/Starter_02.controller";
        private const string MovingParameter = "IsMoving";

        [InitializeOnLoadMethod]
        private static void GenerateOnLoad()
        {
            if (!File.Exists(ControllerPath))
                Generate();
        }

        [MenuItem("Tools/Hex Demo/Rebuild Starter 02 Animations")]
        public static void Generate()
        {
            if (File.Exists(QuaterniusFbxPath))
            {
                GenerateFromQuaternius();
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Starter02AnimationAssetBuilder could not find prefab at {PrefabPath}");
                return;
            }

            EnsureFolder(OutputFolder);

            var bones = CacheBones(prefab.transform);
            var idle = CreateIdleClip(prefab.transform, bones);
            var walk = CreateWalkClip(prefab.transform, bones);

            SaveAsset(idle, IdleClipPath);
            SaveAsset(walk, WalkClipPath);
            SaveController(idle, walk);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated Starter_02 idle/walk animation clips and controller.");
        }

        private static void GenerateFromQuaternius()
        {
            EnsureFolder(OutputFolder);
            ConfigureQuaterniusImporter();

            var clips = AssetDatabase.LoadAllAssetsAtPath(QuaterniusFbxPath);
            AnimationClip idle = null;
            AnimationClip walk = null;

            foreach (var asset in clips)
            {
                if (asset is not AnimationClip clip)
                    continue;

                string clipName = clip.name.ToLowerInvariant();
                if (idle == null && IsStandingIdleName(clipName))
                    idle = clip;

                if (walk == null && IsPlainWalkName(clipName))
                    walk = clip;
            }

            if (idle == null)
                idle = FindFirstClipContaining(clips, "idle");

            if (walk == null)
                walk = FindFirstClipContaining(clips, "walk");

            if (idle == null || walk == null)
            {
                Debug.LogWarning($"Could not find idle/walk clips in {QuaterniusFbxPath}. Found: {string.Join(", ", GetClipNames(clips))}");
                return;
            }

            SaveController(idle, walk);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Generated Starter_02 controller from Quaternius clips: idle={idle.name}, walk={walk.name}");
        }

        private static void ConfigureQuaterniusImporter()
        {
            var importer = AssetImporter.GetAtPath(QuaterniusFbxPath) as ModelImporter;
            if (importer == null)
                return;

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                changed = true;
            }

            var clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    string clipName = clips[i].name.ToLowerInvariant();
                    if (clipName.Contains("idle") || clipName.Contains("walk") || clipName.Contains("run") || clipName.Contains("jog"))
                    {
                        clips[i].loopTime = true;
                        clips[i].lockRootRotation = true;
                        clips[i].lockRootHeightY = true;
                        clips[i].lockRootPositionXZ = true;
                    }
                }

                importer.clipAnimations = clips;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        private static AnimationClip FindFirstClipContaining(Object[] assets, string text)
        {
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && clip.name.ToLowerInvariant().Contains(text))
                    return clip;
            }

            return null;
        }

        private static bool IsForwardWalkName(string clipName)
        {
            return clipName.Contains("walk") &&
                   !clipName.Contains("back") &&
                   !clipName.Contains("left") &&
                   !clipName.Contains("right") &&
                   !clipName.Contains("strafe") &&
                   !clipName.Contains("crouch");
        }

        private static bool IsStandingIdleName(string clipName)
        {
            return clipName.EndsWith("idle_loop") &&
                   !clipName.Contains("crouch") &&
                   !clipName.Contains("sitting") &&
                   !clipName.Contains("swim") &&
                   !clipName.Contains("pistol") &&
                   !clipName.Contains("spell") &&
                   !clipName.Contains("sword") &&
                   !clipName.Contains("talking") &&
                   !clipName.Contains("torch");
        }

        private static bool IsPlainWalkName(string clipName)
        {
            return clipName.EndsWith("walk_loop") &&
                   !clipName.Contains("formal") &&
                   !clipName.Contains("back") &&
                   !clipName.Contains("left") &&
                   !clipName.Contains("right") &&
                   !clipName.Contains("strafe") &&
                   !clipName.Contains("crouch");
        }

        private static IEnumerable<string> GetClipNames(Object[] assets)
        {
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip)
                    yield return clip.name;
            }
        }

        private static AnimationClip CreateIdleClip(Transform prefabRoot, Dictionary<string, Transform> bones)
        {
            var clip = NewLoopingClip("Starter_02_Idle", 1.2f);
            SetPositionYCurve(clip, "", 0f, 0.018f, 1.2f);

            SetRotationCurve(clip, prefabRoot, bones, "spine_01", (0f, Vector3.zero), (0.6f, new Vector3(1.8f, 0f, 0.8f)), (1.2f, Vector3.zero));
            SetRotationCurve(clip, prefabRoot, bones, "spine_02", (0f, Vector3.zero), (0.6f, new Vector3(1.2f, 0f, -0.5f)), (1.2f, Vector3.zero));
            SetRotationCurve(clip, prefabRoot, bones, "head", (0f, Vector3.zero), (0.6f, new Vector3(-1f, 0f, 0f)), (1.2f, Vector3.zero));

            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static AnimationClip CreateWalkClip(Transform prefabRoot, Dictionary<string, Transform> bones)
        {
            var clip = NewLoopingClip("Starter_02_Walk", 0.8f);
            SetPositionYCurve(clip, "", 0f, 0.055f, 0.8f);

            SetRotationCurve(clip, prefabRoot, bones, "pelvis",
                (0f, new Vector3(0f, 0f, 4f)),
                (0.2f, new Vector3(0f, 0f, -4f)),
                (0.4f, new Vector3(0f, 0f, 4f)),
                (0.6f, new Vector3(0f, 0f, -4f)),
                (0.8f, new Vector3(0f, 0f, 4f)));

            SetRotationCurve(clip, prefabRoot, bones, "spine_01",
                (0f, new Vector3(5f, 0f, -3f)),
                (0.2f, new Vector3(7f, 0f, 3f)),
                (0.4f, new Vector3(5f, 0f, -3f)),
                (0.6f, new Vector3(7f, 0f, 3f)),
                (0.8f, new Vector3(5f, 0f, -3f)));

            SetRotationCurve(clip, prefabRoot, bones, "thigh_l",
                (0f, new Vector3(34f, 0f, 0f)),
                (0.2f, new Vector3(0f, 0f, 0f)),
                (0.4f, new Vector3(-28f, 0f, 0f)),
                (0.6f, new Vector3(0f, 0f, 0f)),
                (0.8f, new Vector3(34f, 0f, 0f)));

            SetRotationCurve(clip, prefabRoot, bones, "thigh_r",
                (0f, new Vector3(-28f, 0f, 0f)),
                (0.2f, new Vector3(0f, 0f, 0f)),
                (0.4f, new Vector3(34f, 0f, 0f)),
                (0.6f, new Vector3(0f, 0f, 0f)),
                (0.8f, new Vector3(-28f, 0f, 0f)));

            SetRotationCurve(clip, prefabRoot, bones, "calf_l",
                (0f, new Vector3(0f, 0f, 0f)),
                (0.2f, new Vector3(24f, 0f, 0f)),
                (0.4f, new Vector3(12f, 0f, 0f)),
                (0.6f, new Vector3(4f, 0f, 0f)),
                (0.8f, new Vector3(0f, 0f, 0f)));

            SetRotationCurve(clip, prefabRoot, bones, "calf_r",
                (0f, new Vector3(12f, 0f, 0f)),
                (0.2f, new Vector3(4f, 0f, 0f)),
                (0.4f, new Vector3(0f, 0f, 0f)),
                (0.6f, new Vector3(24f, 0f, 0f)),
                (0.8f, new Vector3(12f, 0f, 0f)));

            SetRotationCurve(clip, prefabRoot, bones, "upperarm_l",
                (0f, new Vector3(-26f, 0f, 0f)),
                (0.2f, new Vector3(0f, 0f, 0f)),
                (0.4f, new Vector3(26f, 0f, 0f)),
                (0.6f, new Vector3(0f, 0f, 0f)),
                (0.8f, new Vector3(-26f, 0f, 0f)));

            SetRotationCurve(clip, prefabRoot, bones, "upperarm_r",
                (0f, new Vector3(26f, 0f, 0f)),
                (0.2f, new Vector3(0f, 0f, 0f)),
                (0.4f, new Vector3(-26f, 0f, 0f)),
                (0.6f, new Vector3(0f, 0f, 0f)),
                (0.8f, new Vector3(26f, 0f, 0f)));

            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static AnimationClip NewLoopingClip(string name, float length)
        {
            var clip = new AnimationClip
            {
                name = name,
                frameRate = 30f,
                legacy = false
            };

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            settings.stopTime = length;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        private static void SetPositionYCurve(AnimationClip clip, string path, float baseY, float amplitude, float length)
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, baseY),
                new Keyframe(length * 0.25f, baseY + amplitude),
                new Keyframe(length * 0.5f, baseY),
                new Keyframe(length * 0.75f, baseY + amplitude),
                new Keyframe(length, baseY));

            Smooth(curve);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y"), curve);
        }

        private static void SetRotationCurve(
            AnimationClip clip,
            Transform root,
            Dictionary<string, Transform> bones,
            string boneName,
            params (float time, Vector3 eulerOffset)[] keys)
        {
            if (!bones.TryGetValue(boneName, out var bone))
                return;

            string path = AnimationUtility.CalculateTransformPath(bone, root);
            Quaternion baseRotation = bone.localRotation;
            var x = new AnimationCurve();
            var y = new AnimationCurve();
            var z = new AnimationCurve();
            var w = new AnimationCurve();

            foreach (var key in keys)
            {
                Quaternion value = baseRotation * Quaternion.Euler(key.eulerOffset);
                x.AddKey(key.time, value.x);
                y.AddKey(key.time, value.y);
                z.AddKey(key.time, value.z);
                w.AddKey(key.time, value.w);
            }

            Smooth(x);
            Smooth(y);
            Smooth(z);
            Smooth(w);

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"), x);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"), y);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"), z);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"), w);
        }

        private static void Smooth(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
        }

        private static Dictionary<string, Transform> CacheBones(Transform root)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
            {
                if (!bones.ContainsKey(bone.name))
                    bones.Add(bone.name, bone);
            }

            return bones;
        }

        private static void SaveAsset(Object asset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.CreateAsset(asset, path);
        }

        private static void SaveController(AnimationClip idle, AnimationClip walk)
        {
            if (File.Exists(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(MovingParameter, AnimatorControllerParameterType.Bool);

            var stateMachine = controller.layers[0].stateMachine;
            stateMachine.states = new ChildAnimatorState[0];
            stateMachine.anyStateTransitions = new AnimatorStateTransition[0];

            var idleState = stateMachine.AddState("Idle");
            idleState.motion = idle;
            idleState.writeDefaultValues = true;

            var walkState = stateMachine.AddState("Walk");
            walkState.motion = walk;
            walkState.writeDefaultValues = true;

            stateMachine.defaultState = idleState;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.08f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, MovingParameter);

            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.08f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, MovingParameter);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
