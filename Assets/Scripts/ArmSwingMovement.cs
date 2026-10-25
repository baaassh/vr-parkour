
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ArmSwingMovement : MonoBehaviour
{
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private Transform head;

    [SerializeField] private float maxSpeed = 7f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 5f;

    private Vector3 previousLeft;
    private Vector3 previousRight;

    private float currentSpeed = 0f;
    private CharacterController controller;

    private float lastJumpTime = -10f;
    private float jumpCooldown = 0.5f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        previousLeft = leftHand.localPosition;
        previousRight = rightHand.localPosition;
    }

    void Update()
    {
        if (Time.deltaTime <= 0f) return;

        Vector3 leftVelocity =
            (leftHand.localPosition - previousLeft)
            / Time.deltaTime;

        Vector3 rightVelocity =
            (rightHand.localPosition - previousRight)
            / Time.deltaTime;

        previousLeft = leftHand.localPosition;
        previousRight = rightHand.localPosition;


        bool isAlternating =
    leftVelocity.z * rightVelocity.z < 0f;

        bool isSwinging =
            Mathf.Abs(leftVelocity.z) > 0.4f &&
            Mathf.Abs(rightVelocity.z) > 0.4f;

        float targetSpeed = 0f;
        float armSpeed =
           (leftVelocity.magnitude +
            rightVelocity.magnitude) / 2f;

        if (isAlternating && isSwinging)
        {
            targetSpeed = Mathf.Clamp(
                armSpeed * 2f, 0f, maxSpeed);
        };

        float changeRate =
            targetSpeed > currentSpeed
            ? acceleration
            : deceleration;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            changeRate * Time.deltaTime
        );

        Vector3 direction = head.forward;
        direction.y = 0f;
        direction.Normalize();

        controller.Move(
            direction * currentSpeed * Time.deltaTime
        );

    }
}



