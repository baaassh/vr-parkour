using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[DefaultExecutionOrder(-500)]
public sealed class VRFullBodyAvatar : MonoBehaviour
{
    [Header("Tracked poses (read only; never moved by this component)")]
    [SerializeField] private Transform hmd;
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private ArmSwingMovement movement;

    [Header("Body estimation")]
    [SerializeField] private float bodyBehindHead = 0.12f;
    [SerializeField] private float rootPositionSharpness = 18f;
    [SerializeField] private float rootRotationSharpness = 12f;
    [SerializeField] private float crouchInfluence = 0.65f;
    [SerializeField] private float maximumCrouchOffset = 0.65f;

    [Header("Local-player visibility")]
    [SerializeField] private bool hideHeadFromHmd = true;
    [SerializeField, Range(0.001f, 0.2f)] private float hiddenHeadScale = 0.01f;

    [Header("Grounded feet")]
    [SerializeField] private bool enableFootGrounding = true;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float footRayHeight = 0.35f;
    [SerializeField] private float footRayDistance = 0.8f;
    [SerializeField] private float footGroundOffset = 0.025f;
    [SerializeField] private float footIkSharpness = 14f;
    [SerializeField] private float maximumFootIkSpeed = 5.5f;

    private Animator animator;
    private Transform hips;
    private Transform chest;
    private Transform headBone;
    private Transform leftFoot;
    private Transform rightFoot;
    private TwoBoneIKConstraint leftLegConstraint;
    private TwoBoneIKConstraint rightLegConstraint;
    private Transform leftFootTarget;
    private Transform rightFootTarget;

    private readonly HashSet<int> animatorParameters = new HashSet<int>();
    private Vector3 originalHeadScale;
    private float standingHeadHeight;
    private Quaternion headTrackingOffset;
    private Quaternion chestTrackingOffset;
    private bool calibrated;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");

    public void Configure(
        Transform trackedHmd,
        Transform trackedLeftController,
        Transform trackedRightController,
        CharacterController controller,
        ArmSwingMovement armSwingMovement)
    {
        hmd = trackedHmd;
        leftController = trackedLeftController;
        rightController = trackedRightController;
        characterController = controller;
        movement = armSwingMovement;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            enabled = false;
            Debug.LogError("[VRFullBodyAvatar] KyleRobot needs a Humanoid Animator.", this);
            return;
        }

        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
        headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        foreach (var parameter in animator.parameters)
            animatorParameters.Add(parameter.nameHash);

