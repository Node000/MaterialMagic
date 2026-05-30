using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MagicModifierSelectionPanelUI : MonoBehaviour
{
    private readonly List<Button> optionButtons = new List<Button>();
    private readonly List<Text> optionTexts = new List<Text>();
    private readonly List<MagicModifierData> currentChoices = new List<MagicModifierData>();

    private HandSystemUI owner;
    private RectTransform panel;
    private RectTransform optionRoot;
    private Text titleText;
    private Text hintText;
    private Button backButton;
    private RectTransform popupRoot;
    private Text popupText;
    private CanvasGroup popupCanvasGroup;
    private Tween popupTween;
    private MagicModifierData selectedModifier;
    private Action completed;

    public MagicModifierData SelectedModifier => selectedModifier;
    public bool HasSelectedModifier => selectedModifier != null;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        panel = (RectTransform)transform;
        CacheReferences();
        gameObject.SetActive(false);
    }

    public void Show(IReadOnlyList<MagicModifierData> choices, Action completed)
    {
        this.completed = completed;
        selectedModifier = null;
        currentChoices.Clear();
        for (int i = 0; choices != null && i < choices.Count; i++)
        {
            if (choices[i] != null)
                currentChoices.Add(choices[i]);
        }

        CacheReferences();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (titleText != null)
            titleText.text = LocalizationSystem.GetText("ui.magic_modifier.panel.title", "选择法术强化");
        if (hintText != null)
            hintText.text = LocalizationSystem.GetText("ui.magic_modifier.panel.hint", "选择一个强化后，点击一个已有法术完成附魔。每个法术只能附魔一次。");

        EnsureOptionCount(Mathf.Max(1, currentChoices.Count));
        RefreshOptions();
    }

    public void Hide()
    {
        selectedModifier = null;
        gameObject.SetActive(false);
    }

    public void CompleteSelection()
    {
        Hide();
        completed?.Invoke();
        completed = null;
    }

    public void ShowPopup(string message)
    {
        CacheReferences();
        if (popupRoot == null || popupCanvasGroup == null || popupText == null)
            return;

        popupText.text = message;
        popupTween?.Kill(false);
        popupRoot.gameObject.SetActive(true);
        popupRoot.SetAsLastSibling();
        PopupLayerUtility.ApplyTo(popupRoot);
        popupCanvasGroup.alpha = 0f;
        popupRoot.localScale = new Vector3(0.72f, 0.72f, 1f);

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Append(popupCanvasGroup.DOFade(1f, 0.12f));
        sequence.Join(popupRoot.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack));
        sequence.AppendInterval(0.72f);
        sequence.Append(popupCanvasGroup.DOFade(0f, 0.14f));
        sequence.Join(popupRoot.DOScale(new Vector3(0.82f, 0.82f, 1f), 0.16f).SetEase(Ease.InBack));
        popupTween = sequence.OnComplete(() => popupRoot.gameObject.SetActive(false));
    }

    private void CacheReferences()
    {
        titleText = titleText != null ? titleText : UIManager.FindChildComponent<Text>(transform, "Title");
        hintText = hintText != null ? hintText : UIManager.FindChildComponent<Text>(transform, "Hint");
        optionRoot = optionRoot != null ? optionRoot : UIManager.FindChildRect(transform, "OptionArea");
        backButton = backButton != null ? backButton : UIManager.FindChildComponent<Button>(transform, "BackButton");
        if (optionRoot == null)
            optionRoot = CreateRect("OptionArea", panel, new Vector2(0f, -10f), new Vector2(700f, 150f));
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CompleteSelection);
            Text backText = UIManager.FindChildComponent<Text>(backButton.transform, "Text");
            if (backText != null)
                backText.text = LocalizationSystem.GetText("ui.magic_modifier.panel.back", "返回");
        }
        EnsurePopup();
    }

    private void RefreshOptions()
    {
        if (currentChoices.Count == 0)
        {
            optionButtons[0].gameObject.SetActive(true);
            optionButtons[0].interactable = false;
            optionTexts[0].text = LocalizationSystem.GetText("ui.magic_modifier.panel.empty", "暂无可用法术强化");
            for (int i = 1; i < optionButtons.Count; i++)
                optionButtons[i].gameObject.SetActive(false);
            return;
        }

        for (int i = 0; i < optionButtons.Count; i++)
        {
            bool visible = i < currentChoices.Count;
            optionButtons[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            MagicModifierData data = currentChoices[i];
            optionButtons[i].interactable = true;
            optionTexts[i].text = BuildOptionText(data);
            int index = i;
            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => SelectOption(index));
            SetOptionSelected(i, data == selectedModifier, true);
        }
    }

    private string BuildOptionText(MagicModifierData data)
    {
        string name = LocalizationSystem.GetText(data.nameKey, data.id);
        string desc = LocalizationSystem.GetText(data.descriptionKey, string.Empty);
        return string.IsNullOrEmpty(desc) ? name : name + "\n" + desc;
    }

    private void SelectOption(int index)
    {
        if (index < 0 || index >= currentChoices.Count)
            return;

        selectedModifier = currentChoices[index];
        owner?.SelectPendingMagicModifier(selectedModifier);
        for (int i = 0; i < optionButtons.Count; i++)
            SetOptionSelected(i, i == index, false);
    }

    private void SetOptionSelected(int index, bool selected, bool instant)
    {
        if (index < 0 || index >= optionButtons.Count)
            return;

        Transform option = optionButtons[index].transform;
        option.DOKill(false);
        Vector3 scale = selected ? Vector3.one * 1.12f : Vector3.one;
        if (instant)
            option.localScale = scale;
        else
            option.DOScale(scale, 0.16f).SetEase(Ease.OutBack).SetTarget(this);
    }

    private void EnsureOptionCount(int count)
    {
        while (optionButtons.Count < count)
            CreateOption(optionButtons.Count);
    }

    private void CreateOption(int index)
    {
        Image image = new GameObject("ModifierOption" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(JuicyMotion)).GetComponent<Image>();
        image.transform.SetParent(optionRoot, false);
        image.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2((index - 1) * 230f, 0f);
        rect.sizeDelta = new Vector2(210f, 112f);

        Text text = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(rect, false);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 8f);
        textRect.offsetMax = new Vector2(-10f, -8f);

        optionButtons.Add(image.GetComponent<Button>());
        optionTexts.Add(text);
    }

    private void EnsurePopup()
    {
        if (popupRoot == null)
        {
            Transform existing = transform.Find("Popup");
            popupRoot = existing as RectTransform;
        }
        if (popupRoot == null)
        {
            Image image = new GameObject("Popup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)).GetComponent<Image>();
            image.transform.SetParent(transform, false);
            image.color = new Color(0.03f, 0.03f, 0.04f, 0.98f);
            image.raycastTarget = false;
            popupRoot = image.rectTransform;
            popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
            popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
            popupRoot.pivot = new Vector2(0.5f, 0.5f);
            popupRoot.anchoredPosition = new Vector2(0f, -148f);
            popupRoot.sizeDelta = new Vector2(320f, 54f);
        }

        popupCanvasGroup = popupRoot.GetComponent<CanvasGroup>();
        if (popupCanvasGroup == null)
            popupCanvasGroup = popupRoot.gameObject.AddComponent<CanvasGroup>();
        popupText = popupText != null ? popupText : UIManager.FindChildComponent<Text>(popupRoot, "Text");
        if (popupText == null)
        {
            popupText = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            popupText.transform.SetParent(popupRoot, false);
            popupText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            popupText.fontSize = 18;
            popupText.fontStyle = FontStyle.Bold;
            popupText.alignment = TextAnchor.MiddleCenter;
            popupText.color = new Color(1f, 0.86f, 0.56f, 1f);
            popupText.raycastTarget = false;
            RectTransform rect = popupText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        popupCanvasGroup.alpha = 0f;
        popupRoot.gameObject.SetActive(false);
    }

    private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }
}
