using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BackgroundHalftoneSweepUI : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Material materialTemplate;
    [Header("播放时序")]
    [SerializeField, Min(0f)] private float initialDelay = 4f;
    [SerializeField, Min(0f)] private float repeatInterval = 10f;
    [SerializeField, Min(0.01f)] private float sweepDuration = 1.5f;
    [SerializeField] private AnimationCurve sweepCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Header("半调样式")]
    [SerializeField, Range(0.01f, 1f)] private float sweepWidth = 0.24f;
    [SerializeField, Range(0.001f, 0.5f)] private float edgeSoftness = 0.08f;
    [SerializeField, Min(1f)] private float dotResolution = 96f;
    [SerializeField, Range(0.01f, 1f)] private float dotSize = 0.62f;
    [SerializeField, Range(0f, 1f)] private float dotShape;
    [SerializeField] private Color effectColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    [SerializeField, Range(0f, 1f)] private float effectStrength = 0.28f;
    [SerializeField] private bool useUnscaledTime = true;

    private Material runtimeMaterial;
    private float timer;
    private float sweepElapsed;
    private bool sweeping;

    private static readonly int SweepProgressId = Shader.PropertyToID("_SweepProgress");
    private static readonly int SweepWidthId = Shader.PropertyToID("_SweepWidth");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int DotResId = Shader.PropertyToID("_DotRes");
    private static readonly int DotSizeId = Shader.PropertyToID("_DotSize");
    private static readonly int DotShapeId = Shader.PropertyToID("_DotShape");
    private static readonly int EffectColorId = Shader.PropertyToID("_EffectColor");
    private static readonly int EffectStrengthId = Shader.PropertyToID("_EffectStrength");

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetImage != null && materialTemplate != null)
        {
            runtimeMaterial = new Material(materialTemplate);
            targetImage.material = runtimeMaterial;
            targetImage.raycastTarget = false;
            ApplyStyleProperties();
        }

        timer = initialDelay;
        SetSweepProgress(0f);
    }

    private void Update()
    {
        if (runtimeMaterial == null)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (!sweeping)
        {
            timer -= deltaTime;
            if (timer <= 0f)
            {
                sweeping = true;
                sweepElapsed = 0f;
            }
            return;
        }

        sweepElapsed += deltaTime;
        float normalizedTime = Mathf.Clamp01(sweepElapsed / sweepDuration);
        float curveValue = sweepCurve != null ? sweepCurve.Evaluate(normalizedTime) : normalizedTime;
        SetSweepProgress(curveValue);
        if (normalizedTime >= 1f)
        {
            sweeping = false;
            timer = repeatInterval;
            SetSweepProgress(1f);
        }
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    private void ApplyStyleProperties()
    {
        runtimeMaterial.SetFloat(SweepWidthId, sweepWidth);
        runtimeMaterial.SetFloat(EdgeSoftnessId, edgeSoftness);
        runtimeMaterial.SetFloat(DotResId, dotResolution);
        runtimeMaterial.SetFloat(DotSizeId, dotSize);
        runtimeMaterial.SetFloat(DotShapeId, dotShape);
        runtimeMaterial.SetColor(EffectColorId, effectColor);
        runtimeMaterial.SetFloat(EffectStrengthId, effectStrength);
    }

    private void SetSweepProgress(float progress)
    {
        if (runtimeMaterial != null)
            runtimeMaterial.SetFloat(SweepProgressId, progress);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        initialDelay = Mathf.Max(0f, initialDelay);
        repeatInterval = Mathf.Max(0f, repeatInterval);
        sweepDuration = Mathf.Max(0.01f, sweepDuration);
        sweepWidth = Mathf.Clamp(sweepWidth, 0.01f, 1f);
        edgeSoftness = Mathf.Clamp(edgeSoftness, 0.001f, 0.5f);
        dotResolution = Mathf.Max(1f, dotResolution);
        dotSize = Mathf.Clamp(dotSize, 0.01f, 1f);
        effectStrength = Mathf.Clamp01(effectStrength);
        if (Application.isPlaying && runtimeMaterial != null)
            ApplyStyleProperties();
    }
#endif
}
