using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

public static class FullBodyAvatarSetup
{
    private const string MenuPath = "Tools/VR Parkour/Configure Full Body Avatar";
    private const string ValidateMenuPath = "Tools/VR Parkour/Validate Full Body Avatar";
    private const string KylePrefabPath = "Assets/Starter Assets/Runtime/SpaceRobotKyle/Prefabs/KyleRobot.prefab";

    [InitializeOnLoadMethod]
    private static void ConfigureOnceAfterReload()
    {
        if (Application.isBatchMode)
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var kyle = FindTransformByName("KyleRobot");
            if (kyle == null || kyle.GetComponent<VRFullBodyAvatar>() != null)
                return;

            try
            {
                Configure();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem(MenuPath)]
    public static void Configure()
    {
        ConfigureKylePrefab();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var kyle = FindTransformByName("KyleRobot");
        var leftController = FindTransformByName("Left Controller");
        var rightController = FindTransformByName("Right Controller");
        var hmd = FindTransformByName("Main Camera");
        var movement = UnityEngine.Object.FindAnyObjectByType<ArmSwingMovement>(FindObjectsInactive.Include);
        var characterController = movement != null ? movement.GetComponent<CharacterController>() : null;

        if (kyle == null)
            throw new InvalidOperationException("KyleRobot was not found in the active scene.");
        if (leftController == null || rightController == null || hmd == null)
            throw new InvalidOperationException("Main Camera, Left Controller, or Right Controller was not found under XR Origin.");
        if (movement == null || characterController == null)
            throw new InvalidOperationException("ArmSwingMovement and CharacterController were not found on XR Origin.");

        ConnectFollower(kyle, "LeftHandIKTarget", leftController);
        ConnectFollower(kyle, "RightHandIKTarget", rightController);

        var driver = kyle.GetComponent<VRFullBodyAvatar>();
        if (driver == null)
            driver = Undo.AddComponent<VRFullBodyAvatar>(kyle.gameObject);

        Undo.RecordObject(driver, "Configure full body VR avatar");
        driver.Configure(hmd, leftController, rightController, characterController, movement);
        EditorUtility.SetDirty(driver);
        PrefabUtility.RecordPrefabInstancePropertyModifications(driver);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = kyle.gameObject;
        Validate();
        Debug.Log("[FullBodyAvatarSetup] CONFIGURED: both controller-driven arms, HMD head/body estimation, Animator locomotion/jump sync, grounded leg IK, and local head hiding. XR Origin and camera were not moved.");
    }

    [MenuItem(ValidateMenuPath)]
    private static void Validate()
    {
        var kyle = FindTransformByName("KyleRobot");
        if (kyle == null)
            throw new InvalidOperationException("KyleRobot was not found.");

        var animator = kyle.GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
            throw new InvalidOperationException("KyleRobot Animator is not Humanoid.");

        RequireBone(animator, HumanBodyBones.LeftUpperArm, "LeftUpperArm");
        RequireBone(animator, HumanBodyBones.LeftLowerArm, "LeftArm");
        RequireBone(animator, HumanBodyBones.LeftHand, "LeftHand");
        RequireBone(animator, HumanBodyBones.RightUpperArm, "RightUpperArm");
        RequireBone(animator, HumanBodyBones.RightLowerArm, "RightArm");
        RequireBone(animator, HumanBodyBones.RightHand, "RightHand");
        RequireBone(animator, HumanBodyBones.LeftUpperLeg, "LeftLeg");
        RequireBone(animator, HumanBodyBones.LeftLowerLeg, "LeftCalf");
        RequireBone(animator, HumanBodyBones.LeftFoot, "LeftFoot");
        RequireBone(animator, HumanBodyBones.RightUpperLeg, "RightLeg");
        RequireBone(animator, HumanBodyBones.RightLowerLeg, "RightCalf");
        RequireBone(animator, HumanBodyBones.RightFoot, "RightFoot");
        RequireBone(animator, HumanBodyBones.Head, "Head");

        var requiredObjects = new[]
        {
            "Rig", "LeftArmIK", "RightArmIK", "LeftHandIKTarget", "RightHandIKTarget",
            "LeftLegIK", "RightLegIK", "LeftFootIKTarget", "RightFootIKTarget"
        };
        foreach (var objectName in requiredObjects)
            if (FindDescendant(kyle, objectName) == null)
                throw new InvalidOperationException($"Required avatar object is missing: {objectName}");

        if (kyle.GetComponent<RigBuilder>() == null)
            throw new InvalidOperationException("RigBuilder is missing from KyleRobot.");
        if (kyle.GetComponent<VRFullBodyAvatar>() == null)
            throw new InvalidOperationException("VRFullBodyAvatar is missing from the scene instance.");

        Debug.Log("[FullBodyAvatarSetup] VALID: Kyle Humanoid bone map, bilateral arm IK, bilateral leg IK, RigBuilder, and full-body runtime driver are present.");
    }

    private static void ConfigureKylePrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(KylePrefabPath);
        try
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
                throw new InvalidOperationException("KyleRobot prefab needs a Humanoid Animator.");

            var avatarRoot = animator.avatarRoot;
            if (avatarRoot == null)
                throw new InvalidOperationException("KyleRobot Animator does not expose an avatar root.");

            var rigTransform = FindDescendant(root.transform, "Rig") ?? GetOrCreateChild(avatarRoot, "Rig");
            if (rigTransform.parent != avatarRoot)
                rigTransform.SetParent(avatarRoot, true);
            var rig = GetOrAddComponent<Rig>(rigTransform.gameObject);
            rig.weight = 1f;

            ConfigureArm(animator, rigTransform, true);
            ConfigureArm(animator, rigTransform, false);
            ConfigureLeg(animator, rigTransform, true);
            ConfigureLeg(animator, rigTransform, false);

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

    private static void ConfigureArm(Animator animator, Transform rig, bool left)
    {
        var side = left ? "Left" : "Right";
        var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        var lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        RequireTransforms(side + " arm", upper, lower, hand);

        var target = GetOrCreateChild(rig, side + "HandIKTarget");
        target.SetPositionAndRotation(hand.position, hand.rotation);
        GetOrAddComponent<TransformPoseFollower>(target.gameObject);

        var hint = GetOrCreateChild(rig, side + "ElbowHint");
        hint.position = lower.position - animator.transform.forward * 0.3f - animator.transform.up * 0.08f;

        var constraint = GetOrAddComponent<TwoBoneIKConstraint>(GetOrCreateChild(rig, side + "ArmIK").gameObject);
        var data = constraint.data;
        data.root = upper;
        data.mid = lower;
        data.tip = hand;
        data.target = target;
        data.hint = hint;
        data.targetPositionWeight = 1f;
        data.targetRotationWeight = 1f;
        data.hintWeight = 1f;
        constraint.data = data;
        constraint.weight = 1f;
    }

    private static void ConfigureLeg(Animator animator, Transform rig, bool left)
    {
        var side = left ? "Left" : "Right";
        var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
        var lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
        var foot = animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        RequireTransforms(side + " leg", upper, lower, foot);

        var target = GetOrCreateChild(rig, side + "FootIKTarget");
        target.SetPositionAndRotation(foot.position, foot.rotation);

        var hint = GetOrCreateChild(rig, side + "KneeHint");
        hint.position = lower.position + animator.transform.forward * 0.4f;

        var constraint = GetOrAddComponent<TwoBoneIKConstraint>(GetOrCreateChild(rig, side + "LegIK").gameObject);
        var data = constraint.data;
        data.root = upper;
        data.mid = lower;
        data.tip = foot;
        data.target = target;
        data.hint = hint;
        data.targetPositionWeight = 1f;
        data.targetRotationWeight = 1f;
        data.hintWeight = 1f;
        constraint.data = data;
        constraint.weight = 0f;
    }

    private static void ConnectFollower(Transform kyle, string targetName, Transform source)
    {
        var target = FindDescendant(kyle, targetName);
        var follower = target != null ? target.GetComponent<TransformPoseFollower>() : null;
        if (follower == null)
            throw new InvalidOperationException($"{targetName} or TransformPoseFollower is missing.");

        Undo.RecordObject(follower, "Connect tracked controller to hand IK");
        follower.Source = source;
        EditorUtility.SetDirty(follower);
        PrefabUtility.RecordPrefabInstancePropertyModifications(follower);
    }

    private static Transform RequireBone(Animator animator, HumanBodyBones bone, string expectedName)
    {
        var result = animator.GetBoneTransform(bone);
        if (result == null)
            throw new InvalidOperationException($"Humanoid bone is missing: {bone}");
        Debug.Log($"[FullBodyAvatarSetup] {bone} -> {result.name} (expected Kyle source name: {expectedName})");
        return result;
    }

    private static void RequireTransforms(string label, params Transform[] transforms)
    {
        if (transforms.Any(value => value == null))
            throw new InvalidOperationException($"Humanoid transforms could not be resolved for {label}.");
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        var existing = gameObject.GetComponent<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        for (var index = 0; index < parent.childCount; index++)
            if (parent.GetChild(index).name == name)
                return parent.GetChild(index);

        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
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
            if (child.name == name)
                return child;
        return null;
    }
}
