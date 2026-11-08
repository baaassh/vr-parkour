
using UnityEngine;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(CharacterController))]
public class ArmSwingMovement : MonoBehaviour
{
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private Transform head;

    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float acceleration = 9f;
    [SerializeField] private float deceleration = 1.5f;

    [SerializeField] private float turnSpeed = Mathf.PI;

    [SerializeField] private InputActionReference jumpAction;

    [SerializeField] private float jumpSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;

    private float verticalVelocity = 0f;

    private Vector3 previousLeft;
    private Vector3 previousRight;

    private float currentSpeed = 0f;
    private CharacterController controller;

    private Vector3 moveDirection = Vector3.zero;
    public event Action<float> OnArmSwing;

    public float HorizontalSpeed => currentSpeed;
    public float VerticalVelocity => verticalVelocity;
    public bool IsGrounded => controller != null && controller.isGrounded;

    [SerializeField] private float swingEventInterval = 0.3f;

    private float lastSwingEventTime = -10f;
    private int lastSwingSide = 0;


    void OnEnable()
    {
        if (jumpAction != null && jumpAction.action != null)
        {
            jumpAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (jumpAction != null && jumpAction.action != null)
        {
            jumpAction.action.Disable();
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (leftHand != null)
            previousLeft = leftHand.localPosition;
        if (rightHand != null)
            previousRight = rightHand.localPosition;
    }

    void Update()
    {
        if (Time.deltaTime <= 0f) return;
        if (leftHand == null || rightHand == null || head == null || controller == null) return;

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
            Mathf.Abs(leftVelocity.z) > 0.2f &&
            Mathf.Abs(rightVelocity.z) > 0.2f;

        Vector3 direction = head.forward;
        direction.y = 0f;
        direction.Normalize();


        bool isRunning = isSwinging;
        if(isRunning)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
            if (moveDirection == Vector3.zero) {
                moveDirection = direction;
            }
            else {
                moveDirection = Vector3.RotateTowards(moveDirection, direction, turnSpeed * Time.deltaTime,0f);
            }
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
        }

        if (isRunning)
        {
            int swingSide = leftVelocity.z > 0f ? 1 : -1;

            if (swingSide != lastSwingSide &&
                Time.time - lastSwingEventTime >= swingEventInterval)
            {
                lastSwingSide = swingSide;
                lastSwingEventTime = Time.time;

                float intensity = Mathf.Clamp01(currentSpeed / maxSpeed);

                OnArmSwing?.Invoke(intensity);
            }
        }

        bool jumpPressed =
    jumpAction != null &&
    jumpAction.action.WasPressedThisFrame();

        
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }


        if (controller.isGrounded && jumpPressed)
        {
            verticalVelocity = jumpSpeed;
        }

        verticalVelocity += gravity * Time.deltaTime;


        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);

    }
}



