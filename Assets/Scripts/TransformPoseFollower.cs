using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class TransformPoseFollower : MonoBehaviour
{
    [SerializeField] private Transform source;
    [SerializeField] private Vector3 localPositionOffset;
    [SerializeField] private Vector3 localRotationOffset;

    public Transform Source
    {
        get => source;
        set => source = value;
    }

    public Vector3 LocalPositionOffset
    {
        get => localPositionOffset;
        set => localPositionOffset = value;
    }

    public Vector3 LocalRotationOffset
    {
        get => localRotationOffset;
        set => localRotationOffset = value;
    }

    private void Update()
    {
        if (source == null)
            return;

        transform.SetPositionAndRotation(
            source.TransformPoint(localPositionOffset),
            source.rotation * Quaternion.Euler(localRotationOffset));
    }
}
