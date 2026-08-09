using UnityEngine;

[DisallowMultipleComponent]
public sealed class VerticalFloatUI : MonoBehaviour
{
    [SerializeField, Min(0f)] private float amplitude = 20f;
    [SerializeField, Min(0f)] private float frequency = 0.3f;
    [SerializeField] private float phase;
    [SerializeField] private bool useUnscaledTime = true;

    private RectTransform rectTransform;
    private Vector2 baseAnchoredPosition;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
    }

    private void OnEnable()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;

        baseAnchoredPosition = rectTransform.anchoredPosition;
    }

    private void Update()
    {
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float offset = Mathf.Sin((time * frequency + phase) * Mathf.PI * 2f) * amplitude;
        rectTransform.anchoredPosition = baseAnchoredPosition + Vector2.up * offset;
    }

    private void OnDisable()
    {
        if (rectTransform != null)
            rectTransform.anchoredPosition = baseAnchoredPosition;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        frequency = Mathf.Max(0f, frequency);
    }
#endif
}
