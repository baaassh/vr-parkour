using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class BackflipVisuals : MonoBehaviour
{
    [SerializeField] private float flipDuration = 0.7f;
    [SerializeField] private InputActionReference backflipAction;
    [SerializeField] private Transform avatar;

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

        Quaternion initialRotation = transform.localRotation;
        float elapsed = 0f;

        // 回転軸を開始時に固定する
        Vector3 worldAxis = avatar != null
            ? avatar.right
            : transform.right;

        Vector3 localAxis = transform.parent != null
            ? transform.parent.InverseTransformDirection(worldAxis)
            : worldAxis;

        while (elapsed < flipDuration)
        {
            elapsed += Time.deltaTime;

            // 0〜1の進行度
            float t = Mathf.Clamp01(elapsed / flipDuration);

            // 後方に360度回転
            float angle = -1200f * t;

            transform.localRotation =
                Quaternion.AngleAxis(angle, localAxis)
                * initialRotation;

            yield return null;
        }

        transform.localRotation = initialRotation;
        isFlipping = false;
    }
}