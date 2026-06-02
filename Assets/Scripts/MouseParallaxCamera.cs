using UnityEngine;

[DisallowMultipleComponent]
public class MouseParallaxCamera : MonoBehaviour
{
    [Header("鼠标转动")]
    [SerializeField] private float maxYawAngle = 1.2f;
    [SerializeField] private float maxPitchAngle = 0.8f;
    [SerializeField, Min(0f)] private float followSpeed = 6f;
    [SerializeField] private bool useUnscaledTime = true;

    private Quaternion baseLocalRotation;

    private void Awake()
    {
        baseLocalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        baseLocalRotation = transform.localRotation;
    }

    private void OnDisable()
    {
        transform.localRotation = baseLocalRotation;
    }

    private void Update()
    {
        Quaternion targetRotation = baseLocalRotation;

        if (Application.isFocused && Screen.width > 0 && Screen.height > 0)
        {
            Vector3 mousePosition = Input.mousePosition;
            float normalizedX = Mathf.Clamp(mousePosition.x / Screen.width * 2f - 1f, -1f, 1f);
            float normalizedY = Mathf.Clamp(mousePosition.y / Screen.height * 2f - 1f, -1f, 1f);
            targetRotation *= Quaternion.Euler(-normalizedY * maxPitchAngle, normalizedX * maxYawAngle, 0f);
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float blend = followSpeed <= 0f ? 1f : 1f - Mathf.Exp(-followSpeed * deltaTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, blend);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        followSpeed = Mathf.Max(0f, followSpeed);
    }
#endif
}
