using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BouncingTitleUI : MonoBehaviour
{
    [SerializeField] private Vector2 floatAmplitude = new Vector2(24f, 14f);
    [SerializeField, Min(0f)] private Vector2 floatFrequency = new Vector2(0.08f, 0.11f);
    [SerializeField] private Vector2 floatPhase;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private Image dissolveTargetImage;
    [SerializeField] private CanvasGroup dissolveTargetCanvasGroup;
    [SerializeField] private Material dissolveMaterialTemplate;
    [SerializeField, Min(0.01f)] private float dissolveDuration = 0.45f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Image image;
    private Material dissolveMaterial;
    private Tween dissolveTween;
    private Vector2 anchorPosition;
    private float floatTime;
    private bool anchorCaptured;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        CaptureAnchor();
        canvasGroup = dissolveTargetCanvasGroup != null ? dissolveTargetCanvasGroup : GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        image = dissolveTargetImage != null ? dissolveTargetImage : GetComponent<Image>();
        if (image != null && dissolveMaterialTemplate != null)
        {
            dissolveMaterial = new Material(dissolveMaterialTemplate);
            image.material = dissolveMaterial;
        }
    }

    private void OnEnable()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;
        CaptureAnchor();
    }

    private void Update()
    {
        if (floatAmplitude.sqrMagnitude <= 0f)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        floatTime += deltaTime;
        float xOffset = Mathf.Sin((floatTime * floatFrequency.x + floatPhase.x) * Mathf.PI * 2f) * floatAmplitude.x;
        float yOffset = Mathf.Sin((floatTime * floatFrequency.y + floatPhase.y) * Mathf.PI * 2f) * floatAmplitude.y;
        rectTransform.anchoredPosition = anchorPosition + new Vector2(xOffset, yOffset);
    }

    private void OnDestroy()
    {
        dissolveTween?.Kill(false);
        if (dissolveMaterial != null)
            Destroy(dissolveMaterial);
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        dissolveTween?.Kill(false);
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
        if (dissolveMaterial == null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            return;
        }

        float targetProgress = visible ? 0f : 1f;
        dissolveTween = DOTween.To(
                () => dissolveMaterial.GetFloat("_DissolveProgress"),
                value => dissolveMaterial.SetFloat("_DissolveProgress", value),
                targetProgress,
                dissolveDuration)
            .SetUpdate(true)
            .SetEase(Ease.OutCubic)
            .SetTarget(this);
    }

    private void CaptureAnchor()
    {
        if (!anchorCaptured)
        {
            anchorPosition = rectTransform.anchoredPosition;
            anchorCaptured = true;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        floatAmplitude.x = Mathf.Max(0f, floatAmplitude.x);
        floatAmplitude.y = Mathf.Max(0f, floatAmplitude.y);
        floatFrequency.x = Mathf.Max(0f, floatFrequency.x);
        floatFrequency.y = Mathf.Max(0f, floatFrequency.y);
        dissolveDuration = Mathf.Max(0.01f, dissolveDuration);
    }
#endif
}
