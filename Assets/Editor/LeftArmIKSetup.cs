using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

public static class LeftArmIKSetup
{
    private const string MenuPath = "Tools/VR Parkour/Configure Left Arm IK";
    private const string KylePrefabPath = "Assets/Starter Assets/Runtime/SpaceRobotKyle/Prefabs/KyleRobot.prefab";

    [MenuItem(MenuPath)]
    private static void Configure()
    {
        var avatarVisual = FindTransformByName("AvatarVisual");
        var kyleInstance = avatarVisual != null ? FindDescendant(avatarVisual, "KyleRobot") : FindTransformByName("KyleRobot");
        var leftController = FindTransformByName("Left Controller");

        if (kyleInstance == null)
            throw new InvalidOperationException("KyleRobot was not found in the active scene.");
        if (leftController == null)
            throw new InvalidOperationException("Left Controller was not found in the active scene.");

        RemoveSceneOnlyRig(kyleInstance, leftController);
        ConfigureKylePrefab();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        kyleInstance = avatarVisual != null ? FindDescendant(avatarVisual, "KyleRobot") : FindTransformByName("KyleRobot");
        var rigTransform = FindDescendant(kyleInstance, "Rig");
        var constraintTransform = FindDescendant(rigTransform, "LeftArmIK");
        var target = FindDescendant(rigTransform, "LeftHandIKTarget");
        var follower = target != null ? target.GetComponent<TransformPoseFollower>() : null;

        if (constraintTransform == null || follower == null)
            throw new InvalidOperationException("The prefab IK hierarchy was not instantiated correctly.");

        Undo.RecordObject(follower, "Connect left controller to IK target");
        follower.Source = leftController;
        EditorUtility.SetDirty(follower);
        PrefabUtility.RecordPrefabInstancePropertyModifications(follower);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = constraintTransform.gameObject;
        Debug.Log("[LeftArmIKSetup] CONFIGURED prefab rig with LeftUpperArm -> LeftArm -> LeftHand; LeftHandIKTarget follows Left Controller.");
    }

    private static void RemoveSceneOnlyRig(Transform kyleInstance, Transform leftController)
    {
        var rig = FindDescendant(kyleInstance, "Rig");
        if (rig != null && PrefabUtility.GetCorrespondingObjectFromSource(rig.gameObject) == null)
            UnityEngine.Object.DestroyImmediate(rig.gameObject);

        var rigBuilder = kyleInstance.GetComponent<RigBuilder>();
        if (rigBuilder != null && PrefabUtility.IsAddedComponentOverride(rigBuilder))
            UnityEngine.Object.DestroyImmediate(rigBuilder);

        var oldTarget = FindDescendant(leftController, "LeftHandIKTarget");
        if (oldTarget != null && PrefabUtility.GetCorrespondingObjectFromSource(oldTarget.gameObject) == null)
            UnityEngine.Object.DestroyImmediate(oldTarget.gameObject);
    }

    private static void ConfigureKylePrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(KylePrefabPath);
        try
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
                throw new InvalidOperationException("KyleRobot prefab needs a Humanoid Animator.");

            var upperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var lowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (upperArm == null || lowerArm == null || hand == null)
                throw new InvalidOperationException("Left arm Humanoid bones could not be resolved in the prefab.");

            var avatarRoot = animator.avatarRoot;
            if (avatarRoot == null)
                throw new InvalidOperationException("KyleRobot Animator does not expose an avatar root.");

            var rigTransform = FindDescendant(root.transform, "Rig");
            if (rigTransform == null)
                rigTransform = GetOrCreateChild(avatarRoot, "Rig");
            else if (rigTransform.parent != avatarRoot)
                rigTransform.SetParent(avatarRoot, true);
            var rig = GetOrAddComponent<Rig>(rigTransform.gameObject);
            rig.weight = 1f;

            var constraintTransform = GetOrCreateChild(rigTransform, "LeftArmIK");
            var constraint = GetOrAddComponent<TwoBoneIKConstraint>(constraintTransform.gameObject);

            var target = GetOrCreateChild(rigTransform, "LeftHandIKTarget");
            target.position = hand.position;
            target.rotation = hand.rotation;
            GetOrAddComponent<TransformPoseFollower>(target.gameObject);

            var hint = GetOrCreateChild(rigTransform, "LeftElbowHint");
            var armDirection = (hand.position - upperArm.position).normalized;
            var bendDirection = Vector3.Cross(armDirection, root.transform.up).normalized;
            if (bendDirection.sqrMagnitude < 0.5f)
                bendDirection = -root.transform.forward;
            hint.position = lowerArm.position + bendDirection * 0.35f;
            hint.rotation = Quaternion.identity;

            var data = constraint.data;
            data.root = upperArm;
            data.mid = lowerArm;
            data.tip = hand;
            data.target = target;
            data.hint = hint;
            data.targetPositionWeight = 1f;
            data.targetRotationWeight = 1f;
            data.hintWeight = 1f;
            constraint.data = data;
            constraint.weight = 1f;

            var rigBuilder = GetOrAddComponent<RigBuilder>(root);
            if (!rigBuilder.layers.Any(layer => layer.rig == rig))
                rigBuilder.layers.Add(new RigLayer(rig, true));

            PrefabUtility.SaveAsPrefabAsset(root, KylePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        var existing = gameObject.GetComponent<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        var existing = FindDirectChild(parent, name);
        if (existing != null)
            return existing;

        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name)
                return child;
        }

        return null;
    }

    private static Transform FindTransformByName(string name)
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                return root.transform;

            var match = FindDescendant(root.transform, name);
            if (match != null)
                return match;
        }

        return null;
    }

    private static Transform FindDescendant(Transform parent, string name)
    {
        if (parent == null)
            return null;

        foreach (var child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }

        return null;
    }
}
