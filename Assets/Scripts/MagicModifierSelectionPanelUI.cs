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
    private readonly List<MagicModifierData> currentChoices = new List<MagicModifierData>();

    private HandSystemUI owner;
    private RectTransform panel;
    private RectTransform optionRoot;
    private TMP_Text titleText;
    private TMP_Text hintText;
    private Button backButton;
    private RectTransform popupRoot;
    private TMP_Text popupText;
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

        RefreshOptions();
    }

    public void Hide()
    {
        selectedModifier = null;
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
        if (optionButtons.Count == 0)
            return;

        if (currentChoices.Count == 0)
        {
            optionButtons[0].gameObject.SetActive(true);
            optionButtons[0].interactable = false;
            if (optionTexts[0] != null)
                optionTexts[0].text = LocalizationSystem.GetText("ui.magic_modifier.panel.empty", "暂无可用法术强化");
            for (int i = 1; i < optionButtons.Count; i++)
                optionButtons[i].gameObject.SetActive(false);
            return;
        }

        int visibleCount = Mathf.Min(currentChoices.Count, optionButtons.Count);
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

    private void CacheOptionReferences()
    {
        optionButtons.Clear();
        optionTexts.Clear();
        if (optionRoot == null)
            return;

        for (int i = 0; i < optionRoot.childCount; i++)
        {
            Button button = optionRoot.GetChild(i).GetComponent<Button>();
            if (button == null)
                continue;

            optionButtons.Add(button);
            optionTexts.Add(UIManager.FindChildComponent<TMP_Text>(button.transform, "Text"));
        }
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
