using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StartConfigEnchantTransitionUI : MonoBehaviour
{
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private CanvasGroup targetCanvasGroup;
    [Header("临时附魔过场")]
    [SerializeField, Min(0.01f)] private float switchOutDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float switchInDuration = 0.24f;
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0.34f;
    [SerializeField] private float switchScale = 0.9f;
    [SerializeField] private float overshootScale = 1.04f;
    [SerializeField] private Ease switchOutEase = Ease.InCubic;
    [SerializeField] private Ease switchInEase = Ease.OutBack;
    [Header("附魔光效")]
    [SerializeField] private Color glowColor = new Color(0.45f, 0.95f, 1f, 0.58f);
    [SerializeField] private Color sweepColor = new Color(1f, 0.95f, 0.55f, 0.75f);
    [SerializeField] private float glowScale = 1.08f;
    [SerializeField] private float sweepWidth = 90f;
    [SerializeField] private float sweepHeightPadding = 180f;
    [SerializeField] private float sweepRotation = -18f;

    private RectTransform effectRect;
    private Image effectImage;
    private CanvasGroup effectCanvasGroup;
    private RectTransform sweepRect;
    private Image sweepImage;
    private Sequence switchSequence;
    private bool storedCanvasGroupState;
    private bool storedInteractable;
    private bool storedBlocksRaycasts;

    private void OnDestroy()
    {
        switchSequence?.Kill(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        switchOutDuration = Mathf.Max(0.01f, switchOutDuration);
        switchInDuration = Mathf.Max(0.01f, switchInDuration);
        hiddenAlpha = Mathf.Clamp01(hiddenAlpha);
        switchScale = Mathf.Max(0.01f, switchScale);
        overshootScale = Mathf.Max(0.01f, overshootScale);
        glowScale = Mathf.Max(0.01f, glowScale);
        sweepWidth = Mathf.Max(1f, sweepWidth);
        sweepHeightPadding = Mathf.Max(0f, sweepHeightPadding);
    }
#endif

    public void PlaySwitch(Action swapContent, Action onComplete)
    {
        ResolveReferences();
        EnsureEffectViews();
        Kill(false);

        if (targetRect == null)
        {
            swapContent?.Invoke();
            onComplete?.Invoke();
            return;
        }

        StoreCanvasGroupState();
        targetCanvasGroup.alpha = 1f;
        targetCanvasGroup.interactable = false;
        PrepareEffectViews();

        float sweepEndX = GetTargetWidth() * 0.5f + sweepWidth;
        switchSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        switchSequence.Append(targetRect.DOScale(Vector3.one * switchScale, switchOutDuration).SetEase(switchOutEase));
        switchSequence.Join(targetCanvasGroup.DOFade(hiddenAlpha, switchOutDuration).SetEase(Ease.OutQuad));
        switchSequence.Insert(0f, effectCanvasGroup.DOFade(1f, switchOutDuration * 0.55f).SetEase(Ease.OutQuad));
        switchSequence.Insert(0f, effectRect.DOScale(Vector3.one * glowScale, switchOutDuration + switchInDuration).SetEase(Ease.OutCubic));
        if (sweepRect != null)
            switchSequence.Insert(0f, sweepRect.DOAnchorPosX(sweepEndX, switchOutDuration + switchInDuration * 0.55f).SetEase(Ease.InOutSine));
        switchSequence.AppendCallback(() =>
        {
            swapContent?.Invoke();
            targetRect.localScale = Vector3.one * switchScale;
            targetCanvasGroup.alpha = hiddenAlpha;
        });
        switchSequence.Append(targetRect.DOScale(Vector3.one * overshootScale, switchInDuration * 0.58f).SetEase(switchInEase));
        switchSequence.Join(targetCanvasGroup.DOFade(1f, switchInDuration * 0.58f).SetEase(Ease.OutQuad));
        switchSequence.Join(effectCanvasGroup.DOFade(0f, switchInDuration).SetEase(Ease.InQuad));
        switchSequence.Append(targetRect.DOScale(Vector3.one, switchInDuration * 0.42f).SetEase(Ease.OutCubic));
        switchSequence.OnComplete(() =>
        {
            switchSequence = null;
            RestoreCanvasGroupState();
            HideEffectImmediate();
            onComplete?.Invoke();
        });
    }

    public void Kill(bool restoreTarget = true)
    {
        switchSequence?.Kill(false);
        switchSequence = null;
        if (restoreTarget && targetRect != null)
            targetRect.localScale = Vector3.one;
        if (restoreTarget && targetCanvasGroup != null)
            targetCanvasGroup.alpha = 1f;
        RestoreCanvasGroupState();
        HideEffectImmediate();
    }

    private void ResolveReferences()
    {
        if (targetRect == null)
            targetRect = transform as RectTransform;
        if (targetCanvasGroup == null && targetRect != null)
        {
            targetCanvasGroup = targetRect.GetComponent<CanvasGroup>();
            if (targetCanvasGroup == null)
                targetCanvasGroup = targetRect.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void EnsureEffectViews()
    {
        if (targetRect == null)
            return;

        if (effectRect == null)
        {
            Transform existing = targetRect.Find("TemporaryEnchantEffect");
            if (existing != null)
                effectRect = existing as RectTransform;
        }

        if (effectRect == null)
        {
            GameObject effectObject = new GameObject("TemporaryEnchantEffect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            effectObject.transform.SetParent(targetRect, false);
            effectRect = (RectTransform)effectObject.transform;
        }

        effectRect.anchorMin = Vector2.zero;
        effectRect.anchorMax = Vector2.one;
        effectRect.offsetMin = Vector2.zero;
        effectRect.offsetMax = Vector2.zero;
        effectRect.pivot = new Vector2(0.5f, 0.5f);

        effectImage = effectRect.GetComponent<Image>();
        if (effectImage == null)
            effectImage = effectRect.gameObject.AddComponent<Image>();
        effectImage.raycastTarget = false;

        effectCanvasGroup = effectRect.GetComponent<CanvasGroup>();
        if (effectCanvasGroup == null)
            effectCanvasGroup = effectRect.gameObject.AddComponent<CanvasGroup>();
        effectCanvasGroup.interactable = false;
        effectCanvasGroup.blocksRaycasts = false;

        if (sweepRect == null)
        {
            Transform existing = effectRect.Find("EnchantSweep");
            if (existing != null)
                sweepRect = existing as RectTransform;
        }

        if (sweepRect == null)
        {
            GameObject sweepObject = new GameObject("EnchantSweep", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            sweepObject.transform.SetParent(effectRect, false);
            sweepRect = (RectTransform)sweepObject.transform;
        }

        sweepImage = sweepRect.GetComponent<Image>();
        if (sweepImage == null)
            sweepImage = sweepRect.gameObject.AddComponent<Image>();
        sweepImage.raycastTarget = false;
    }

    private void PrepareEffectViews()
    {
        if (effectRect == null || effectCanvasGroup == null)
            return;

        effectRect.gameObject.SetActive(true);
        effectRect.SetAsLastSibling();
        effectRect.localScale = Vector3.one;
        effectCanvasGroup.alpha = 0f;
        effectImage.color = glowColor;

        if (sweepRect == null || sweepImage == null)
            return;

        float targetWidth = GetTargetWidth();
        float targetHeight = GetTargetHeight();
        sweepRect.anchorMin = new Vector2(0.5f, 0.5f);
        sweepRect.anchorMax = new Vector2(0.5f, 0.5f);
        sweepRect.pivot = new Vector2(0.5f, 0.5f);
        sweepRect.sizeDelta = new Vector2(sweepWidth, targetHeight + sweepHeightPadding);
        sweepRect.anchoredPosition = new Vector2(-targetWidth * 0.5f - sweepWidth, 0f);
        sweepRect.localRotation = Quaternion.Euler(0f, 0f, sweepRotation);
        sweepRect.localScale = Vector3.one;
        sweepImage.color = sweepColor;
    }

    private void HideEffectImmediate()
    {
        if (effectCanvasGroup != null)
            effectCanvasGroup.alpha = 0f;
        if (effectRect != null)
            effectRect.gameObject.SetActive(false);
    }

    private void StoreCanvasGroupState()
    {
        if (targetCanvasGroup == null || storedCanvasGroupState)
            return;

        storedInteractable = targetCanvasGroup.interactable;
        storedBlocksRaycasts = targetCanvasGroup.blocksRaycasts;
        storedCanvasGroupState = true;
    }

    private void RestoreCanvasGroupState()
    {
        if (targetCanvasGroup == null || !storedCanvasGroupState)
            return;

        targetCanvasGroup.interactable = storedInteractable;
        targetCanvasGroup.blocksRaycasts = storedBlocksRaycasts;
        storedCanvasGroupState = false;
    }

    private float GetTargetWidth()
    {
        return targetRect != null ? Mathf.Max(1f, targetRect.rect.width) : 1f;
    }

    private float GetTargetHeight()
    {
        return targetRect != null ? Mathf.Max(1f, targetRect.rect.height) : 1f;
    }
}
