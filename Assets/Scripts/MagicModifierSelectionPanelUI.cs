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
    private readonly List<Image> optionIcons = new List<Image>();
    private readonly List<SpringLineHighlightUI> optionBackgrounds = new List<SpringLineHighlightUI>();
    private readonly List<SpringLineHighlightUI> optionSelectedHighlights = new List<SpringLineHighlightUI>();
    private readonly List<EnchantIconUI> optionEnchantIcons = new List<EnchantIconUI>();
    private readonly List<MagicModifierData> currentChoices = new List<MagicModifierData>();
    private readonly List<MaterialModifierData> currentMaterialChoices = new List<MaterialModifierData>();

    private const float OptionWidth = 168f;
    private const float OptionHeight = 92f;
    private const float SelectedOptionScale = 1.06f;
    private const float OptionIconSize = 51f;
    private const float OptionIconY = 14f;
    /// <summary>箭头附魔图标尺寸（与战斗结算奖励的图标区同为 64）。</summary>
    private const float EnchantIconSize = 64f;
    /// <summary>选项名字所在行（参考战斗结算奖励：图标在上、名字在下）。</summary>
    private const float OptionNameY = -31f;
    /// <summary>选项间距（参考战斗结算奖励：196 宽 + 34 间隔 = 230）。</summary>
    private const float OptionSpacing = 230f;
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
    private Action cancelled;
    private Action<MaterialModifierData> materialModifierSelected;
    private bool materialModifierMode;
    private Sprite fallbackModifierIcon;
    [Tooltip("箭头附魔图标预制体（Assets/Prefabs/UI/EnchantIcon.prefab）：上层 + 底色两层图片合成，颜色由 Enchant_Color 配置决定。")]
    [SerializeField] private EnchantIconUI enchantIconPrefab;

    public MagicModifierData SelectedModifier => selectedModifier;
    public bool HasSelectedModifier => selectedModifier != null;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        panel = (RectTransform)transform;
        CacheReferences();
        LocalizationSystem.LanguageChanged -= RefreshLocalizedContent;
        LocalizationSystem.LanguageChanged += RefreshLocalizedContent;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        LocalizationSystem.LanguageChanged -= RefreshLocalizedContent;
        popupTween?.Kill(false);
    }

    public bool ShouldUseMobileInteraction()
    {
        return owner != null && owner.ShouldUseMobileInteraction();
    }

    public void Show(IReadOnlyList<MagicModifierData> choices, Action completed, Action cancelled = null)
    {
        materialModifierMode = false;
        materialModifierSelected = null;
        selectedMaterialModifier = null;
        hoveredOptionIndex = -1;
        this.completed = completed;
        this.cancelled = cancelled ?? completed;
        selectedModifier = null;
        currentChoices.Clear();
        currentMaterialChoices.Clear();
        for (int i = 0; choices != null && i < choices.Count; i++)
        {
            if (choices[i] != null)
                currentChoices.Add(choices[i]);
        }

        CacheReferences();
        ResetOptionHoverEffects();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (titleText != null)
            titleText.text = LocalizationSystem.GetText("ui.magic_modifier.panel.title", "选择道具强化");
        if (hintText != null)
            hintText.text = LocalizationSystem.GetText("ui.magic_modifier.panel.hint", "选择一个强化后，点击一个已有道具完成附魔。每个道具只能附魔一次。");
        HideSelectedHint();
        RefreshOptions();
    }

    public void ShowMaterialModifierChoices(IReadOnlyList<MaterialModifierData> choices, Action<MaterialModifierData> selected, Action completed, Action cancelled = null)
    {
        materialModifierMode = true;
        materialModifierSelected = selected;
        this.completed = completed;
        this.cancelled = cancelled ?? completed;
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
        ResetOptionHoverEffects();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (titleText != null)
            titleText.text = LocalizationSystem.GetText("ui.magic_modifier.panel.material_title", "选择箭头附魔");
        if (hintText != null)
            hintText.text = LocalizationSystem.GetText("ui.magic_modifier.panel.material_hint", "选择一个附魔后，再选择一个箭头应用。后来的附魔会覆盖旧附魔。");
        HideSelectedHint();
        RefreshOptions();
    }

    private void RefreshLocalizedContent()
    {
        if (this == null || !gameObject.activeInHierarchy)
            return;

        CacheReferences();
        if (titleText != null)
            titleText.text = materialModifierMode
                ? LocalizationSystem.GetText("ui.magic_modifier.panel.material_title", "选择箭头附魔")
                : LocalizationSystem.GetText("ui.magic_modifier.panel.title", "选择道具强化");
        if (hintText != null)
            hintText.text = materialModifierMode
                ? LocalizationSystem.GetText("ui.magic_modifier.panel.material_hint", "选择一个附魔后，再选择一个箭头应用。后来的附魔会覆盖旧附魔。")
                : LocalizationSystem.GetText("ui.magic_modifier.panel.hint", "选择一个强化后，点击一个已有道具完成附魔。每个道具只能附魔一次。");
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
        ResetOptionHoverEffects();
        if (popupRoot != null)
            popupRoot.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    public void CompleteSelection()
    {
        Action callback = completed;
        completed = null;
        cancelled = null;
        Hide();
        callback?.Invoke();
    }

    public void CancelSelection()
    {
        Action callback = cancelled;
        completed = null;
        cancelled = null;
        Hide();
        callback?.Invoke();
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
            backButton.onClick.AddListener(CancelSelection);
            // 返回按钮文案已由美术统一改为图标（X）：不再在运行时写文字。
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

        HideEnchantIcons();

        if (currentChoices.Count == 0)
        {
            LayoutOptionButtons(1);
            optionButtons[0].gameObject.SetActive(true);
            optionButtons[0].interactable = false;
            if (optionTexts[0] != null)
                optionTexts[0].text = LocalizationSystem.GetText("ui.magic_modifier.panel.empty", "暂无可用道具强化");
            SetOptionTextVisible(0, true);
            ConfigureOptionTextLayout(0, false);
            SetOptionIconVisible(0, false);
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
            SetOptionTextVisible(i, true);
            ConfigureOptionTextLayout(i, true);
            SetMagicModifierOptionIcon(i, data);
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
                optionTexts[0].text = LocalizationSystem.GetText("ui.magic_modifier.panel.material_empty", "暂无可用箭头附魔");
            SetOptionTextVisible(0, true);
            ConfigureOptionTextLayout(0, false);
            SetOptionIconVisible(0, false);
            SetEnchantIconVisible(0, false, null);
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
            // 与「道具强化」模式一致：图标在上、名字在下（布局参考战斗结算奖励的选项）。
            if (optionTexts[i] != null)
                optionTexts[i].text = BuildMaterialOptionText(data);
            SetOptionTextVisible(i, true);
            ConfigureOptionTextLayout(i, true);
            SetOptionIconVisible(i, false);
            SetEnchantIconVisible(i, true, data);
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
        // 与战斗结算奖励的道具选项一致：选项 196 宽 + 34 间隔 = 230。
        return OptionSpacing;
    }

    private string BuildOptionText(MagicModifierData data)
    {
        return data != null ? LocalizationSystem.GetText(data.nameKey, data.id) : string.Empty;
    }

    /// <summary>箭头附魔选项的名字（显示在图标下方，取自附魔本地化 nameKey）。</summary>
    private string BuildMaterialOptionText(MaterialModifierData data)
    {
        if (data == null)
            return string.Empty;

        return LocalizationSystem.GetText(data.nameKey, data.id);
    }

    private void ConfigureOptionTextLayout(int index, bool withIcon)
    {
        if (index < 0 || index >= optionTexts.Count || optionTexts[index] == null)
            return;

        TMP_Text text = optionTexts[index];
        text.alignment = TextAlignmentOptions.Center;
        RectTransform rect = text.transform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(OptionWidth - 22f, withIcon ? 28f : OptionHeight - 18f);
        rect.anchoredPosition = new Vector2(0f, withIcon ? OptionNameY : 0f);
    }

    private void SetMagicModifierOptionIcon(int index, MagicModifierData data)
    {
        if (index < 0 || index >= optionIcons.Count)
            return;

        Image icon = optionIcons[index];
        if (icon == null)
            return;

        Sprite sprite = MagicModifierIconDatabase.Get(data);
        bool hasIcon = sprite != null;
        icon.sprite = hasIcon ? sprite : GetFallbackModifierIcon();
        icon.color = hasIcon ? Color.white : new Color(1f, 0.88f, 0.38f, 1f);
        icon.preserveAspect = true;
        icon.gameObject.SetActive(icon.sprite != null);
    }

    private void SetOptionIconVisible(int index, bool visible)
    {
        if (index >= 0 && index < optionIcons.Count && optionIcons[index] != null)
            optionIcons[index].gameObject.SetActive(visible);
    }

    private void SetOptionTextVisible(int index, bool visible)
    {
        if (index < 0 || index >= optionTexts.Count || optionTexts[index] == null)
            return;

        optionTexts[index].gameObject.SetActive(visible);
    }

    /// <summary>显示/隐藏某个选项上的箭头附魔图标（两层合成）；显示时按附魔 id 刷新两层颜色。</summary>
    private void SetEnchantIconVisible(int index, bool visible, MaterialModifierData data)
    {
        EnchantIconUI icon = visible ? EnsureOptionEnchantIcon(index) : GetOptionEnchantIcon(index);
        if (icon == null)
            return;

        if (!visible)
        {
            icon.gameObject.SetActive(false);
            return;
        }

        icon.Apply(data);
        icon.gameObject.SetActive(true);
        icon.transform.SetAsLastSibling();
    }

    private void HideEnchantIcons()
    {
        for (int i = 0; i < optionEnchantIcons.Count; i++)
        {
            if (optionEnchantIcons[i] != null)
                optionEnchantIcons[i].gameObject.SetActive(false);
        }
    }

    private EnchantIconUI GetOptionEnchantIcon(int index)
    {
        return index >= 0 && index < optionEnchantIcons.Count ? optionEnchantIcons[index] : null;
    }

    /// <summary>选项上已存在的附魔图标（首次构建时为 null，由 <see cref="EnsureOptionEnchantIcon"/> 补上）。</summary>
    private EnchantIconUI FindExistingEnchantIcon(RectTransform optionRect)
    {
        if (optionRect == null)
            return null;

        Transform existing = optionRect.Find("EnchantIcon");
        return existing != null ? existing.GetComponent<EnchantIconUI>() : null;
    }

    private EnchantIconUI EnsureOptionEnchantIcon(int index)
    {
        RectTransform optionRect = GetOptionRect(index);
        if (optionRect == null || enchantIconPrefab == null)
            return null;

        while (optionEnchantIcons.Count <= index)
            optionEnchantIcons.Add(null);
        if (optionEnchantIcons[index] != null)
            return optionEnchantIcons[index];

        Transform existing = optionRect.Find("EnchantIcon");
        EnchantIconUI icon = existing != null ? existing.GetComponent<EnchantIconUI>() : null;
        if (icon == null)
        {
            icon = Instantiate(enchantIconPrefab, optionRect);
            icon.name = "EnchantIcon";
        }

        RectTransform rect = icon.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            // 图标在上、名字在下（布局参考战斗结算奖励的选项）。
            rect.anchoredPosition = new Vector2(0f, OptionIconY);
            rect.sizeDelta = new Vector2(EnchantIconSize, EnchantIconSize);
        }

        icon.gameObject.SetActive(false);
        optionEnchantIcons[index] = icon;
        return icon;
    }

    private RectTransform GetOptionRect(int index)
    {
        if (index < 0 || index >= optionButtons.Count || optionButtons[index] == null)
            return null;

        return optionButtons[index].transform as RectTransform;
    }

    private Sprite GetFallbackModifierIcon()
    {
        if (fallbackModifierIcon == null)
            fallbackModifierIcon = Resources.Load<Sprite>("Images/UI/箭头");
        return fallbackModifierIcon;
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
        {
            if (!instant)
                option.localEulerAngles = Vector3.zero;
            motion.SetBaseScale(scale, instant);
        }
        if (instant)
        {
            option.localScale = scale;
        }
        else
        {
            option.DOScale(scale, 0.16f).SetEase(Ease.OutBack).SetTarget(this);
            option.DOLocalRotate(Vector3.zero, 0.16f).SetEase(Ease.OutBack).SetTarget(this);
        }

        if (index < optionSelectedHighlights.Count && optionSelectedHighlights[index] != null)
            optionSelectedHighlights[index].gameObject.SetActive(selected);
        RefreshOptionFrameColor(index);
    }

    public void ConfirmTouchOption(int index)
    {
        if (materialModifierMode)
            SelectMaterialModifierOption(index);
        else
            SelectOption(index);
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

    /// <summary>选项详情内容随当前可选列表变化，所以用 Provider 注入。</summary>
    private UnifiedDetailContent BuildOptionDetailContent(int index)
    {
        if (materialModifierMode)
        {
            return index >= 0 && index < currentMaterialChoices.Count
                ? UnifiedDetailContentBuilder.Build(currentMaterialChoices[index])
                : default;
        }

        return index >= 0 && index < currentChoices.Count
            ? UnifiedDetailContentBuilder.Build(currentChoices[index])
            : default;
    }

    private void ResetOptionHoverEffects()
    {
        hoveredOptionIndex = -1;
        for (int i = 0; i < optionButtons.Count; i++)
        {
            Button button = optionButtons[i];
            if (button == null)
                continue;

            Transform option = button.transform;
            option.DOKill(false);
            option.localScale = Vector3.one;
            option.localEulerAngles = Vector3.zero;

            JuicyMotion motion = button.GetComponent<JuicyMotion>();
            if (motion != null)
                motion.CaptureCurrentTransformAsBase(true);

            RefreshOptionFrameColor(i);
        }
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
        optionIcons.Clear();
        optionBackgrounds.Clear();
        optionSelectedHighlights.Clear();
        optionEnchantIcons.Clear();
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
            optionIcons.Add(EnsureOptionIcon(button.transform as RectTransform));
            optionEnchantIcons.Add(FindExistingEnchantIcon(button.transform as RectTransform));
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

    /// <summary>
    /// 详情与手势统一交给 UnifiedDetailTriggerUI：PC 悬停弹详情并高亮选项，PE 长按看详情、短按确认选项。
    /// </summary>
    private void ConfigureOptionHover(Button button, int index)
    {
        UnifiedDetailTriggerUI trigger = button.GetComponent<UnifiedDetailTriggerUI>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<UnifiedDetailTriggerUI>();
        trigger.SetAnchor(button);
        trigger.SetContentProvider(() => BuildOptionDetailContent(index));
        trigger.SetHoverActions(hovering => SetOptionHovered(index, hovering));
        trigger.SetAction(() => ConfirmTouchOption(index));
    }

    private Image EnsureOptionIcon(RectTransform optionRect)
    {
        if (optionRect == null)
            return null;

        Transform existing = optionRect.Find("Icon");
        Image icon = existing != null ? existing.GetComponent<Image>() : null;
        if (icon == null)
        {
            GameObject obj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(optionRect, false);
            icon = obj.GetComponent<Image>();
            icon.raycastTarget = false;
        }

        RectTransform iconRect = icon.transform as RectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, OptionIconY);
        iconRect.sizeDelta = new Vector2(OptionIconSize, OptionIconSize);
        icon.transform.SetAsLastSibling();
        icon.gameObject.SetActive(false);
        return icon;
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
        // 步进帧率与道具栏（MagicSlot_PC）线框一致：道具强化与箭头附魔共用本面板，
        // 帧率不能只依赖场景里的旧值（可能仍为 12）。
        spring.SetAnimationFramesPerSecond(SpringLineHighlightUI.StandardSteppedFrameRate);
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
