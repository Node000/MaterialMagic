using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Text healthText;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image healthBufferFill;
    [SerializeField] private Image shieldFill;
    [SerializeField] private float textWidth = 72f;
    [Header("动画参数")]
    [SerializeField] private float healthFillDuration = 0.35f;
    [SerializeField] private float healthBufferDecreaseDuration = 0.55f;
    [SerializeField] private float healthBufferIncreaseDuration = 0.35f;
    [SerializeField] private float healthTextDuration = 0.35f;
    [SerializeField] private Ease healthEase = Ease.OutQuad;

    private int displayedHealth;
    private Tween healthNumberTween;

    public Text HealthText => healthText;
    public Image HealthFill => healthFill;
    public Image HealthBufferFill => healthBufferFill;
    public Image ShieldFill => shieldFill;
    public int DisplayedHealth => displayedHealth;
    public Tween HealthNumberTween => healthNumberTween;

    public void Initialize(Text text, Image fill, float labelWidth, int currentHealth)
    {
        healthText = text;
        healthFill = fill;
        textWidth = labelWidth;
        displayedHealth = currentHealth;
        CacheLayers();
        Setup(currentHealth, currentHealth, 0, true);
    }

    public void Setup(int currentHealth, int maxHealth, int shield, bool instant)
    {
        SetupHealthText(healthText);
        if (healthFill == null)
            return;

        RectTransform barBack = healthFill.transform.parent as RectTransform;
        if (barBack == null)
            return;

        if (barBack.sizeDelta.x < 0f)
        {
            barBack.anchorMin = new Vector2(0f, 0.5f);
            barBack.anchorMax = new Vector2(0f, 0.5f);
            barBack.pivot = new Vector2(0f, 0.5f);
            barBack.sizeDelta = new Vector2(220f, barBack.sizeDelta.y);
            barBack.anchoredPosition = new Vector2(86f, barBack.anchoredPosition.y);
        }

        SetupFillImage(healthFill, new Color(0.82f, 0.05f, 0.04f, 1f), 1);
        CacheLayers();
        SetHealthLayerOrder(healthBufferFill, healthFill, shieldFill);
        PositionHealthTextLeftOfBar(healthText, barBack, textWidth);
        UpdateValue(currentHealth, maxHealth, shield, instant);
    }

    public void UpdateValue(int currentHealth, int maxHealth, int shield, bool instant)
    {
        UpdateHealthBar(healthFill, healthBufferFill, shieldFill, currentHealth, maxHealth, shield, instant, healthFillDuration, healthBufferDecreaseDuration, healthBufferIncreaseDuration, healthEase);
        healthNumberTween?.Kill(false);
        healthNumberTween = UpdateHealthText(healthText, displayedHealth, currentHealth, instant, value => displayedHealth = value, this, healthTextDuration, healthEase);
    }

    private void CacheLayers()
    {
        if (healthFill == null)
            return;

        RectTransform barBack = healthFill.transform.parent as RectTransform;
        if (barBack == null)
            return;

        if (healthBufferFill == null)
            healthBufferFill = CreateHealthFillLayer(barBack, "HealthBufferFill", Color.white, 0);
        if (shieldFill == null)
            shieldFill = CreateHealthFillLayer(barBack, "ShieldFill", new Color(0.2f, 0.55f, 1f, 1f), 2);
        if (shieldFill != null)
            shieldFill.raycastTarget = false;
    }

    public static Image CreateHealthFillLayer(RectTransform parent, string name, Color color, int siblingIndex)
    {
        Transform child = parent.Find(name);
        Image image = child != null ? child.GetComponent<Image>() : null;
        if (image == null)
            return null;

        image.transform.SetParent(parent, false);
        SetupFillImage(image, color, siblingIndex);
        return image;
    }

    public static void SetupFillImage(Image image, Color color, int siblingIndex)
    {
        if (image == null)
            return;

        image.color = color;
        image.raycastTarget = false;
        image.fillAmount = 1f;
        image.transform.SetSiblingIndex(siblingIndex);
        SetRectHorizontalRange(image.GetComponent<RectTransform>(), 0f, 1f);
    }

    public static void SetHealthLayerOrder(Image bufferFill, Image healthFillImage, Image shieldFill)
    {
        if (bufferFill != null)
            bufferFill.transform.SetSiblingIndex(0);
        if (healthFillImage != null)
            healthFillImage.transform.SetSiblingIndex(1);
        if (shieldFill != null)
            shieldFill.transform.SetSiblingIndex(2);
    }

    public static void SetupHealthText(Text text)
    {
        if (text == null)
            return;

        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    public static void PositionHealthTextLeftOfBar(Text text, RectTransform barBack, float width)
    {
        if (text == null || barBack == null)
            return;

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = barBack.anchorMin;
        rect.anchorMax = barBack.anchorMin;
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(width, barBack.rect.height + 8f);
        rect.anchoredPosition = barBack.anchoredPosition + new Vector2(-8f, 0f);
    }

    public static void UpdateHealthBar(Image healthFillImage, Image bufferFillImage, Image shieldFillImage, int currentHealth, int maxHealth, int shield, bool instant)
    {
        UpdateHealthBar(healthFillImage, bufferFillImage, shieldFillImage, currentHealth, maxHealth, shield, instant, 0.35f, 0.55f, 0.35f, Ease.OutQuad);
    }

    public static void UpdateHealthBar(Image healthFillImage, Image bufferFillImage, Image shieldFillImage, int currentHealth, int maxHealth, int shield, bool instant, float fillDuration, float bufferDecreaseDuration, float bufferIncreaseDuration, Ease ease)
    {
        if (healthFillImage == null || maxHealth <= 0)
            return;

        float totalMax = maxHealth + Mathf.Max(0, shield);
        float healthEnd = Mathf.Clamp01((currentHealth + Mathf.Max(0, shield)) / totalMax);
        float shieldWidth = Mathf.Clamp01(Mathf.Max(0, shield) / totalMax);
        float shieldStart = shield > 0 ? Mathf.Max(0f, healthEnd - shieldWidth) : healthEnd;
        float shieldEnd = healthEnd;
        AnimateHorizontalRange(healthFillImage.GetComponent<RectTransform>(), 0f, healthEnd, fillDuration, instant, ease);
        if (shieldFillImage != null)
        {
            SetRectHorizontalRange(shieldFillImage.GetComponent<RectTransform>(), shieldStart, shieldEnd);
            SetImageAlpha(shieldFillImage, shield > 0 && shieldEnd > shieldStart ? 1f : 0f);
        }

        if (bufferFillImage != null)
        {
            float bufferEnd = GetRectHorizontalEnd(bufferFillImage.GetComponent<RectTransform>());
            float end = healthEnd < bufferEnd ? healthEnd : shieldEnd;
            float duration = healthEnd < bufferEnd ? bufferDecreaseDuration : bufferIncreaseDuration;
            AnimateHorizontalRange(bufferFillImage.GetComponent<RectTransform>(), 0f, end, duration, instant, ease);
        }
    }

    public static float GetRectHorizontalEnd(RectTransform rect)
    {
        return rect != null ? rect.anchorMax.x : 0f;
    }

    public static void SetRectHorizontalRange(RectTransform rect, float start, float end)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(Mathf.Clamp01(start), 0f);
        rect.anchorMax = new Vector2(Mathf.Clamp01(end), 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static void AnimateHorizontalRange(RectTransform rect, float start, float end, float duration, bool instant)
    {
        AnimateHorizontalRange(rect, start, end, duration, instant, Ease.OutQuad);
    }

    public static void AnimateHorizontalRange(RectTransform rect, float start, float end, float duration, bool instant, Ease ease)
    {
        if (rect == null)
            return;

        rect.DOKill(false);
        start = Mathf.Clamp01(start);
        end = Mathf.Clamp01(end);
        if (instant)
        {
            SetRectHorizontalRange(rect, start, end);
            return;
        }

        rect.DOAnchorMin(new Vector2(start, 0f), duration).SetEase(ease);
        rect.DOAnchorMax(new Vector2(end, 1f), duration).SetEase(ease);
    }

    public static Tween UpdateHealthText(Text text, int displayedHealth, int targetHealth, bool instant, Action<int> setDisplayedHealth, object tweenTarget)
    {
        return UpdateHealthText(text, displayedHealth, targetHealth, instant, setDisplayedHealth, tweenTarget, 0.35f, Ease.OutQuad);
    }

    public static Tween UpdateHealthText(Text text, int displayedHealth, int targetHealth, bool instant, Action<int> setDisplayedHealth, object tweenTarget, float duration, Ease ease)
    {
        if (text == null)
            return null;

        if (instant)
        {
            setDisplayedHealth(targetHealth);
            text.text = targetHealth.ToString();
            return null;
        }

        return DOVirtual.Int(displayedHealth, targetHealth, duration, value =>
        {
            setDisplayedHealth(value);
            text.text = value.ToString();
        }).SetEase(ease).SetTarget(tweenTarget);
    }

    public static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void OnDestroy()
    {
        healthNumberTween?.Kill(false);
    }
}
