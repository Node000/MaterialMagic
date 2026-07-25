using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PvOnlyBossEndCinematicUI : MonoBehaviour
{
    private const string GridSpeedProperty = "_Speed";
    private const string GridVerticalOffsetProperty = "_GridVerticalOffset";
    private const string GlitchAmountProperty = "_DurationAmount";

    [Header("References")]
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private RectTransform crashPopup;
    [SerializeField] private CanvasGroup crashPopupCanvasGroup;
    [SerializeField] private TMP_Text crashTitleText;
    [SerializeField] private TMP_Text crashWarningText;
    [SerializeField] private RawImage glitchOverlay;
    [SerializeField] private RawImage gridBackground;
    [SerializeField] private RectTransform screenShakeRoot;
    [SerializeField] private RectTransform[] closingTargets;

    [Header("Server Crash Popup")]
    [SerializeField] private string crashMessage = "Server Crash!!!";
    [SerializeField] private string warningMessage = "⚠";
    [SerializeField, Min(0f)] private float crashHoldDuration = 0.65f;
    [SerializeField, Min(0f)] private float crashShakeDuration = 0.18f;
    [SerializeField] private float crashShakeStrength = 36f;
    [SerializeField] private int crashShakeVibrato = 16;

    [Header("UI Shutdown")]
    [SerializeField, Min(0f)] private float closeTargetStagger = 0.045f;
    [SerializeField, Min(0f)] private float collapseDuration = 0.16f;
    [SerializeField, Range(0.01f, 1f)] private float collapseScale = 0.08f;
    [SerializeField, Range(0f, 1f)] private float fallStartProgress = 0.5f;
    [SerializeField, Min(0f)] private float closeDuration = 0.42f;
    [FormerlySerializedAs("closeFallDistance")]
    [SerializeField] private float closeRiseDistance = 420f;
    [SerializeField] private AnimationCurve uiRiseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fall Grid")]
    [SerializeField, Min(0f)] private float fallDuration = 1.8f;
    [FormerlySerializedAs("gridVerticalSweep")]
    [SerializeField] private float gridScreenSweep = 1.6f;
    [SerializeField] private float screenShakeStrength = 18f;
    [SerializeField] private int screenShakeVibrato = 24;

    [Header("VHS")]
    [SerializeField, Min(0f)] private float vhsRampDuration = 1.2f;
    [SerializeField] private AnimationCurve vhsIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] private float vhsFadeOutDuration = 0.45f;

    private Material gridMaterial;
    private Material glitchMaterial;
    private float initialGridSpeed;
    private float initialGridVerticalOffset;
    private bool played;

    private void Awake()
    {
        PrepareMaterials();
        ResetOverlay();
    }

    private void OnDestroy()
    {
        if (gridMaterial != null)
            Destroy(gridMaterial);
        if (glitchMaterial != null)
            Destroy(glitchMaterial);
    }

    public void ShowCrashImpact()
    {
        if (played)
            return;

        played = true;
        PrepareMaterials();
        ResetOverlay();
        if (overlayRoot != null)
            overlayRoot.gameObject.SetActive(true);
        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = 1f;
        if (crashPopup != null)
            crashPopup.gameObject.SetActive(true);
        if (crashPopupCanvasGroup != null)
            crashPopupCanvasGroup.alpha = 1f;
        if (crashTitleText != null)
            crashTitleText.text = crashMessage;
        if (crashWarningText != null)
            crashWarningText.text = warningMessage;

        if (screenShakeRoot != null)
            screenShakeRoot.DOShakeAnchorPos(crashShakeDuration, crashShakeStrength, crashShakeVibrato, 90f, false, true)
                .SetUpdate(true)
                .SetTarget(this);
    }

    public IEnumerator Play()
    {
        if (!played)
            ShowCrashImpact();

        yield return new WaitForSecondsRealtime(crashHoldDuration);
        StartUiShutdown();
        StartFallEffects();
        if (crashPopupCanvasGroup != null)
            crashPopupCanvasGroup.DOFade(0f, closeDuration).SetUpdate(true).SetTarget(this);

        float shutdownDuration = closeDuration + Mathf.Max(0, closingTargets.Length - 1) * closeTargetStagger;
        float vhsDuration = Mathf.Max(fallDuration, vhsRampDuration);
        yield return new WaitForSecondsRealtime(Mathf.Max(shutdownDuration, vhsDuration));

        if (overlayCanvasGroup != null)
            yield return overlayCanvasGroup.DOFade(0f, vhsFadeOutDuration).SetUpdate(true).WaitForCompletion();

        if (gridMaterial != null)
        {
            gridMaterial.SetFloat(GridSpeedProperty, initialGridSpeed);
            if (gridMaterial.HasProperty(GridVerticalOffsetProperty))
                gridMaterial.SetFloat(GridVerticalOffsetProperty, initialGridVerticalOffset);
        }
        if (overlayRoot != null)
            overlayRoot.gameObject.SetActive(false);
    }

    private void PrepareMaterials()
    {
        if (gridBackground != null && gridMaterial == null && gridBackground.material != null)
        {
            gridMaterial = new Material(gridBackground.material);
            initialGridSpeed = gridMaterial.HasProperty(GridSpeedProperty) ? gridMaterial.GetFloat(GridSpeedProperty) : 0f;
            initialGridVerticalOffset = gridMaterial.HasProperty(GridVerticalOffsetProperty) ? gridMaterial.GetFloat(GridVerticalOffsetProperty) : 0f;
            gridBackground.material = gridMaterial;
        }

        if (glitchOverlay != null && glitchMaterial == null && glitchOverlay.material != null)
        {
            glitchMaterial = new Material(glitchOverlay.material);
            glitchOverlay.material = glitchMaterial;
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
        if (gridMaterial != null && gridMaterial.HasProperty(GridVerticalOffsetProperty))
            gridMaterial.SetFloat(GridVerticalOffsetProperty, initialGridVerticalOffset);
        if (glitchMaterial != null && glitchMaterial.HasProperty(GlitchAmountProperty))
            glitchMaterial.SetFloat(GlitchAmountProperty, 0f);
    }

    private void StartUiShutdown()
    {
        for (int i = 0; i < closingTargets.Length; i++)
        {
            RectTransform target = closingTargets[i];
            if (target == null || !target.gameObject.activeInHierarchy)
                continue;

            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            float delay = i * closeTargetStagger;
            float fallDelay = delay + collapseDuration * fallStartProgress;
            canvasGroup.DOFade(0f, closeDuration).SetDelay(delay).SetUpdate(true).SetTarget(this);
            target.DOScale(Vector3.one * collapseScale, collapseDuration)
                .SetDelay(delay)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .SetTarget(this);
            target.DOAnchorPosY(target.anchoredPosition.y + closeRiseDistance, closeDuration)
                .SetDelay(fallDelay)
                .SetEase(uiRiseCurve)
                .SetUpdate(true)
                .SetTarget(this);
        }
    }

    private void StartFallEffects()
    {
        if (gridMaterial != null && gridMaterial.HasProperty(GridSpeedProperty))
            gridMaterial.SetFloat(GridSpeedProperty, 0f);

        if (gridMaterial != null && gridMaterial.HasProperty(GridVerticalOffsetProperty))
        {
            float sweepStart = initialGridVerticalOffset;
            float sweepEnd = initialGridVerticalOffset + gridScreenSweep;
            gridMaterial.SetFloat(GridVerticalOffsetProperty, sweepStart);
            DOTween.To(() => gridMaterial.GetFloat(GridVerticalOffsetProperty), value => gridMaterial.SetFloat(GridVerticalOffsetProperty, value), sweepEnd, fallDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .SetTarget(this);
        }

        if (glitchMaterial != null && glitchMaterial.HasProperty(GlitchAmountProperty))
        {
            DOTween.To(() => 0f, value => glitchMaterial.SetFloat(GlitchAmountProperty, vhsIntensityCurve.Evaluate(value)), 1f, vhsRampDuration)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetTarget(this);
        }

        if (screenShakeRoot != null)
            screenShakeRoot.DOShakeAnchorPos(fallDuration, screenShakeStrength, screenShakeVibrato, 90f, false, true)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .SetTarget(this);
    }
}
