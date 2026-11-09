using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class BackflipVisuals : MonoBehaviour
{
    [SerializeField] private float flipDuration = 0.7f;
    [SerializeField] private InputActionReference backflipAction;

    private bool isFlipping = false;
    private Quaternion initialRotation;

    void OnEnable()
    {
        backflipAction?.action?.Enable();
    }

    void OnDisable()
    {
        backflipAction?.action?.Disable();
    }

    void Start()
    {
        Debug.Log("BackflipVisuals 起動成功！");
    }

    void Update()
    {
        if (Time.frameCount % 120 == 0)
        {
            Debug.Log("BackflipVisuals 動作中！");
        }

        if (backflipAction == null || backflipAction.action == null)
        {
            Debug.LogWarning("Backflip Actionが未設定！");
            return;
        }

        if (backflipAction.action.WasPressedThisFrame() && !isFlipping)
        {
            Debug.Log("右スティック下入力を検出！");
            StartCoroutine(DoBackflip());
        }
    }

    IEnumerator DoBackflip()
    {
        isFlipping = true;

        initialRotation = transform.localRotation;
        float elapsed = 0f;

        while (elapsed < flipDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / flipDuration);
            float angle = -2520f * t;

            transform.localRotation =
                initialRotation * Quaternion.Euler(angle, 0f, 0f);

            yield return null;
        }

        transform.localRotation = initialRotation;
        isFlipping = false;
    }
}