using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PvOnlyBossEndCinematicUI : MonoBehaviour
{
    private const string GridSpeedProperty = "_Speed";
    private const string GridVerticalOffsetProperty = "_GridVerticalOffset";
    private const string GridFallingModeProperty = "_FallingMode";
    private const string GridFallHorizonOffsetProperty = "_FallHorizonOffset";
    private const string GridPerspectivePowerProperty = "_PerspectivePower";
    private const string GridRotationAngleProperty = "_GridRotationAngle";
    private const string VhsIntensityProperty = "_Intensity";

    [Header("References")]
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private RectTransform crashPopup;
    [SerializeField] private CanvasGroup crashPopupCanvasGroup;
    [SerializeField] private RawImage gridBackground;
    [SerializeField] private RectTransform screenShakeContentRoot;
    [SerializeField] private RectTransform handArea;
    [SerializeField] private RectTransform[] closingTargets;
    [SerializeField] private RectTransform playerPortrait;

    [Header("Server Crash Popup")]
    [SerializeField, Min(0f)] private float crashHoldDuration = 0.65f;
    [SerializeField, Min(0f)] private float crashShakeDuration = 0.28f;
    [SerializeField] private float crashShakeStrength = 52f;
    [SerializeField] private int crashShakeVibrato = 22;

    [Header("UI Shutdown")]
    [SerializeField, Min(0f)] private float closeTargetStagger = 0.045f;
    [SerializeField, Min(0f)] private float collapseDuration = 0.32f;
    [SerializeField, Range(0.01f, 1f)] private float collapseScale = 0.08f;
    [SerializeField] private AnimationCurve collapseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] private float playerPortraitExtraDelay = 0.08f;

    [Header("Grid Fall")]
    [SerializeField, Min(0f)] private float fallAccelerationDuration = 1.8f;
    [SerializeField, Min(0f)] private float gridRotationSpeed = 90f;
    [SerializeField] private AnimationCurve gridRotationSpeedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fall VHS")]
    [SerializeField] private Material fallVhsMaterial;
    [SerializeField] private AnimationCurve fallVhsIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.72f);

    private Material gridMaterial;
    private Material vhsMaterial;
    private float initialGridSpeed;
    private float initialGridVerticalOffset;
    private float initialGridFallingMode;
    private float initialGridFallHorizonOffset;
    private float initialGridPerspectivePower;
    private float initialGridRotationAngle;
    private float fallElapsed;
    private bool isFalling;
    private bool played;

    private void Awake()
    {
        PrepareMaterials();
        ResetOverlay();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
            ShowCrashImpact();
        if (Input.GetKeyDown(KeyCode.I))
            StartUiShutdown();
        if (Input.GetKeyDown(KeyCode.O))
            StartFallEffects();

        UpdateFall();
    }

    private void OnDestroy()
    {
        if (gridMaterial != null)
            Destroy(gridMaterial);
        if (vhsMaterial != null)
            Destroy(vhsMaterial);
        if (PvOnlyVhsFallRendererFeature.RuntimeMaterial == vhsMaterial)
            PvOnlyVhsFallRendererFeature.RuntimeMaterial = null;
    }

    public void ShowCrashImpact()
    {
        if (played)
            return;

        played = true;
        PrepareMaterials();
        ResetOverlay();
        if (crashPopup != null)
            crashPopup.gameObject.SetActive(true);
        if (crashPopupCanvasGroup != null)
            crashPopupCanvasGroup.alpha = 1f;

        if (screenShakeContentRoot == null)
            return;

        for (int i = 0; i < screenShakeContentRoot.childCount; i++)
        {
            RectTransform target = screenShakeContentRoot.GetChild(i) as RectTransform;
            if (target == null || !target.gameObject.activeInHierarchy)
                continue;

            target.DOKill(false);
            target.DOShakeAnchorPos(crashShakeDuration, crashShakeStrength, crashShakeVibrato, 90f, false, true)
                .SetUpdate(true)
                .SetTarget(this);
        }
    }

    public IEnumerator Play()
    {
        if (!played)
            ShowCrashImpact();

        yield return new WaitForSecondsRealtime(crashHoldDuration);
        StartUiShutdown();
        StartFallEffects();
        if (crashPopupCanvasGroup != null)
            crashPopupCanvasGroup.DOFade(0f, collapseDuration).SetUpdate(true).SetTarget(this);
    }

    private void PrepareMaterials()
    {
        if (gridBackground != null && gridMaterial == null && gridBackground.material != null)
        {
            gridMaterial = new Material(gridBackground.material);
            initialGridSpeed = gridMaterial.HasProperty(GridSpeedProperty) ? gridMaterial.GetFloat(GridSpeedProperty) : 0f;
            initialGridVerticalOffset = gridMaterial.HasProperty(GridVerticalOffsetProperty) ? gridMaterial.GetFloat(GridVerticalOffsetProperty) : 0f;
            initialGridFallingMode = gridMaterial.HasProperty(GridFallingModeProperty) ? gridMaterial.GetFloat(GridFallingModeProperty) : 0f;
            initialGridFallHorizonOffset = gridMaterial.HasProperty(GridFallHorizonOffsetProperty) ? gridMaterial.GetFloat(GridFallHorizonOffsetProperty) : 0f;
            initialGridPerspectivePower = gridMaterial.HasProperty(GridPerspectivePowerProperty) ? gridMaterial.GetFloat(GridPerspectivePowerProperty) : 1f;
            initialGridRotationAngle = gridMaterial.HasProperty(GridRotationAngleProperty) ? gridMaterial.GetFloat(GridRotationAngleProperty) : 0f;
            gridBackground.material = gridMaterial;
        }

        if (fallVhsMaterial != null && vhsMaterial == null)
        {
            vhsMaterial = new Material(fallVhsMaterial);
            vhsMaterial.SetFloat(VhsIntensityProperty, 0f);
            PvOnlyVhsFallRendererFeature.RuntimeMaterial = vhsMaterial;
        }
    }

    private void ResetOverlay()
    {
        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = 0f;
        if (crashPopup != null)
            crashPopup.gameObject.SetActive(false);
        if (crashPopupCanvasGroup != null)
            crashPopupCanvasGroup.alpha = 0f;
        if (gridMaterial != null)
        {
            if (gridMaterial.HasProperty(GridVerticalOffsetProperty))
                gridMaterial.SetFloat(GridVerticalOffsetProperty, initialGridVerticalOffset);
            if (gridMaterial.HasProperty(GridFallingModeProperty))
                gridMaterial.SetFloat(GridFallingModeProperty, initialGridFallingMode);
            if (gridMaterial.HasProperty(GridFallHorizonOffsetProperty))
                gridMaterial.SetFloat(GridFallHorizonOffsetProperty, initialGridFallHorizonOffset);
            if (gridMaterial.HasProperty(GridPerspectivePowerProperty))
                gridMaterial.SetFloat(GridPerspectivePowerProperty, initialGridPerspectivePower);
            if (gridMaterial.HasProperty(GridRotationAngleProperty))
                gridMaterial.SetFloat(GridRotationAngleProperty, initialGridRotationAngle);
        }
        if (vhsMaterial != null)
            vhsMaterial.SetFloat(VhsIntensityProperty, 0f);
        isFalling = false;
        fallElapsed = 0f;
    }

    private void StartUiShutdown()
    {
        float latestDelay = 0f;
        float delayRange = closeTargetStagger * closingTargets.Length;
        for (int i = 0; i < closingTargets.Length; i++)
        {
            RectTransform target = closingTargets[i];
            if (target == null || !target.gameObject.activeInHierarchy)
                continue;

            float delay = Random.Range(0f, delayRange);
            latestDelay = Mathf.Max(latestDelay, delay);
            CollapseTarget(target, delay);
        }

        if (handArea != null)
        {
            for (int i = 0; i < handArea.childCount; i++)
            {
                RectTransform card = handArea.GetChild(i) as RectTransform;
                if (card == null || !card.gameObject.activeInHierarchy)
                    continue;

                float delay = Random.Range(0f, delayRange);
                latestDelay = Mathf.Max(latestDelay, delay);
                CollapseTarget(card, delay);
            }
        }

        if (playerPortrait != null && playerPortrait.gameObject.activeInHierarchy)
            CollapseTarget(playerPortrait, latestDelay + collapseDuration + playerPortraitExtraDelay);
    }

    private void CollapseTarget(RectTransform target, float delay)
    {
        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Vector3 startScale = target.localScale;
        canvasGroup.DOFade(0f, collapseDuration)
            .SetDelay(delay)
            .SetEase(collapseCurve)
            .SetUpdate(true)
            .SetTarget(this);
        target.DOScale(startScale * collapseScale, collapseDuration)
            .SetDelay(delay)
            .SetEase(collapseCurve)
            .SetUpdate(true)
            .SetTarget(this);
    }

    private void StartFallEffects()
    {
        if (gridMaterial == null)
            return;

        if (gridMaterial.HasProperty(GridSpeedProperty))
            gridMaterial.SetFloat(GridSpeedProperty, initialGridSpeed);
        if (gridMaterial.HasProperty(GridRotationAngleProperty))
            gridMaterial.SetFloat(GridRotationAngleProperty, initialGridRotationAngle);

        fallElapsed = 0f;
        isFalling = true;
    }

    private void UpdateFall()
    {
        if (!isFalling || gridMaterial == null || !gridMaterial.HasProperty(GridRotationAngleProperty))
            return;

        fallElapsed += Time.unscaledDeltaTime;
        float normalizedTime = fallAccelerationDuration <= 0f
            ? 1f
            : Mathf.Clamp01(fallElapsed / fallAccelerationDuration);
        float speed = gridRotationSpeed * Mathf.Max(0f, gridRotationSpeedCurve.Evaluate(normalizedTime));
        float rotationAngle = gridMaterial.GetFloat(GridRotationAngleProperty) - speed * Time.unscaledDeltaTime;
        gridMaterial.SetFloat(GridRotationAngleProperty, rotationAngle);

        if (vhsMaterial != null)
            vhsMaterial.SetFloat(VhsIntensityProperty, Mathf.Clamp01(fallVhsIntensityCurve.Evaluate(normalizedTime)));
    }
}
