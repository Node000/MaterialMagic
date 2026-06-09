using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MagicModifierSelectionPanelUI : MonoBehaviour
{
    private readonly List<Button> optionButtons = new List<Button>();
    private readonly List<TMP_Text> optionTexts = new List<TMP_Text>();
    private readonly List<SpringLineHighlightUI> optionBackgrounds = new List<SpringLineHighlightUI>();
    private readonly List<SpringLineHighlightUI> optionSelectedHighlights = new List<SpringLineHighlightUI>();
    private readonly List<MagicModifierData> currentChoices = new List<MagicModifierData>();
    private readonly List<MaterialModifierData> currentMaterialChoices = new List<MaterialModifierData>();

    private const float OptionWidth = 168f;
    private const float OptionHeight = 89.6f;
    private const float SelectedOptionScale = 1.06f;
    private static readonly Color OptionFrameColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    private static readonly Color SelectedOptionFrameColor = Color.white;

    private HandSystemUI owner;
    private RectTransform panel;
    private RectTransform optionRoot;
    private TMP_Text titleText;
    private TMP_Text hintText;
    private TMP_Text selectedHintText;
    private Button backButton;
    private RectTransform popupRoot;
    private TMP_Text popupText;
    private CanvasGroup popupCanvasGroup;
    private Tween popupTween;
    private MagicModifierData selectedModifier;
    private MaterialModifierData selectedMaterialModifier;
    private int hoveredOptionIndex = -1;
    private Action completed;
    private Action<MaterialModifierData> materialModifierSelected;
    private bool materialModifierMode;

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
        materialModifierMode = false;
        materialModifierSelected = null;
        selectedMaterialModifier = null;
        hoveredOptionIndex = -1;
        this.completed = completed;
        selectedModifier = null;
        currentChoices.Clear();
        currentMaterialChoices.Clear();
        for (int i = 0; choices != null && i < choices.Count; i++)
        {
            if (choices[i] != null)
                currentChoices.Add(choices[i]);
        }

        CacheReferences();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (titleText != null)
            titleText.text = LocalizationSystem.GetText("ui.magic_modifier.panel.title", "选择道具强化");
        if (hintText != null)
            hintText.text = LocalizationSystem.GetText("ui.magic_modifier.panel.hint", "选择一个强化后，点击一个已有道具完成附魔。每个道具只能附魔一次。");
        HideSelectedHint();
        RefreshOptions();
    }

    public void ShowMaterialModifierChoices(IReadOnlyList<MaterialModifierData> choices, Action<MaterialModifierData> selected, Action completed)
    {
        materialModifierMode = true;
        materialModifierSelected = selected;
        this.completed = completed;
        selectedModifier = null;
        selectedMaterialModifier = null;
        hoveredOptionIndex = -1;
        currentChoices.Clear();
        currentMaterialChoices.Clear();
        for (int i = 0; choices != null && i < choices.Count; i++)
        {
            if (choices[i] != null)
                currentMaterialChoices.Add(choices[i]);
        }

        CacheReferences();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (titleText != null)
            titleText.text = "选择箭头附魔";
        if (hintText != null)
            hintText.text = "选择一个附魔后，再选择一个箭头应用。后来的附魔会覆盖旧附魔。";
        HideSelectedHint();
        RefreshOptions();
    }

    public void Hide()
    {
        selectedModifier = null;
        selectedMaterialModifier = null;
        hoveredOptionIndex = -1;
        materialModifierSelected = null;
        popupTween?.Kill(false);
        popupTween = null;
        if (popupRoot != null)
            popupRoot.gameObject.SetActive(false);
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
        if (panel == null)
            panel = (RectTransform)transform;
        titleText = titleText != null ? titleText : UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        hintText = hintText != null ? hintText : UIManager.FindChildComponent<TMP_Text>(transform, "Hint");
        selectedHintText = selectedHintText != null ? selectedHintText : UIManager.FindChildComponent<TMP_Text>(transform, "SelectedHint");
        HideSelectedHint();
        optionRoot = optionRoot != null ? optionRoot : UIManager.FindChildRect(transform, "OptionArea");
        backButton = backButton != null ? backButton : UIManager.FindChildComponent<Button>(transform, "BackButton");
        CacheOptionReferences();
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CompleteSelection);
            TMP_Text backText = UIManager.FindChildComponent<TMP_Text>(backButton.transform, "Text");
            if (backText != null)
                backText.text = LocalizationSystem.GetText("ui.magic_modifier.panel.back", "返回");
        }
        CachePopupReferences();
    }

    private void RefreshOptions()
    {
        if (materialModifierMode)
        {
            RefreshMaterialModifierOptions();
            return;
        }

        if (optionButtons.Count == 0)
            return;

        if (currentChoices.Count == 0)
        {
            LayoutOptionButtons(1);
            optionButtons[0].gameObject.SetActive(true);
            optionButtons[0].interactable = false;
            if (optionTexts[0] != null)
                optionTexts[0].text = LocalizationSystem.GetText("ui.magic_modifier.panel.empty", "暂无可用道具强化");
            for (int i = 1; i < optionButtons.Count; i++)
                optionButtons[i].gameObject.SetActive(false);
            return;
        }

        int visibleCount = Mathf.Min(currentChoices.Count, optionButtons.Count);
        LayoutOptionButtons(visibleCount);
        for (int i = 0; i < optionButtons.Count; i++)
        {
            bool visible = i < visibleCount;
            optionButtons[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            MagicModifierData data = currentChoices[i];
            optionButtons[i].interactable = true;
            if (optionTexts[i] != null)
                optionTexts[i].text = BuildOptionText(data);
            int index = i;
            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => SelectOption(index));
            SetOptionSelected(i, data == selectedModifier, true);
        }
    }

    private void RefreshMaterialModifierOptions()
    {
        if (optionButtons.Count == 0)
            return;

        if (currentMaterialChoices.Count == 0)
        {
            LayoutOptionButtons(1);
            optionButtons[0].gameObject.SetActive(true);
            optionButtons[0].interactable = false;
            if (optionTexts[0] != null)
                optionTexts[0].text = "暂无可用箭头附魔";
            for (int i = 1; i < optionButtons.Count; i++)
                optionButtons[i].gameObject.SetActive(false);
            return;
        }

        int visibleCount = Mathf.Min(currentMaterialChoices.Count, optionButtons.Count);
        LayoutOptionButtons(visibleCount);
        for (int i = 0; i < optionButtons.Count; i++)
        {
            bool visible = i < visibleCount;
            optionButtons[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            MaterialModifierData data = currentMaterialChoices[i];
            optionButtons[i].interactable = true;
            if (optionTexts[i] != null)
                optionTexts[i].text = BuildMaterialModifierOptionText(data);
            int index = i;
            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => SelectMaterialModifierOption(index));
            SetOptionSelected(i, data == selectedMaterialModifier, true);
        }
    }

    private void LayoutOptionButtons(int visibleCount)
    {
        if (visibleCount <= 0 || optionButtons.Count == 0)
            return;

        float spacing = GetOptionSpacing();
        float startX = visibleCount > 1 ? -spacing * (visibleCount - 1) * 0.5f : 0f;
        for (int i = 0; i < optionButtons.Count; i++)
        {
            RectTransform rect = optionButtons[i] != null ? optionButtons[i].transform as RectTransform : null;
            if (rect == null)
                continue;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            if (i < visibleCount)
                rect.anchoredPosition = new Vector2(startX + spacing * i, rect.anchoredPosition.y);
        }
    }

    private float GetOptionSpacing()
    {
        RectTransform first = optionButtons[0] != null ? optionButtons[0].transform as RectTransform : null;
        if (first == null || first.sizeDelta.x <= 0f)
            return 230f;

        return first.sizeDelta.x + 20f;
    }

    private string BuildOptionText(MagicModifierData data)
    {
        string name = LocalizationSystem.GetText(data.nameKey, data.id);
        string desc = LocalizationSystem.GetText(data.descriptionKey, string.Empty);
        return string.IsNullOrEmpty(desc) ? name : name + "\n" + desc;
    }

    private string BuildMaterialModifierOptionText(MaterialModifierData data)
    {
        string name = data != null && !string.IsNullOrEmpty(data.nameKey) ? LocalizationSystem.GetText(data.nameKey, data.id) : string.Empty;
        string desc = data != null && !string.IsNullOrEmpty(data.descriptionKey) ? LocalizationSystem.GetText(data.descriptionKey, string.Empty) : string.Empty;
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

    private void SelectMaterialModifierOption(int index)
    {
        if (index < 0 || index >= currentMaterialChoices.Count)
            return;

        selectedMaterialModifier = currentMaterialChoices[index];
        for (int i = 0; i < optionButtons.Count; i++)
            SetOptionSelected(i, i == index, false);
        materialModifierSelected?.Invoke(selectedMaterialModifier);
    }

    private void HideSelectedHint()
    {
        if (selectedHintText != null)
            selectedHintText.gameObject.SetActive(false);
    }

    private void SetOptionSelected(int index, bool selected, bool instant)
    {
        if (index < 0 || index >= optionButtons.Count)
            return;

        Transform option = optionButtons[index].transform;
        option.DOKill(false);
        Vector3 scale = selected ? Vector3.one * SelectedOptionScale : Vector3.one;
        JuicyMotion motion = optionButtons[index].GetComponent<JuicyMotion>();
        if (motion != null)
            motion.SetBaseScale(scale, instant);
        if (instant)
            option.localScale = scale;
        else
            option.DOScale(scale, 0.16f).SetEase(Ease.OutBack).SetTarget(this);

        if (index < optionSelectedHighlights.Count && optionSelectedHighlights[index] != null)
            optionSelectedHighlights[index].gameObject.SetActive(selected);
        RefreshOptionFrameColor(index);
    }

    public void SetOptionHovered(int index, bool hovered)
    {
        if (index < 0 || index >= optionButtons.Count)
            return;

        if (hovered)
            hoveredOptionIndex = index;
        else if (hoveredOptionIndex == index)
            hoveredOptionIndex = -1;

        RefreshOptionFrameColor(index);
    }

    private void RefreshOptionFrameColor(int index)
    {
        if (index < 0 || index >= optionBackgrounds.Count || optionBackgrounds[index] == null)
            return;

        optionBackgrounds[index].color = hoveredOptionIndex == index || IsOptionSelected(index) ? SelectedOptionFrameColor : OptionFrameColor;
        optionBackgrounds[index].SetVerticesDirty();
    }

    private bool IsOptionSelected(int index)
    {
        if (materialModifierMode)
            return index >= 0 && index < currentMaterialChoices.Count && currentMaterialChoices[index] == selectedMaterialModifier;

        return index >= 0 && index < currentChoices.Count && currentChoices[index] == selectedModifier;
    }

    private void CacheOptionReferences()
    {
        optionButtons.Clear();
        optionTexts.Clear();
        optionBackgrounds.Clear();
        optionSelectedHighlights.Clear();
        if (optionRoot == null)
            return;

        for (int i = 0; i < optionRoot.childCount; i++)
        {
            Button button = optionRoot.GetChild(i).GetComponent<Button>();
            if (button == null)
                continue;

            ConfigureOptionButton(button);
            ConfigureOptionHover(button, optionButtons.Count);
            optionButtons.Add(button);
            optionTexts.Add(UIManager.FindChildComponent<TMP_Text>(button.transform, "Text"));
            optionBackgrounds.Add(EnsureOptionSpring(button.transform as RectTransform, "SpringBackground", OptionFrameColor, true, true));
            RemoveOptionSpring(button.transform as RectTransform, "SpringHoverHighlight");
            optionSelectedHighlights.Add(EnsureOptionSpring(button.transform as RectTransform, "SpringSelectedHighlight", SelectedOptionFrameColor, false, false));
        }
    }

    private void ConfigureOptionButton(Button button)
    {
        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
            rect.sizeDelta = new Vector2(OptionWidth, OptionHeight);

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
        }
        button.transition = Selectable.Transition.None;
    }

    private void ConfigureOptionHover(Button button, int index)
    {
        MagicModifierOptionHoverUI hover = button.GetComponent<MagicModifierOptionHoverUI>();
        if (hover == null)
            hover = button.gameObject.AddComponent<MagicModifierOptionHoverUI>();
        hover.Initialize(this, index);
    }

    private SpringLineHighlightUI EnsureOptionSpring(RectTransform optionRect, string name, Color color, bool fill, bool active, GameObject hoverTarget = null)
    {
        if (optionRect == null)
            return null;

        Transform existing = optionRect.Find(name);
        SpringLineHighlightUI spring = existing != null ? existing.GetComponent<SpringLineHighlightUI>() : null;
        if (spring == null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SpringLineHighlightUI));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(optionRect, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            spring = obj.GetComponent<SpringLineHighlightUI>();
        }

        if (fill)
            spring.transform.SetAsFirstSibling();
        else
            spring.transform.SetAsLastSibling();
        spring.color = color;
        spring.SetShape(SpringLineHighlightUI.HighlightShape.RoundedRect);
        spring.SetLineCount(fill ? 2 : 1);
        spring.SetSamplesPerLine(120);
        spring.SetLineWidth(fill ? 1.5f : 2f);
        spring.SetLineSpacing(fill ? 1.5f : 2f);
        spring.SetOutset(fill ? 0f : 5f);
        spring.SetWobbleAmplitude(fill ? 4f : 6f);
        spring.SetFill(fill, Color.black);
        spring.SetHideOnAwake(false);
        spring.SetBindHoverTarget(hoverTarget != null);
        if (hoverTarget != null)
            spring.SetHoverTarget(hoverTarget);
        spring.gameObject.SetActive(active);
        return spring;
    }

    private void RemoveOptionSpring(RectTransform optionRect, string name)
    {
        if (optionRect == null)
            return;

        Transform existing = optionRect.Find(name);
        if (existing == null)
            return;

        if (Application.isPlaying)
            Destroy(existing.gameObject);
        else
            DestroyImmediate(existing.gameObject);
    }

    private void CachePopupReferences()
    {
        if (popupRoot == null)
        {
            Transform existing = transform.Find("Popup");
            popupRoot = existing as RectTransform;
        }
        if (popupRoot == null)
            return;

        popupCanvasGroup = popupRoot.GetComponent<CanvasGroup>();
        popupText = popupText != null ? popupText : UIManager.FindChildComponent<TMP_Text>(popupRoot, "Text");
        if (popupCanvasGroup != null)
            popupCanvasGroup.alpha = 0f;
        popupRoot.gameObject.SetActive(false);
    }
}