        originalHeadScale = headBone != null ? headBone.localScale : Vector3.one;
        ResolveRigObjects();
    }

    private void Start()
    {
        TryCalibrate();
    }

    private void OnDisable()
    {
        if (headBone != null)
            headBone.localScale = originalHeadScale;

        if (leftLegConstraint != null)
            leftLegConstraint.weight = 0f;
        if (rightLegConstraint != null)
            rightLegConstraint.weight = 0f;
    }

    private void Update()
    {
        if (!calibrated)
            TryCalibrate();

        if (hmd == null)
            return;

        UpdateEstimatedRoot();
        UpdateAnimatorParameters();
        UpdateFootGrounding();
    }

    private void LateUpdate()
    {
        if (!calibrated || hmd == null)
            return;

        ApplyUpperBodyTracking();
        ApplyCrouchEstimate();

        if (headBone != null)
            headBone.localScale = hideHeadFromHmd ? Vector3.one * hiddenHeadScale : originalHeadScale;
    }

    private void TryCalibrate()
    {
        if (hmd == null || headBone == null || hips == null)
            return;

        standingHeadHeight = Mathf.Max(0.8f, Vector3.Dot(hmd.position - transform.position, transform.up));
        headTrackingOffset = Quaternion.Inverse(hmd.rotation) * headBone.rotation;
        chestTrackingOffset = chest != null ? Quaternion.Inverse(hmd.rotation) * chest.rotation : Quaternion.identity;
        calibrated = true;
    }

    private void UpdateEstimatedRoot()
    {
        var flatForward = Vector3.ProjectOnPlane(hmd.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = transform.forward;
        flatForward.Normalize();

        var groundY = characterController != null ? characterController.transform.position.y : transform.position.y;
        var desiredPosition = hmd.position - flatForward * bodyBehindHead;
        desiredPosition.y = groundY;

        var positionBlend = 1f - Mathf.Exp(-rootPositionSharpness * Time.deltaTime);
        var rotationBlend = 1f - Mathf.Exp(-rootRotationSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatForward, Vector3.up), rotationBlend);
    }

    private void UpdateAnimatorParameters()
    {
        if (animator == null)
            return;

        var horizontalSpeed = movement != null
            ? movement.HorizontalSpeed
            : characterController != null
                ? Vector3.ProjectOnPlane(characterController.velocity, Vector3.up).magnitude
                : 0f;
        var grounded = movement != null ? movement.IsGrounded : characterController != null && characterController.isGrounded;
        var verticalVelocity = movement != null ? movement.VerticalVelocity : characterController != null ? characterController.velocity.y : 0f;

        SetFloat(SpeedHash, horizontalSpeed);
        SetFloat(MotionSpeedHash, horizontalSpeed > 0.05f ? 1f : 0f);
        SetBool(GroundedHash, grounded);
        SetBool(JumpHash, !grounded && verticalVelocity > 0.1f);
        SetBool(FreeFallHash, !grounded && verticalVelocity < -0.1f);
    }

    private void ApplyUpperBodyTracking()
    {
        if (chest != null)
        {
            var trackedChestRotation = hmd.rotation * chestTrackingOffset;
            chest.rotation = Quaternion.Slerp(chest.rotation, trackedChestRotation, 0.35f);
        }

        if (headBone != null)
            headBone.rotation = hmd.rotation * headTrackingOffset;
    }

    private void ApplyCrouchEstimate()
    {
        if (hips == null)
            return;

        var currentHeadHeight = Vector3.Dot(hmd.position - transform.position, transform.up);
        var crouch = Mathf.Clamp((currentHeadHeight - standingHeadHeight) * crouchInfluence, -maximumCrouchOffset, 0.15f);
        hips.position += transform.up * crouch;
    }

    private void UpdateFootGrounding()
    {
        var horizontalSpeed = movement != null ? movement.HorizontalSpeed : 0f;
        var grounded = movement != null ? movement.IsGrounded : characterController != null && characterController.isGrounded;
        var requestedWeight = enableFootGrounding && grounded && horizontalSpeed <= maximumFootIkSpeed ? 1f : 0f;
        var blend = 1f - Mathf.Exp(-footIkSharpness * Time.deltaTime);

        UpdateFoot(leftFoot, leftFootTarget, leftLegConstraint, requestedWeight, blend);
        UpdateFoot(rightFoot, rightFootTarget, rightLegConstraint, requestedWeight, blend);
    }

    private void UpdateFoot(Transform foot, Transform target, TwoBoneIKConstraint constraint, float requestedWeight, float blend)
    {
        if (foot == null || target == null || constraint == null)
            return;

        var rayOrigin = foot.position + Vector3.up * footRayHeight;
        var hasGround = Physics.Raycast(rayOrigin, Vector3.down, out var hit, footRayHeight + footRayDistance, groundLayers, QueryTriggerInteraction.Ignore);
        var desiredWeight = hasGround ? requestedWeight : 0f;
        constraint.weight = Mathf.Lerp(constraint.weight, desiredWeight, blend);

        if (!hasGround)
            return;

        var desiredPosition = hit.point + hit.normal * footGroundOffset;
        var forward = Vector3.ProjectOnPlane(foot.forward, hit.normal);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(transform.forward, hit.normal);
        var desiredRotation = Quaternion.LookRotation(forward.normalized, hit.normal);
        target.SetPositionAndRotation(
            Vector3.Lerp(target.position, desiredPosition, blend),
            Quaternion.Slerp(target.rotation, desiredRotation, blend));
    }

    private void ResolveRigObjects()
    {
        leftLegConstraint = FindNamedComponent<TwoBoneIKConstraint>("LeftLegIK");
        rightLegConstraint = FindNamedComponent<TwoBoneIKConstraint>("RightLegIK");
        leftFootTarget = FindNamedTransform("LeftFootIKTarget");
        rightFootTarget = FindNamedTransform("RightFootIKTarget");
    }

    private T FindNamedComponent<T>(string objectName) where T : Component
    {
        foreach (var component in GetComponentsInChildren<T>(true))
            if (component.name == objectName)
                return component;
        return null;
    }

    private Transform FindNamedTransform(string objectName)
    {
        foreach (var child in GetComponentsInChildren<Transform>(true))
            if (child.name == objectName)
                return child;
        return null;
    }

    private void SetFloat(int hash, float value)
    {
        if (animatorParameters.Contains(hash))
            animator.SetFloat(hash, value, 0.1f, Time.deltaTime);
    }

    private void SetBool(int hash, bool value)
    {
        if (animatorParameters.Contains(hash))
            animator.SetBool(hash, value);
    }
}
