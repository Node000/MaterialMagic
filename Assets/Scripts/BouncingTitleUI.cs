using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

/// <summary>
/// 开始界面标题的漂浮动画。
/// 注意：不再在 Awake 里替换标题 Image 的材质。溶解材质（M_StartTitlePixelParticleDissolve）的 shader
/// 不消费 uv1/uv2/uv3，一旦替换就会顶掉美术的蜡笔材质（cray.mat / UI/ProceduralCrayon_Edge）与
/// CrayonUIEdge，使标题在 Play Mode 下失去蜡笔效果；而且配置界面现在是弹窗叠在菜单上，也不需要靠溶解隐藏标题。
/// 溶解相关引用保留在 Inspector 里以便日后需要时手动启用（useDissolveMaterial 默认关闭）。
/// </summary>
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
    [Tooltip("默认关闭：替换材质会顶掉标题的蜡笔材质。开启后才会使用 dissolveMaterialTemplate 做溶解。")]
    [SerializeField] private bool useDissolveMaterial;

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
        // 默认不换材质：保留美术为标题配置的蜡笔材质，标题在 Play Mode 下与 Edit Mode 外观一致。
        if (useDissolveMaterial && image != null && dissolveMaterialTemplate != null)
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
