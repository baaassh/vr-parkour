using UnityEngine;

public class RunBounceEffect : MonoBehaviour
{
    [SerializeField] private ArmSwingMovement movement;
    [SerializeField] private Transform visualTarget;

    [SerializeField] private float bounceDelay = 0.08f;
    [SerializeField] private float bounceDuration = 0.25f;
    [SerializeField] private float bounceHeight = 0.015f;

    private Vector3 originalLocalPosition;

    private float delayTimer;
    private float bounceTimer;
    private float intensity;

    private bool waiting;
    private bool bouncing;

    void Start()
    {
        if (visualTarget != null)
        {
            originalLocalPosition = visualTarget.localPosition;
        }
    }

    void OnEnable()
    {
        if (movement != null)
            movement.OnArmSwing += TriggerBounce;
    }

    void OnDisable()
    {
        if (movement != null)
            movement.OnArmSwing -= TriggerBounce;

        waiting = false;
        bouncing = false;

        if (visualTarget != null)
            visualTarget.localPosition = originalLocalPosition;
    }

    void TriggerBounce(float strength)
    {
        if (waiting || bouncing) return;

        intensity = strength;
        delayTimer = bounceDelay;
        waiting = true;
    }

    void LateUpdate()
    {
        if (visualTarget == null) return;

        if (waiting)
        {
            delayTimer -= Time.deltaTime;

            if (delayTimer <= 0f)
            {
                waiting = false;
                bouncing = true;
                bounceTimer = 0f;
            }
        }

        float offset = 0f;

        if (bouncing)
        {
            bounceTimer += Time.deltaTime;

            float t = Mathf.Clamp01(
                bounceTimer / Mathf.Max(0.01f, bounceDuration)
            );

            offset = Mathf.Sin(t * Mathf.PI)
                     * bounceHeight
                     * intensity;

            if (t >= 1f)
                bouncing = false;
        }

        visualTarget.localPosition =
            originalLocalPosition + Vector3.up * offset;
    }
}