using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EventPanelUI : MonoBehaviour
{
    private static readonly Vector2 PanelOpenPivot = new Vector2(0f, 1f);
    private static readonly Vector2 PanelClosePivot = new Vector2(1f, 0f);

    private readonly List<RectTransform> optionRects = new List<RectTransform>();
    [Header("预制体")]
    [SerializeField] private EventOptionView optionPrefab;
    [Header("面板动画参数")]
    [SerializeField] private float panelOpenDuration = 0.28f;
    [SerializeField] private Ease panelOpenEase = Ease.OutCubic;
    [SerializeField] private float panelCloseDuration = 0.2f;
    [SerializeField] private Ease panelCloseEase = Ease.InCubic;
    [SerializeField] private Vector2 panelDiagonalMoveOffset = new Vector2(72f, -72f);
    [SerializeField] private RectTransform revealMask;
    [SerializeField] private RectTransform contentRoot;
    [Header("文本和选项动画参数")]
    [SerializeField] private float textCharactersPerSecond = 34f;
    [SerializeField] private float optionShowDuration = 0.24f;
    [SerializeField] private float optionShowDelayStep = 0.06f;
    [SerializeField] private float optionSpacing = 14f;
    [SerializeField] private float optionAreaHeight = 300f;
    [SerializeField] private float minOptionHeight = 46f;
    [SerializeField] private float optionHeightShrinkPerExtraOption = 12f;
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
    [SerializeField] private float tooltipYOffset = 28f;
    [SerializeField] private float tagTooltipXOffset = 12f;
    [SerializeField] private float tagTooltipSlideDistance = 24f;
    [SerializeField] private Vector2 tagTooltipSize = new Vector2(250f, 120f);
    [SerializeField] private float tagTooltipVerticalPadding = 20f;

    private RectTransform panel;
    private RectTransform optionArea;
    private Vector2 panelDefaultAnchoredPosition;
    private Vector2 fullPanelSize;
    private bool hasPanelDefaultTransform;
    private Tween panelTween;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text hintText;
    private RectTransform optionTooltip;
    private RectTransform optionTagTooltip;
    private CanvasGroup optionTooltipCanvasGroup;
    private CanvasGroup optionTagTooltipCanvasGroup;
    private TMP_Text optionTooltipTitle;
    private TMP_Text optionTooltipDescription;
    private TMP_Text optionTagTooltipText;
    private Tween optionTooltipTween;
    private Tween optionTagTooltipTween;
    private EventModel eventModel;
    private bool typing;
    private bool waitingForClick;
    private bool waitingForFinalNodeClick;
    private bool showingOptions;
    private string fullText;
    private Action optionsShown;
    private readonly Vector3[] tooltipAnchorCorners = new Vector3[4];

    public bool ShowingOptions => showingOptions;
    public bool WaitingForFinalClick => waitingForFinalNodeClick && waitingForClick && !typing && !showingOptions && eventModel != null && eventModel.CurrentOptions.Length == 0;
    public EventOptionData[] CurrentOptions => eventModel != null ? eventModel.CurrentOptions : System.Array.Empty<EventOptionData>();
    public RectTransform MatchedOptionRect { get; private set; }

    public void Initialize(RectTransform parent, TMP_FontAsset font, Action optionsShown = null)
    {
        this.optionsShown = optionsShown;
        Transform panelTransform = parent.Find("EventPanel");
        if (panelTransform == null)
        {
            enabled = false;
            return;
        }

        panel = (RectTransform)panelTransform;
        CachePanelTransform();
        DOTween.Kill(this);
        panelTween?.Kill(false);
        ResetPanelTransform();
        HideOptionTooltipImmediate();
        titleText = FindText(GetPanelContentRoot(), "Title");
        bodyText = FindText(GetPanelContentRoot(), "Body");
        hintText = FindText(GetPanelContentRoot(), "Hint");
        Transform optionAreaTransform = GetPanelContentRoot().Find("OptionArea");
        optionArea = optionAreaTransform as RectTransform;
        if (hintText != null)
            hintText.text = LocalizationSystem.GetText("ui.event_panel.hint", "左键：跳过/继续；打出素材并结束回合来选择选项");
        if (optionArea != null)
        {
            if (optionPrefab == null)
                optionPrefab = optionArea.GetComponentInChildren<EventOptionView>(true);

            for (int i = 0; i < optionArea.childCount; i++)
                optionArea.GetChild(i).gameObject.SetActive(false);
        }
        SetPanelActive(false);
    }

    public void Bind(EventModel model)
    {
        eventModel = model;
        typing = false;
        waitingForClick = false;
        waitingForFinalNodeClick = false;
        showingOptions = false;
        StopAllCoroutines();
        SetPanelActive(true);
        if (titleText != null)
            titleText.text = model.Title;
        StartCoroutine(BindRoutine());
    }

    private IEnumerator BindRoutine()
    {
        yield return PlayPanelOpenRoutine();
        yield return ShowCurrentNodeRoutine();
    }

    private IEnumerator PlayPanelOpenRoutine()
    {
        if (panel == null)
            yield break;

        panelTween?.Kill(false);
        ResetPanelTransform();
        ConfigureRevealLayout(true);
        SetRevealProgress(0f);
        panel.anchoredPosition = panelDefaultAnchoredPosition - panelDiagonalMoveOffset;
        if (panelOpenDuration <= 0f)
        {
            ResetPanelTransform();
            yield break;
        }

        bool complete = false;
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(DOVirtual.Float(0f, 1f, panelOpenDuration, SetRevealProgress).SetEase(panelOpenEase));
        sequence.Join(panel.DOAnchorPos(panelDefaultAnchoredPosition, panelOpenDuration).SetEase(panelOpenEase));
        panelTween = sequence;
        panelTween.OnComplete(() => complete = true);
        while (!complete && panelTween != null && panelTween.IsActive())
            yield return null;

        panelTween = null;
        ResetPanelTransform();
    }

    private void CachePanelTransform()
    {
        if (panel == null)
            return;

        if (!hasPanelDefaultTransform)
        {
            panelDefaultAnchoredPosition = panel.anchoredPosition;
            hasPanelDefaultTransform = true;
        }

        if (revealMask == null)
        {
            Transform maskTransform = panel.Find("RevealMask");
            revealMask = maskTransform as RectTransform;
        }

        if (contentRoot == null)
        {
            Transform contentTransform = revealMask != null ? revealMask.Find("PanelContent") : panel.Find("PanelContent");
            contentRoot = contentTransform as RectTransform;
        }

        Vector2 rectSize = panel.rect.size;
        fullPanelSize = rectSize.x > 0f && rectSize.y > 0f ? rectSize : panel.sizeDelta;
        ConfigureRevealLayout(true);
        SetRevealProgress(1f);
    }

    private void ResetPanelTransform()
    {
        if (panel == null || !hasPanelDefaultTransform)
            return;

        panel.anchoredPosition = panelDefaultAnchoredPosition;
        panel.localScale = Vector3.one;
        ConfigureRevealLayout(true);
        SetRevealProgress(1f);
    }

    private RectTransform GetPanelContentRoot()
    {
        return contentRoot != null ? contentRoot : panel;
    }

    private void SetPanelActive(bool active)
    {
        if (panel != null)
            panel.gameObject.SetActive(active);
    }

    private void ConfigureRevealLayout(bool fromTopLeft)
    {
        if (revealMask == null)
            return;

        revealMask.anchorMin = new Vector2(0.5f, 0.5f);
        revealMask.anchorMax = new Vector2(0.5f, 0.5f);
        revealMask.pivot = fromTopLeft ? PanelOpenPivot : PanelClosePivot;
        revealMask.anchoredPosition = fromTopLeft
            ? new Vector2(fullPanelSize.x * -0.5f, fullPanelSize.y * 0.5f)
            : new Vector2(fullPanelSize.x * 0.5f, fullPanelSize.y * -0.5f);
        revealMask.localScale = Vector3.one;

        if (contentRoot == null)
            return;

        contentRoot.anchorMin = fromTopLeft ? PanelOpenPivot : PanelClosePivot;
        contentRoot.anchorMax = contentRoot.anchorMin;
        contentRoot.pivot = new Vector2(0.5f, 0.5f);
        contentRoot.anchoredPosition = fromTopLeft
            ? new Vector2(fullPanelSize.x * 0.5f, fullPanelSize.y * -0.5f)
            : new Vector2(fullPanelSize.x * -0.5f, fullPanelSize.y * 0.5f);
        contentRoot.sizeDelta = fullPanelSize;
        contentRoot.localScale = Vector3.one;
    }

    private void SetRevealProgress(float progress)
    {
        if (revealMask == null)
            return;

        float clampedProgress = Mathf.Clamp01(progress);
        revealMask.sizeDelta = new Vector2(fullPanelSize.x * clampedProgress, fullPanelSize.y * clampedProgress);
    }

    private void HideOptionTooltipImmediate()
    {
        optionTooltipTween?.Kill(false);
        optionTagTooltipTween?.Kill(false);
        optionTooltipTween = null;
        optionTagTooltipTween = null;
        if (optionTooltipCanvasGroup != null)
            optionTooltipCanvasGroup.alpha = 0f;
        if (optionTooltip != null)
        {
            optionTooltip.localScale = tooltipHiddenScale;
            optionTooltip.gameObject.SetActive(false);
        }
        if (optionTagTooltipCanvasGroup != null)
            optionTagTooltipCanvasGroup.alpha = 0f;
        if (optionTagTooltip != null)
            optionTagTooltip.gameObject.SetActive(false);
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
        {
            if (waitingForFinalNodeClick)
                return;

            waitingForClick = false;
        }
    }

    public IEnumerator ShowCurrentNodeRoutine()
    {
        if (eventModel == null)
            yield break;

        showingOptions = false;
        waitingForFinalNodeClick = false;
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
                waitingForFinalNodeClick = false;
                while (waitingForClick)
                    yield return null;
            }
        }

        if (eventModel.CurrentOptions.Length > 0)
            ShowOptions();
        else
        {
            waitingForFinalNodeClick = true;
            waitingForClick = true;
        }
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
        float optionHeight = GetOptionHeight(options.Length);
        float optionStep = optionHeight + optionSpacing;
        float startY = (options.Length - 1) * optionStep * 0.5f;
        for (int i = 0; i < options.Length; i++)
        {
            EventOptionData option = options[i];
            EventOptionView optionView = GetOptionView(i);
            if (optionView == null)
                continue;

            RectTransform rect = (RectTransform)optionView.transform;

            rect.gameObject.SetActive(true);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, startY - optionStep * i);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, optionHeight);
            rect.localRotation = Quaternion.identity;
            Image background = rect.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.08f, 0.08f, 0.12f, 1f);
                background.raycastTarget = true;
            }
            optionView.Bind(this, option);
            rect.localScale = Vector3.zero;
            TMP_Text recipeText = optionView.RecipeText;
            if (recipeText != null)
                BuildRecipeIcons(recipeText.rectTransform, option);
            TMP_Text optionText = optionView.OptionText;
            if (optionText != null)
                optionText.text = LocalizationSystem.GetText(option.titleKey, option.id);
            rect.DOScale(Vector3.one, optionShowDuration).SetDelay(i * optionShowDelayStep).SetEase(optionShowEase).SetTarget(this);
            optionRects.Add(rect);
        }
    }

    private float GetOptionHeight(int optionCount)
    {
        if (optionPrefab == null)
            return Mathf.Max(minOptionHeight, 64f - Mathf.Max(0, optionCount - 3) * optionHeightShrinkPerExtraOption);

        RectTransform prefabRect = optionPrefab.transform as RectTransform;
        float baseHeight = prefabRect != null ? prefabRect.sizeDelta.y : 64f;
        float shrunkHeight = baseHeight - Mathf.Max(0, optionCount - 3) * optionHeightShrinkPerExtraOption;
        if (optionCount > 1)
        {
            float maxHeightByArea = (optionAreaHeight - optionSpacing * (optionCount - 1)) / optionCount;
            shrunkHeight = Mathf.Min(shrunkHeight, maxHeightByArea);
        }
        return Mathf.Max(minOptionHeight, shrunkHeight);
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
        StopAllCoroutines();
        DOTween.Kill(this);
        panelTween?.Kill(false);
        optionTooltipTween?.Kill(false);
        optionTagTooltipTween?.Kill(false);
        HideOptionTooltipImmediate();
        eventModel = null;
        typing = false;
        waitingForClick = false;
        waitingForFinalNodeClick = false;
        showingOptions = false;
        if (panel == null || !panel.gameObject.activeSelf)
        {
            ClearOptions();
            return;
        }

        ResetPanelTransform();
        ConfigureRevealLayout(false);
        SetRevealProgress(1f);
        Vector2 closeTargetPosition = panelDefaultAnchoredPosition + panelDiagonalMoveOffset;
        if (panelCloseDuration <= 0f)
        {
            CompletePanelClose();
            return;
        }

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(DOVirtual.Float(1f, 0f, panelCloseDuration, SetRevealProgress).SetEase(panelCloseEase));
        sequence.Join(panel.DOAnchorPos(closeTargetPosition, panelCloseDuration).SetEase(panelCloseEase));
        panelTween = sequence;
        panelTween.OnComplete(CompletePanelClose);
    }

    private void CompletePanelClose()
    {
        panelTween = null;
        ClearOptions();
        if (panel == null)
            return;

        SetPanelActive(false);
        ResetPanelTransform();
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

        UIManager uiManager = GetComponentInParent<UIManager>();
        uiManager?.ShowUnifiedDetailPopup(anchor, UnifiedDetailContentBuilder.Build(option));
    }

    public void HideOptionTooltip()
    {
        UIManager uiManager = GetComponentInParent<UIManager>();
        uiManager?.HideUnifiedDetailPopup(null);
    }

    public void PinOptionTooltip(RectTransform anchor, EventOptionData option)
    {
        if (anchor == null || option == null)
            return;

        UIManager uiManager = GetComponentInParent<UIManager>();
        uiManager?.PinUnifiedDetailPopup(anchor, UnifiedDetailContentBuilder.Build(option));
    }

    private Vector2 GetTooltipAnchoredPosition(RectTransform anchor)
    {
        anchor.GetWorldCorners(tooltipAnchorCorners);
        Vector3 topCenter = (tooltipAnchorCorners[1] + tooltipAnchorCorners[2]) * 0.5f;
        Vector3 panelLocalPoint = panel.InverseTransformPoint(topCenter);
        return new Vector2(panelLocalPoint.x, panelLocalPoint.y + tooltipYOffset);
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
        image.color = new Color(0.035f, 0.035f, 0.055f, 1f);
        image.raycastTarget = false;
        optionTooltip = image.rectTransform;
        optionTooltip.sizeDelta = new Vector2(320f, 92f);
        optionTooltip.anchorMin = new Vector2(0.5f, 0.5f);
        optionTooltip.anchorMax = new Vector2(0.5f, 0.5f);
        optionTooltip.pivot = new Vector2(0.5f, 0f);
        optionTooltipCanvasGroup = optionTooltip.GetComponent<CanvasGroup>();
        PopupLayerUtility.ApplyTo(optionTooltip);
        optionTooltipCanvasGroup.alpha = 0f;

        optionTooltipTitle = CreateTooltipText(optionTooltip, "Title", 18, FontStyles.Bold, new Vector2(0f, 24f), new Vector2(280f, 28f));
        optionTooltipDescription = CreateTooltipText(optionTooltip, "Description", 15, FontStyles.Normal, new Vector2(0f, -12f), new Vector2(280f, 44f));
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
        image.color = new Color(0.03f, 0.03f, 0.04f, 1f);
        image.raycastTarget = false;
        optionTagTooltipCanvasGroup = optionTagTooltip.GetComponent<CanvasGroup>();
        optionTagTooltipCanvasGroup.alpha = 0f;
        optionTagTooltipCanvasGroup.blocksRaycasts = false;
        PopupLayerUtility.ApplyTo(optionTagTooltip);

        optionTagTooltipText = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        optionTagTooltipText.transform.SetParent(optionTagTooltip, false);
        optionTagTooltipText.font = optionTooltipDescription != null && optionTooltipDescription.font != null ? optionTooltipDescription.font : UIManager.GetDefaultTMPFont();
        optionTagTooltipText.fontSize = 16;
        optionTagTooltipText.alignment = TextAlignmentOptions.TopLeft;
        optionTagTooltipText.color = new Color(1f, 0.88f, 0.58f, 1f);
        optionTagTooltipText.raycastTarget = false;
        optionTagTooltipText.richText = true;
        optionTagTooltipText.enableWordWrapping = true;
        optionTagTooltipText.overflowMode = TextOverflowModes.Overflow;
        RectTransform textRect = optionTagTooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 10f);
        textRect.offsetMax = new Vector2(-12f, -10f);
        optionTagTooltip.gameObject.SetActive(false);
    }

    private TMP_Text CreateTooltipText(RectTransform parent, string name, int fontSize, FontStyles fontStyle, Vector2 position, Vector2 size)
    {
        TMP_Text text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        text.transform.SetParent(parent, false);
        text.font = bodyText != null && bodyText.font != null ? bodyText.font : UIManager.GetDefaultTMPFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
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
        return EventDetailTextUtility.GetOptionEffectText(option);
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
        TMP_Text recipeText = recipeRoot.GetComponent<TMP_Text>();
        if (recipeText != null)
            recipeText.text = string.Empty;

        for (int i = recipeRoot.childCount - 1; i >= 0; i--)
            Destroy(recipeRoot.GetChild(i).gameObject);

        MaterialEnum[] materials = EventModel.ParseRecipe(option != null ? option.recipe : null);
        if (option != null && option.isExitOption)
            materials = System.Array.Empty<MaterialEnum>();
        float spacing = 60f;
        float startX = materials.Length > 1 ? -spacing * (materials.Length - 1) * 0.5f : 0f;
        for (int i = 0; i < materials.Length; i++)
        {
            Image icon = new GameObject("MaterialIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(recipeRoot, false);
            Sprite sprite = MaterialCardView.GetMaterialIcon(materials[i]);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.color = sprite != null ? Color.white : MaterialCardView.GetMaterialColor(materials[i]);
            icon.raycastTarget = false;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(startX + spacing * i, 0f);
            iconRect.sizeDelta = new Vector2(96f, 96f);
        }
    }

    private static TMP_Text FindText(RectTransform root, string name)
    {
        Transform child = root.Find(name);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
