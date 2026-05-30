using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class EventPanelUI : MonoBehaviour
{
    private readonly List<RectTransform> optionRects = new List<RectTransform>();
    [Header("预制体")]
    [SerializeField] private EventOptionView optionPrefab;
    [Header("动画参数")]
    [SerializeField] private float textCharactersPerSecond = 34f;
    [SerializeField] private float optionShowDuration = 0.24f;
    [SerializeField] private float optionShowDelayStep = 0.06f;
    [SerializeField] private Ease optionShowEase = Ease.OutBack;
    [SerializeField] private float optionHideDuration = 0.18f;
    [SerializeField] private Ease optionHideEase = Ease.InBack;
    [SerializeField] private float matchedOptionMoveDuration = 0.26f;
    [SerializeField] private Ease matchedOptionMoveEase = Ease.OutCubic;
    [SerializeField] private float optionChosenWaitDuration = 0.3f;
    [SerializeField] private float tooltipFadeDuration = 0.12f;
    [SerializeField] private float tooltipScaleDuration = 0.18f;
    [SerializeField] private Ease tooltipShowEase = Ease.OutBack;
    [SerializeField] private Ease tooltipHideEase = Ease.InBack;
    [SerializeField] private Vector3 tooltipHiddenScale = new Vector3(0.82f, 0.82f, 1f);
    [SerializeField] private float tooltipYOffset = 58f;
    [SerializeField] private float tagTooltipXOffset = 12f;
    [SerializeField] private float tagTooltipSlideDistance = 24f;
    [SerializeField] private Vector2 tagTooltipSize = new Vector2(250f, 120f);
    [SerializeField] private float tagTooltipVerticalPadding = 20f;

    private RectTransform panel;
    private RectTransform optionArea;
    private Text titleText;
    private Text bodyText;
    private Text hintText;
    private RectTransform optionTooltip;
    private RectTransform optionTagTooltip;
    private CanvasGroup optionTooltipCanvasGroup;
    private CanvasGroup optionTagTooltipCanvasGroup;
    private Text optionTooltipTitle;
    private Text optionTooltipDescription;
    private Text optionTagTooltipText;
    private Tween optionTooltipTween;
    private Tween optionTagTooltipTween;
    private EventModel eventModel;
    private bool typing;
    private bool waitingForClick;
    private bool showingOptions;
    private string fullText;
    private Action optionsShown;

    public bool ShowingOptions => showingOptions;
    public bool WaitingForFinalClick => waitingForClick && !typing && !showingOptions && eventModel != null && eventModel.CurrentOptions.Length == 0;
    public EventOptionData[] CurrentOptions => eventModel != null ? eventModel.CurrentOptions : System.Array.Empty<EventOptionData>();
    public RectTransform MatchedOptionRect { get; private set; }

    public void Initialize(RectTransform parent, Font font, Action optionsShown = null)
    {
        this.optionsShown = optionsShown;
        Transform panelTransform = parent.Find("EventPanel");
        if (panelTransform == null)
        {
            enabled = false;
            return;
        }

        panel = (RectTransform)panelTransform;
        titleText = FindText(panel, "Title");
        bodyText = FindText(panel, "Body");
        hintText = FindText(panel, "Hint");
        EnsureOptionTooltip();
        Transform optionAreaTransform = panel.Find("OptionArea");
        optionArea = optionAreaTransform as RectTransform;
        if (hintText != null)
            hintText.text = "左键：跳过/继续；打出素材并结束回合来选择选项";
        if (optionArea != null)
        {
            if (optionPrefab == null)
                optionPrefab = optionArea.GetComponentInChildren<EventOptionView>(true);

            for (int i = 0; i < optionArea.childCount; i++)
                optionArea.GetChild(i).gameObject.SetActive(false);
        }
        panel.gameObject.SetActive(false);
    }

    public void Bind(EventModel model)
    {
        eventModel = model;
        if (panel != null)
            panel.gameObject.SetActive(true);
        if (titleText != null)
            titleText.text = model.Title;
        StartCoroutine(ShowCurrentNodeRoutine());
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            AdvanceTextInput();
    }

    public void AdvanceTextInput()
    {
        if (showingOptions || eventModel == null)
            return;

        if (typing)
        {
            typing = false;
            if (bodyText != null)
                bodyText.text = fullText;
            return;
        }

        if (waitingForClick)
            waitingForClick = false;
    }

    public IEnumerator ShowCurrentNodeRoutine()
    {
        showingOptions = false;
        ClearOptions();
        string[] texts = eventModel.CurrentTexts;
        if (texts == null || texts.Length == 0)
            texts = new[] { string.Empty };

        for (int i = 0; i < texts.Length; i++)
        {
            yield return TypeText(LocalizationSystem.GetText(texts[i], texts[i]));
            if (i < texts.Length - 1)
            {
                waitingForClick = true;
                while (waitingForClick)
                    yield return null;
            }
        }

        if (eventModel.CurrentOptions.Length > 0)
            ShowOptions();
        else
            waitingForClick = true;
    }

    private IEnumerator TypeText(string text)
    {
        fullText = text;
        if (bodyText != null)
            bodyText.text = string.Empty;
        typing = true;
        float timer = 0f;
        int visible = 0;
        while (typing && visible < fullText.Length)
        {
            timer += Time.deltaTime * textCharactersPerSecond;
            int target = Mathf.Clamp(Mathf.FloorToInt(timer), 0, fullText.Length);
            if (target != visible)
            {
                visible = target;
                if (bodyText != null)
                    bodyText.text = fullText.Substring(0, visible);
            }
            yield return null;
        }

        typing = false;
        if (bodyText != null)
            bodyText.text = fullText;
    }

    private void ShowOptions()
    {
        showingOptions = true;
        ClearOptions();
        if (optionArea == null)
            return;

        EventOptionData[] options = eventModel.CurrentOptions;
        optionsShown?.Invoke();
        for (int i = 0; i < options.Length; i++)
        {
            EventOptionData option = options[i];
            EventOptionView optionView = GetOptionView(i);
            if (optionView == null)
                continue;

            RectTransform rect = (RectTransform)optionView.transform;
            float optionHeight = rect.sizeDelta.y;
            float optionStep = optionHeight + 14f;
            float startY = (options.Length - 1) * optionStep * 0.5f;

            rect.gameObject.SetActive(true);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, startY - optionStep * i);
            rect.localRotation = Quaternion.identity;
            Image background = rect.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
                background.raycastTarget = true;
            }
            optionView.Bind(this, option);
            rect.localScale = Vector3.zero;
            Text recipeText = optionView.RecipeText;
            if (recipeText != null)
                BuildRecipeIcons(recipeText.rectTransform, option);
            Text optionText = optionView.OptionText;
            if (optionText != null)
                optionText.text = LocalizationSystem.GetText(option.titleKey, option.id);
            rect.DOScale(Vector3.one, optionShowDuration).SetDelay(i * optionShowDelayStep).SetEase(optionShowEase).SetTarget(this);
            optionRects.Add(rect);
        }
    }

    private EventOptionView GetOptionView(int index)
    {
        if (index < optionArea.childCount)
        {
            EventOptionView existing = optionArea.GetChild(index).GetComponent<EventOptionView>();
            if (existing != null)
                return existing;
        }

        if (optionPrefab == null)
            return null;

        EventOptionView instance = Instantiate(optionPrefab, optionArea);
        instance.gameObject.name = "Option" + index;
        return instance;
    }

    public IEnumerator PlayOptionChosen(EventOptionData option)
    {
        MatchedOptionRect = null;
        int optionIndex = System.Array.IndexOf(eventModel.CurrentOptions, option);
        if (optionIndex >= 0 && optionIndex < optionRects.Count)
            MatchedOptionRect = optionRects[optionIndex];

        for (int i = 0; i < optionRects.Count; i++)
        {
            if (optionRects[i] == MatchedOptionRect)
                continue;
            optionRects[i].DOScale(Vector3.zero, optionHideDuration).SetEase(optionHideEase).SetTarget(this);
        }

        if (MatchedOptionRect != null)
            MatchedOptionRect.DOAnchorPos(Vector2.zero, matchedOptionMoveDuration).SetEase(matchedOptionMoveEase).SetTarget(this);

        yield return new WaitForSeconds(optionChosenWaitDuration);
        showingOptions = false;
    }

    public void Close()
    {
        DOTween.Kill(this);
        optionTooltipTween?.Kill(false);
        optionTagTooltipTween?.Kill(false);
        ClearOptions();
        if (panel != null)
            panel.gameObject.SetActive(false);
        Destroy(this);
    }

    private void ClearOptions()
    {
        HideOptionTooltip();
        for (int i = 0; i < optionRects.Count; i++)
        {
            if (optionRects[i] != null)
                optionRects[i].gameObject.SetActive(false);
        }
        optionRects.Clear();
        MatchedOptionRect = null;
    }

    public void ShowOptionTooltip(RectTransform anchor, EventOptionData option)
    {
        if (anchor == null || option == null)
            return;

        EnsureOptionTooltip();
        if (optionTooltip == null)
            return;

        if (optionTooltipTitle != null)
            optionTooltipTitle.text = LocalizationSystem.GetText(option.titleKey, option.id);
        if (optionTooltipDescription != null)
            optionTooltipDescription.text = GetOptionEffectText(option);
        if (optionTagTooltipText != null)
            optionTagTooltipText.text = BuildOptionTagTooltipText(option);

        optionTooltip.gameObject.SetActive(true);
        PopupLayerUtility.ApplyTo(optionTooltip);
        optionTooltip.SetAsLastSibling();
        optionTooltip.position = anchor.position + new Vector3(0f, tooltipYOffset, 0f);
        optionTooltip.localScale = tooltipHiddenScale;
        optionTooltipCanvasGroup.alpha = 0f;
        optionTooltipTween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(optionTooltipCanvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(optionTooltip.DOScale(Vector3.one, tooltipScaleDuration).SetEase(tooltipShowEase));
        optionTooltipTween = sequence;
        ShowOptionTagTooltip();
    }

    public void HideOptionTooltip()
    {
        if (optionTooltip == null || !optionTooltip.gameObject.activeSelf)
            return;

        optionTooltipTween?.Kill(false);
        optionTagTooltipTween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(optionTooltipCanvasGroup.DOFade(0f, tooltipFadeDuration));
        sequence.Join(optionTooltip.DOScale(tooltipHiddenScale, tooltipScaleDuration).SetEase(tooltipHideEase));
        sequence.OnComplete(() => optionTooltip.gameObject.SetActive(false));
        optionTooltipTween = sequence;
        HideOptionTagTooltip();
    }

    private void ShowOptionTagTooltip()
    {
        if (optionTagTooltip == null || optionTagTooltipCanvasGroup == null || optionTagTooltipText == null || string.IsNullOrEmpty(optionTagTooltipText.text))
            return;

        optionTagTooltipText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, tagTooltipSize.x - 24f);
        Canvas.ForceUpdateCanvases();
        optionTagTooltip.sizeDelta = new Vector2(tagTooltipSize.x, optionTagTooltipText.preferredHeight + tagTooltipVerticalPadding);
        Vector2 shownPosition = GetOptionTagTooltipShownPosition();
        optionTagTooltip.gameObject.SetActive(true);
        PopupLayerUtility.ApplyTo(optionTagTooltip);
        optionTagTooltip.SetAsLastSibling();
        optionTagTooltipCanvasGroup.alpha = 0f;
        optionTagTooltip.localScale = Vector3.one;
        optionTagTooltip.anchoredPosition = shownPosition - new Vector2(tagTooltipSlideDistance, 0f);

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(optionTagTooltipCanvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(optionTagTooltip.DOAnchorPos(shownPosition, tooltipScaleDuration).SetEase(tooltipShowEase));
        optionTagTooltipTween = sequence;
    }

    private void HideOptionTagTooltip()
    {
        if (optionTagTooltip == null || optionTagTooltipCanvasGroup == null || !optionTagTooltip.gameObject.activeSelf)
            return;

        Vector2 hiddenPosition = GetOptionTagTooltipShownPosition() - new Vector2(tagTooltipSlideDistance, 0f);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(optionTagTooltipCanvasGroup.DOFade(0f, tooltipFadeDuration));
        sequence.Join(optionTagTooltip.DOAnchorPos(hiddenPosition, tooltipScaleDuration).SetEase(tooltipHideEase));
        optionTagTooltipTween = sequence.OnComplete(() => optionTagTooltip.gameObject.SetActive(false));
    }

    private Vector2 GetOptionTagTooltipShownPosition()
    {
        if (optionTooltip == null)
            return Vector2.zero;

        return optionTooltip.anchoredPosition + new Vector2(optionTooltip.sizeDelta.x * (1f - optionTooltip.pivot.x) + tagTooltipXOffset, optionTooltip.sizeDelta.y * (1f - optionTooltip.pivot.y));
    }

    private void EnsureOptionTooltip()
    {
        if (panel == null || optionTooltip != null)
            return;

        Image image = new GameObject("OptionTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)).GetComponent<Image>();
        image.transform.SetParent(panel, false);
        image.color = new Color(0.035f, 0.035f, 0.055f, 0.96f);
        image.raycastTarget = false;
        optionTooltip = image.rectTransform;
        optionTooltip.sizeDelta = new Vector2(320f, 92f);
        optionTooltip.anchorMin = new Vector2(0.5f, 0.5f);
        optionTooltip.anchorMax = new Vector2(0.5f, 0.5f);
        optionTooltip.pivot = new Vector2(0.5f, 0f);
        optionTooltipCanvasGroup = optionTooltip.GetComponent<CanvasGroup>();
        PopupLayerUtility.ApplyTo(optionTooltip);
        optionTooltipCanvasGroup.alpha = 0f;

        optionTooltipTitle = CreateTooltipText(optionTooltip, "Title", 18, FontStyle.Bold, new Vector2(0f, 24f), new Vector2(280f, 28f));
        optionTooltipDescription = CreateTooltipText(optionTooltip, "Description", 15, FontStyle.Normal, new Vector2(0f, -12f), new Vector2(280f, 44f));
        EnsureOptionTagTooltip();
        optionTooltip.gameObject.SetActive(false);
    }

    private void EnsureOptionTagTooltip()
    {
        if (panel == null || optionTooltip == null || optionTagTooltip != null)
            return;

        optionTagTooltip = new GameObject("OptionTagTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
        optionTagTooltip.SetParent(panel, false);
        optionTagTooltip.anchorMin = optionTooltip.anchorMin;
        optionTagTooltip.anchorMax = optionTooltip.anchorMax;
        optionTagTooltip.pivot = new Vector2(0f, 1f);
        optionTagTooltip.sizeDelta = tagTooltipSize;
        Image image = optionTagTooltip.GetComponent<Image>();
        image.color = new Color(0.03f, 0.03f, 0.04f, 0.96f);
        image.raycastTarget = false;
        optionTagTooltipCanvasGroup = optionTagTooltip.GetComponent<CanvasGroup>();
        optionTagTooltipCanvasGroup.alpha = 0f;
        optionTagTooltipCanvasGroup.blocksRaycasts = false;
        PopupLayerUtility.ApplyTo(optionTagTooltip);

        optionTagTooltipText = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        optionTagTooltipText.transform.SetParent(optionTagTooltip, false);
        optionTagTooltipText.font = optionTooltipDescription != null && optionTooltipDescription.font != null ? optionTooltipDescription.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        optionTagTooltipText.fontSize = 16;
        optionTagTooltipText.alignment = TextAnchor.UpperLeft;
        optionTagTooltipText.color = new Color(1f, 0.88f, 0.58f, 1f);
        optionTagTooltipText.raycastTarget = false;
        optionTagTooltipText.supportRichText = true;
        optionTagTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        optionTagTooltipText.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform textRect = optionTagTooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 10f);
        textRect.offsetMax = new Vector2(-12f, -10f);
        optionTagTooltip.gameObject.SetActive(false);
    }

    private Text CreateTooltipText(RectTransform parent, string name, int fontSize, FontStyle fontStyle, Vector2 position, Vector2 size)
    {
        Text text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(parent, false);
        text.font = bodyText != null ? bodyText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }

    private static string GetOptionEffectText(EventOptionData option)
    {
        string effect = "无直接效果";
        if (option.resultId == 1)
            effect = "恢复10点生命";
        else if (option.resultId == 2)
            effect = "之后每回合抽牌数+1";
        else if (option.resultId == 100)
            effect = "选择并删除" + GetChoiceCountText(option) + "张手牌素材";
        else if (option.resultId >= 101 && option.resultId <= 104)
            effect = "获得1张素材牌";
        else if (option.resultId == 201)
            effect = "选择" + GetChoiceCountText(option) + "张手牌素材，添加助燃";
        else if (option.resultId == 202)
            effect = "选择" + GetChoiceCountText(option) + "张手牌素材，添加流转";
        else if (option.resultId == 203)
            effect = "选择" + GetChoiceCountText(option) + "张手牌素材，添加液化";
        else if (option.resultId == 301)
            effect = LocalizationSystem.GetText("rest.option.study.effect", "从2个强化中选择1个，附魔到一个法术上");
        else if (option.resultId == 302)
            effect = LocalizationSystem.GetText("rest.option.deep_study.effect", "从3个强化中选择1个，附魔到一个法术上");

        return "效果：" + effect;
    }

    private static string GetChoiceCountText(EventOptionData option)
    {
        return option != null && option.choiceCount > 0 ? option.choiceCount.ToString() : "1";
    }

    private static string BuildOptionTagTooltipText(EventOptionData option)
    {
        if (option == null || option.tagIds == null || option.tagIds.Length == 0)
            return string.Empty;

        StringBuilder builder = null;
        for (int i = 0; i < option.tagIds.Length; i++)
        {
            string tagId = option.tagIds[i];
            if (string.IsNullOrEmpty(tagId))
                continue;

            string name = LocalizationSystem.GetText("modifier." + tagId + ".name", string.Empty);
            string description = LocalizationSystem.GetText("modifier." + tagId + ".desc", string.Empty);
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description))
                continue;

            if (builder == null)
                builder = new StringBuilder();
            else
                builder.Append("\n\n");

            builder.Append("<color=#FFE99E>");
            builder.Append(name);
            builder.Append("：</color>\n");
            builder.Append(description);
        }

        return builder != null ? builder.ToString() : string.Empty;
    }

    private static void BuildRecipeIcons(RectTransform recipeRoot, EventOptionData option)
    {
        Text recipeText = recipeRoot.GetComponent<Text>();
        if (recipeText != null)
            recipeText.text = string.Empty;

        for (int i = recipeRoot.childCount - 1; i >= 0; i--)
            Destroy(recipeRoot.GetChild(i).gameObject);

        MaterialEnum[] materials = EventModel.ParseRecipe(option != null ? option.recipe : null);
        if (option != null && option.isExitOption)
            materials = System.Array.Empty<MaterialEnum>();
        float spacing = 30f;
        float startX = materials.Length > 1 ? -spacing * (materials.Length - 1) * 0.5f : 0f;
        for (int i = 0; i < materials.Length; i++)
        {
            Image icon = new GameObject("MaterialIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(recipeRoot, false);
            icon.color = MaterialCardView.GetMaterialColor(materials[i]);
            icon.raycastTarget = false;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(startX + spacing * i, 0f);
            iconRect.sizeDelta = new Vector2(24f, 24f);
        }
    }

    private static Text FindText(RectTransform root, string name)
    {
        Transform child = root.Find(name);
        return child != null ? child.GetComponent<Text>() : null;
    }
}
