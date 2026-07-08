using DG.Tweening;
using TMPro;
using UnityEngine;

public class UnlockPopupItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private UnlockPopupVisualConfig visualConfig;
    [SerializeField] private float enterDuration = 0.28f;
    [SerializeField] private float exitDuration = 0.24f;
    [SerializeField] private float displaySeconds = 2.4f;
    [SerializeField] private float hiddenXOffset = -360f;
    [SerializeField] private Ease enterEase = Ease.OutCubic;
    [SerializeField] private Ease exitEase = Ease.InCubic;

    private RectTransform rectTransform;
    private Vector2 shownPosition;
    private Tween tween;

    public float DisplaySeconds => displaySeconds;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDestroy()
    {
        tween?.Kill(false);
    }

    public void Bind(UnlockPendingMessageData message)
    {
        ResolveReferences();
        if (visualConfig == null)
            visualConfig = Resources.Load<UnlockPopupVisualConfig>("Config/UnlockPopupVisualConfig");

        if (titleText != null)
        {
            titleText.text = LocalizationSystem.GetText("ui.unlock_popup.title", "新内容解锁！");
            if (visualConfig != null)
                titleText.color = visualConfig.TitleColor;
        }

        if (contentText != null)
        {
            string targetType = message != null ? message.targetType : string.Empty;
            string targetId = message != null ? message.targetId : string.Empty;
            string typeName = EscapeRichText(UnlockSystem.GetTargetTypeName(targetType));
            string targetName = EscapeRichText(UnlockSystem.GetTargetName(targetType, targetId));
            string typeColor = visualConfig != null ? ColorUtility.ToHtmlStringRGB(visualConfig.GetTypeColor(targetType)) : "FFFFFF";
            string nameColor = visualConfig != null ? ColorUtility.ToHtmlStringRGB(visualConfig.NameColor) : "FFFFFF";
            contentText.richText = true;
            contentText.text = $"【<color=#{typeColor}>{typeName}</color>】<color=#{nameColor}>{targetName}</color>";
        }
    }

    public void PlayEnter()
    {
        ResolveReferences();
        if (rectTransform == null)
            return;

        tween?.Kill(false);
        shownPosition = rectTransform.anchoredPosition;
        rectTransform.anchoredPosition = shownPosition + new Vector2(hiddenXOffset, 0f);
        rectTransform.localScale = Vector3.one;
        tween = rectTransform.DOAnchorPos(shownPosition, enterDuration).SetEase(enterEase).SetUpdate(true).SetTarget(this);
    }

    public void PlayExit(System.Action completed)
    {
        ResolveReferences();
        if (rectTransform == null)
        {
            completed?.Invoke();
            return;
        }

        tween?.Kill(false);
        tween = rectTransform.DOAnchorPos(shownPosition + new Vector2(hiddenXOffset, 0f), exitDuration)
            .SetEase(exitEase)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(() => completed?.Invoke());
    }

    private void ResolveReferences()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;
        if (titleText == null)
            titleText = transform.Find("Title")?.GetComponent<TMP_Text>();
        if (contentText == null)
            contentText = transform.Find("Content")?.GetComponent<TMP_Text>();
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("<", "<\u200B").Replace(">", "\u200B>");
    }
}
