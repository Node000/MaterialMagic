using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>结算三选一的内容：金币追加量、单件道具、单只箭头；某项为 0/null 表示不提供该选项。</summary>
public class BattleRewardChoices
{
    public int GoldAmount;
    public MagicData Magic;
    public RewardArrowOption Arrow;
}

public class RewardArrowOption
{
    public MaterialEnum material;
    public MaterialModifierData modifierData;

    public bool HasModifier => modifierData != null;
}

public sealed class RewardChoiceHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RewardPanelUI owner;
    private RectTransform slotRect;
    private UnifiedDetailContent? detail;
    private SpringLineHighlightUI hoverFrame;

    public void Initialize(RewardPanelUI owner, RectTransform slotRect, UnifiedDetailContent? detail, SpringLineHighlightUI hoverFrame)
    {
        this.owner = owner;
        this.slotRect = slotRect;
        this.detail = detail;
        this.hoverFrame = hoverFrame;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
            owner.SetChoiceHover(slotRect, detail, hoverFrame, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
            owner.SetChoiceHover(slotRect, detail, hoverFrame, false);
    }
}

public class RewardOptionView : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Button button;

    public void Bind(string label, Action onClick)
    {
        CacheReferences();
        if (labelText != null)
            labelText.text = label;
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void CacheReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (labelText == null)
            labelText = UIManager.FindChildComponent<TMP_Text>(transform, "Text");
        if (labelText == null)
            labelText = GetComponentInChildren<TMP_Text>(true);
    }
}

public class RewardPanelUI : MonoBehaviour
{
    [SerializeField] private Vector2 choiceSlotSize = new Vector2(200f, 150f);
    [SerializeField] private float choiceSlotSpacing = 220f;
    [SerializeField] private string goldCoinSpritePath = "Images/Bonus/coin1";
    [SerializeField] private Vector2 magicChoiceCellSize = new Vector2(196f, 92f);
    [SerializeField] private float magicChoiceSpacing = 230f;
    [SerializeField] private RectTransform materialCardPrefab;
    [Tooltip("结算“一个特定道具”用的独立奖励卡预制体（RewardItemCard，与道具栏的 MagicSlot_PC 分开）；留空时回退为克隆事件三选一的卡面。")]
    [SerializeField] private RectTransform rewardItemCardPrefab;

    private readonly List<MagicItemView> rewardMagicViews = new List<MagicItemView>();
    private HandSystemUI owner;
    private Button endButton;

    // 美术统一把面板按钮改成图标后，按钮文案由美术控制；代码只负责点击行为。
    [Header("场景绑定（优先于按名字查找）")]
    [Tooltip("事件道具奖励面板；留空时先按名字找父物体下的 RewardMagicChoicePanel，找不到再运行时创建（PE / PvOnly 旧场景）。")]
    [SerializeField] private RectTransform magicChoicePanel;
    [Tooltip("事件道具奖励面板的返回按钮；留空时按名字在面板内查找。")]
    [SerializeField] private Button magicChoiceBackButton;
    [Tooltip("结算箭头槽下方的名称文案；留空时按名字查找，再找不到才运行时创建（旧场景）。")]
    [SerializeField] private TMP_Text arrowChoiceLabel;

    private RectTransform magicChoiceContent;
    private bool magicClaimed;
    private MagicItemView selectedMagicView;
    private MagicItemView hoveredMagicView;
    private Tween selectedMagicTween;
    private bool magicOnlyMode;
    private Action magicOnlyCompleted;
    private RewardOptionView magicOnlyOptionView;
    private List<MagicData> currentMagicChoices = new List<MagicData>();
    private RectTransform cachedMaterialCardPrefab;
    private Coroutine magicChoicePrewarmRoutine;
    private bool magicChoicesPrebound;

    // 结算三选一（战斗胜利）。
    private BattleRewardChoices currentChoices;
    private bool settlementClaimed;
    private bool claimInProgress;
    private RectTransform choiceArea;
    private RectTransform optionArea;
    private RectTransform goldChoiceSlot;
    private RectTransform itemChoiceSlot;
    private RectTransform arrowChoiceSlot;
    private TMP_Text goldChoiceLabel;
    private MagicItemView itemChoiceCard;
    private MagicModel itemChoicePreview;
    private RectTransform arrowChoiceCard;
    private MaterialModel arrowChoicePreview;
    private RectTransform hoveredChoiceSlot;
    private SpringLineHighlightUI hoveredChoiceFrame;

    private const float SelectedMagicScale = 1.24f;
    private const float HoverMagicScaleBonus = 0.08f;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        CacheReferences();
        gameObject.SetActive(false);
    }

    public void Show()
    {
        if (owner == null)
            return;

        magicOnlyMode = false;
        magicOnlyCompleted = null;
        magicClaimed = false;
        settlementClaimed = false;
        claimInProgress = false;
        currentMagicChoices = new List<MagicData>();
        currentChoices = new BattleRewardChoices
        {
            GoldAmount = RollGoldChoiceAmount(),
            Magic = owner.RollBattleRewardMagic(),
            Arrow = owner.RollBattleRewardArrow()
        };
        selectedMagicView = null;
        hoveredMagicView = null;
        owner.SelectPendingRewardMagic(null);
        gameObject.SetActive(true);
        SetTitle(LocalizationSystem.GetText("ui.reward_panel.title", "选择一个奖励"));
        CacheReferences();
        ClearChoiceHover(false);
        HideMagicChoices();
        HideArrowChoiceCard();
        RefreshChoiceSlots();
        owner.GetUIManager().TutorialManager?.OnRewardPanelShown();
    }

    private int RollGoldChoiceAmount()
    {
        // 进阶效果 13：累计 <=0 时结算不提供“更多金币”选项。
        if (DifficultyUpgradeSystem.ModifyBattleRewardGoldChoiceCount(1) <= 0)
            return 0;
        return Mathf.Max(0, owner.PendingBattleGoldReward);
    }

    private void SetTitle(string text)
    {
        TMP_Text title = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (title != null)
            title.text = text;
    }

    public void ShowMagicOnly(Action completed)
    {
        if (owner == null)
            return;

        magicOnlyMode = true;
        magicOnlyCompleted = completed;
        magicClaimed = false;
        settlementClaimed = false;
        claimInProgress = false;
        currentChoices = null;
        currentMagicChoices = owner.GetRewardMagicChoices(3);
        selectedMagicView = null;
        hoveredMagicView = null;
        owner.SelectPendingRewardMagic(null);
        gameObject.SetActive(true);
        SetTitle(LocalizationSystem.GetText("ui.reward_panel.magic_only.title", "道具奖励"));
        CacheReferences();
        ClearChoiceHover(false);
        HideMagicChoices();
        HideArrowChoiceCard();
        SetChoiceAreaVisible(false);
        RefreshMagicOnlyOption();
        ScheduleMagicChoicePrewarm();
    }

    public void Hide()
    {
        StopMagicChoicePrewarm();
        owner?.SelectPendingRewardMagic(null);
        ClearChoiceHover(false);
        HideMagicChoices();
        HideArrowChoiceCard();
        SetChoiceAreaVisible(false);
        currentChoices = null;
        gameObject.SetActive(false);
    }

    public RectTransform SelectedMagicRect => selectedMagicView != null ? selectedMagicView.transform as RectTransform : null;

    public void UndoMagicRewardClaim()
    {
        if (!magicClaimed)
            return;

        magicClaimed = false;
        selectedMagicView = null;
        hoveredMagicView = null;
        owner?.SelectPendingRewardMagic(null);
        HideMagicChoices();
        RefreshMagicOnlyOption();
    }

    public void CompleteMagicRewardSelection()
    {
        if (magicOnlyMode)
        {
            magicClaimed = true;
            owner?.SelectPendingRewardMagic(null);
            HideMagicChoices();
            CompleteMagicOnlyReward();
            return;
        }

        // 结算：道具奖励装备完成后直接结束结算并进入商店。
        settlementClaimed = true;
        claimInProgress = false;
        CloseSettlementAndFinish();
    }

    private void CompleteMagicOnlyReward()
    {
        Action completed = magicOnlyCompleted;
        magicOnlyMode = false;
        magicOnlyCompleted = null;
        owner?.SelectPendingRewardMagic(null);
        HideMagicChoices();
        gameObject.SetActive(false);
        completed?.Invoke();
    }
    private void RefreshChoiceSlots()
    {
        bool settlement = !magicOnlyMode && currentChoices != null;
        SetChoiceAreaVisible(settlement);
        if (!settlement)
            return;

        EnsureChoiceSlots();
        bool interactive = !claimInProgress && !settlementClaimed;
        BindGoldChoice(interactive);
        BindItemChoice(interactive);
        BindArrowChoice(interactive);
        BindEndButton();
    }

    private void SetChoiceAreaVisible(bool visible)
    {
        if (choiceArea != null)
            choiceArea.gameObject.SetActive(visible);
        // 结算三选一与事件“获得道具”入口互斥：结算时藏定位区，事件时藏三选一。
        if (optionArea != null)
            optionArea.gameObject.SetActive(!visible);
        if (!visible)
        {
            HideArrowChoiceCard();
            ClearChoiceHover(false);
        }
    }

    private void BindEndButton()
    {
        if (endButton == null)
            return;

        endButton.onClick.RemoveAllListeners();
        if (magicOnlyMode)
            endButton.onClick.AddListener(CompleteMagicOnlyReward);
        else
            endButton.onClick.AddListener(OnLeaveClicked);
        endButton.interactable = !claimInProgress;
        // 按钮文案已由美术统一改为图标（X）：不再在运行时写文字，仅保留点击行为。
    }

    private void OnLeaveClicked()
    {
        if (claimInProgress)
            return;

        // 可以跳过：未领奖（或选了道具但未装备）时直接放弃本次奖励并进入商店。
        owner?.SelectPendingRewardMagic(null);
        settlementClaimed = true;
        claimInProgress = false;
        CloseSettlementAndFinish();
    }

    private void CloseSettlementAndFinish()
    {
        ClearChoiceHover(false);
        HideMagicChoices();
        HideArrowChoiceCard();
        SetChoiceAreaVisible(false);
        currentChoices = null;
        gameObject.SetActive(false);
        owner?.FinishReward();
    }

    private void BindGoldChoice(bool interactive)
    {
        if (goldChoiceSlot == null)
            return;

        bool visible = currentChoices.GoldAmount > 0 && !settlementClaimed;
        goldChoiceSlot.gameObject.SetActive(visible);
        if (!visible)
            return;

        EnsureGoldCoinIcon(goldChoiceSlot);
        if (goldChoiceLabel == null)
            goldChoiceLabel = ResolveOrCreateSlotText(goldChoiceSlot, "AmountText", new Vector2(0f, -44f), 22);
        if (goldChoiceLabel != null)
            goldChoiceLabel.text = string.Format(LocalizationSystem.GetText("ui.reward_panel.choice.gold", "金币x{0}"), currentChoices.GoldAmount);

        BindSlotButton(goldChoiceSlot, ClaimGoldChoice, interactive);
        EnsureSlotHover(goldChoiceSlot, null);
    }

    private void BindItemChoice(bool interactive)
    {
        if (itemChoiceSlot == null)
            return;

        MagicData data = currentChoices.Magic;
        bool visible = data != null && !settlementClaimed;
        itemChoiceSlot.gameObject.SetActive(visible);
        if (!visible)
            return;

        EnsureChoiceSlots();
        if (itemChoiceCard != null)
        {
            itemChoiceCard.gameObject.SetActive(true);
            itemChoicePreview = MagicFactory.Create(data);
            itemChoiceCard.Bind(itemChoicePreview);
            RectTransform cardRect = itemChoiceCard.transform as RectTransform;
            if (cardRect != null)
                DisableChildRaycasts(cardRect);
            if (itemChoicePreview != null)
                EnsureSlotHover(itemChoiceSlot, UnifiedDetailContentBuilder.Build(itemChoicePreview));
        }
        else
        {
            TMP_Text label = ResolveOrCreateSlotText(itemChoiceSlot, "NameText", Vector2.zero, 20);
            if (label != null)
                label.text = LocalizationSystem.GetText(data.nameKey, data.id);
        }

        BindSlotButton(itemChoiceSlot, SelectItemChoice, interactive);
    }

    private void BindArrowChoice(bool interactive)
    {
        if (arrowChoiceSlot == null)
            return;

        RewardArrowOption option = currentChoices.Arrow;
        bool visible = option != null && option.material != MaterialEnum.None && !settlementClaimed;
        arrowChoiceSlot.gameObject.SetActive(visible);
        if (!visible)
        {
            HideArrowChoiceCard();
            return;
        }

        if (arrowChoiceCard == null)
        {
            RectTransform content = ResolveOrCreateSlotContent(arrowChoiceSlot);
            MaterialModel preview;
            arrowChoiceCard = CreateSlotArrowCard(content, option, out preview);
            arrowChoicePreview = preview;
        }

        BindSlotButton(arrowChoiceSlot, ClaimArrowChoice, interactive);
        BindArrowChoiceLabel(option);
        if (arrowChoicePreview != null)
            EnsureSlotHover(arrowChoiceSlot, UnifiedDetailContentBuilder.Build(arrowChoicePreview));
    }

    /// <summary>箭头槽下方的名称文案（含附魔名），与金币槽的 AmountText 同位置同字号。</summary>
    private void BindArrowChoiceLabel(RewardArrowOption option)
    {
        if (arrowChoiceSlot == null)
            return;

        if (arrowChoiceLabel == null)
            arrowChoiceLabel = ResolveOrCreateSlotText(arrowChoiceSlot, "ArrowNameText", new Vector2(0f, -44f), 22);
        if (arrowChoiceLabel != null)
            arrowChoiceLabel.text = GetArrowOptionLabel(option);
    }

    private static void BindSlotButton(RectTransform slot, UnityEngine.Events.UnityAction action, bool interactive)
    {
        if (slot == null)
            return;

        Button button = slot.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        button.interactable = interactive;
    }

    private void ClaimGoldChoice()
    {
        if (claimInProgress || settlementClaimed || currentChoices == null || currentChoices.GoldAmount <= 0)
            return;

        StartCoroutine(ClaimGoldChoiceRoutine());
    }

    private IEnumerator ClaimGoldChoiceRoutine()
    {
        claimInProgress = true;
        RefreshChoiceSlots();
        yield return owner.GainGoldAnimated(currentChoices.GoldAmount, goldChoiceSlot, false);
        settlementClaimed = true;
        claimInProgress = false;
        CloseSettlementAndFinish();
    }

    private void SelectItemChoice()
    {
        if (claimInProgress || settlementClaimed || currentChoices == null || currentChoices.Magic == null)
            return;

        // 选中后进入现有放置流程：有空槽自动放入，否则点场景道具槽覆盖。
        claimInProgress = true;
        owner.SelectPendingRewardMagic(currentChoices.Magic);
        RefreshChoiceSlots();
    }

    private void ClaimArrowChoice()
    {
        if (claimInProgress || settlementClaimed || currentChoices == null || currentChoices.Arrow == null)
            return;

        StartCoroutine(ClaimArrowChoiceRoutine());
    }

    private IEnumerator ClaimArrowChoiceRoutine()
    {
        RewardArrowOption option = currentChoices.Arrow;
        RectTransform sourceRect = arrowChoiceCard != null ? arrowChoiceCard : arrowChoiceSlot;
        claimInProgress = true;
        RefreshChoiceSlots();
        yield return owner.GainRewardArrow(option, sourceRect);
        settlementClaimed = true;
        claimInProgress = false;
        CloseSettlementAndFinish();
    }

    private void EnsureChoiceSlots()
    {
        EnsureOptionArea();
        if (choiceArea == null)
        {
            Transform existing = transform.Find("ChoiceArea");
            choiceArea = existing as RectTransform;
        }
        if (choiceArea == null)
            choiceArea = CreateChoiceArea(transform);

        if (goldChoiceSlot == null)
            goldChoiceSlot = ResolveOrCreateChoiceSlot("ChoiceGold");
        if (itemChoiceSlot == null)
            itemChoiceSlot = ResolveOrCreateChoiceSlot("ChoiceItem");
        if (arrowChoiceSlot == null)
            arrowChoiceSlot = ResolveOrCreateChoiceSlot("ChoiceArrow");
        if (itemChoiceCard == null)
            itemChoiceCard = ResolveOrCreateItemCard();

        LayoutChoiceSlots();
    }

    private void LayoutChoiceSlots()
    {
        RectTransform[] slots = { goldChoiceSlot, itemChoiceSlot, arrowChoiceSlot };
        float spacing = Mathf.Max(1f, choiceSlotSpacing);
        Vector2 size = new Vector2(Mathf.Max(1f, choiceSlotSize.x), Mathf.Max(1f, choiceSlotSize.y));
        for (int i = 0; i < slots.Length; i++)
        {
            RectTransform slot = slots[i];
            if (slot == null)
                continue;

            slot.anchorMin = new Vector2(0.5f, 0.5f);
            slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = new Vector2((i - 1) * spacing, 0f);
            slot.sizeDelta = size;
        }
    }

    private RectTransform EnsureOptionArea()
    {
        if (optionArea == null)
            optionArea = transform.Find("OptionArea") as RectTransform;
        return optionArea;
    }

    private static RectTransform CreateChoiceArea(Transform parent)
    {
        RectTransform area = new GameObject("ChoiceArea", typeof(RectTransform)).GetComponent<RectTransform>();
        area.SetParent(parent, false);
        area.anchorMin = new Vector2(0.5f, 0.5f);
        area.anchorMax = new Vector2(0.5f, 0.5f);
        area.pivot = new Vector2(0.5f, 0.5f);
        area.anchoredPosition = new Vector2(0f, -20f);
        area.sizeDelta = new Vector2(660f, 150f);
        area.localScale = Vector3.one;
        return area;
    }

    private RectTransform ResolveOrCreateChoiceSlot(string name)
    {
        Transform parent = choiceArea != null ? choiceArea : transform;
        Transform existing = parent.Find(name);
        if (existing == null)
            existing = transform.Find(name);
        if (existing != null)
        {
            EnsureSlotButton(existing as RectTransform);
            return existing as RectTransform;
        }

        // 未改造过的场景：优先拷贝现行槽位（RewardOption0）保留美术样式，否则退回纯色 Image。
        RectTransform styled = CreateChoiceSlotFromTemplate(parent, name);
        if (styled != null)
        {
            EnsureSlotButton(styled);
            return styled;
        }

        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        // 奖励槽不需要背景：只保留可点击区域（悬停反馈由 SpringLineHighlight 框提供）。
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = choiceSlotSize;
        rect.localScale = Vector3.one;
        return rect;
    }

    private RectTransform CreateChoiceSlotFromTemplate(Transform parent, string name)
    {
        Transform optionRoot = transform.Find("OptionArea");
        if (optionRoot == null || optionRoot.childCount == 0)
            return null;

        RectTransform template = optionRoot.GetChild(0) as RectTransform;
        if (template == null || template.GetComponent<Image>() == null)
            return null;

        RectTransform slot = Instantiate(template, parent);
        slot.name = name;
        slot.gameObject.SetActive(true);
        slot.localScale = Vector3.one;

        RewardOptionView staleView = slot.GetComponent<RewardOptionView>();
        if (staleView != null)
            Destroy(staleView);

        Image slotImage = slot.GetComponent<Image>();
        if (slotImage != null)
        {
            slotImage.color = new Color(0f, 0f, 0f, 0f);
            slotImage.raycastTarget = true;
        }

        // 模板自带的按钮文字是旧选项文案，清空并改名为金币槽可复用的 AmountText。
        Transform staleText = slot.Find("Text");
        if (staleText != null && staleText.GetComponent<TMP_Text>() != null)
        {
            staleText.name = "AmountText";
            TMP_Text staleLabel = staleText.GetComponent<TMP_Text>();
            if (staleLabel != null)
                staleLabel.text = string.Empty;
        }
        return slot;
    }

    private static void EnsureSlotButton(RectTransform slot)
    {
        if (slot == null || slot.GetComponent<Button>() != null)
            return;

        Image image = slot.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = true;
        slot.gameObject.AddComponent<Button>();
    }

    private static RectTransform ResolveOrCreateSlotContent(RectTransform slot)
    {
        if (slot == null)
            return null;

        Transform existing = slot.Find("Content");
        if (existing != null)
            return existing as RectTransform;

        RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(slot, false);
        content.anchorMin = new Vector2(0.5f, 0.5f);
        content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(196f, 130f);
        content.localScale = Vector3.one;
        return content;
    }

    private TMP_Text ResolveOrCreateSlotText(RectTransform slot, string name, Vector2 anchoredPosition, int fontSize)
    {
        if (slot == null)
            return null;

        TMP_Text existing = UIManager.FindChildComponent<TMP_Text>(slot, name);
        if (existing != null)
            return existing;

        return CreatePanelText(slot, name, string.Empty, fontSize, FontStyles.Bold, anchoredPosition, new Vector2(184f, 36f));
    }

    private void EnsureGoldCoinIcon(RectTransform slot)
    {
        if (slot == null || slot.Find("CoinIcon") != null)
            return;

        Sprite sprite = string.IsNullOrEmpty(goldCoinSpritePath) ? null : Resources.Load<Sprite>(goldCoinSpritePath);
        if (sprite == null)
            return;

        Image icon = new GameObject("CoinIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        icon.transform.SetParent(slot, false);
        icon.sprite = sprite;
        icon.raycastTarget = false;
        RectTransform rect = icon.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 30f);
        rect.sizeDelta = new Vector2(54f, 54f);
        rect.localScale = Vector3.one;
    }

    private MagicItemView ResolveOrCreateItemCard()
    {
        if (itemChoiceSlot == null)
            return null;

        RectTransform content = ResolveOrCreateSlotContent(itemChoiceSlot);
        if (content == null)
            return null;

        MagicItemView existing = content.GetComponentInChildren<MagicItemView>(true);
        if (existing != null)
            return existing;

        // 优先用结算专用的奖励卡预制体（版式与道具栏槽位不同）；未绑定才回退克隆事件三选一卡面。
        if (rewardItemCardPrefab != null)
        {
            RectTransform card = Instantiate(rewardItemCardPrefab, content);
            card.name = "ItemCard";
            card.gameObject.SetActive(true);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.localScale = Vector3.one;
            return card.GetComponent<MagicItemView>();
        }

        MagicItemView template = rewardMagicViews.Count > 0 ? rewardMagicViews[0] : null;
        if (template == null)
            return null;

        MagicItemView clone = Instantiate(template, content);
        clone.name = "ItemCard";
        clone.gameObject.SetActive(true);
        RectTransform rect = clone.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = GetMagicChoiceCellSize();
            rect.localScale = Vector3.one;
        }
        return clone;
    }
    private void ShowMagicChoices()
    {
        if (magicClaimed)
            return;

        StopMagicChoicePrewarm();
        owner.GetUIManager().TutorialManager?.OnMagicRewardChoicesShown();
        EnsureMagicChoicePanel();
        magicChoicePanel.gameObject.SetActive(true);
        magicChoicePanel.SetAsLastSibling();

        List<MagicData> choices = currentMagicChoices;
        int visibleChoiceCount = Mathf.Min(choices.Count, rewardMagicViews.Count);
        Vector2 cellSize = GetMagicChoiceCellSize();
        float spacing = GetMagicChoiceSpacing();
        float startX = visibleChoiceCount > 1 ? -spacing * (visibleChoiceCount - 1) * 0.5f : 0f;
        for (int i = 0; i < rewardMagicViews.Count; i++)
        {
            MagicItemView view = rewardMagicViews[i];
            if (view == null)
                continue;

            bool visible = i < visibleChoiceCount;
            view.gameObject.SetActive(visible);
            if (!visible)
                continue;

            RectTransform rect = (RectTransform)view.transform;
            rect.SetParent(magicChoiceContent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(startX + spacing * i, 0f);
            ApplyMagicChoiceCellSize(rect, cellSize);
            rect.localScale = GetRewardMagicTargetScale(view);
            UIManager.RemoveJuicyMotion(view.transform);

            MagicData data = choices[i];
            if (!magicChoicesPrebound)
                view.Bind(MagicFactory.Create(data));
            Button button = view.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectMagicReward(data, view));
            }
            ConfigureMagicChoiceHover(view);
            view.gameObject.SetActive(true);
            SetRewardMagicHighlightVisible(view, view == selectedMagicView || view == hoveredMagicView);
        }
    }

    private void ScheduleMagicChoicePrewarm()
    {
        StopMagicChoicePrewarm();
        magicChoicesPrebound = false;
        magicChoicePrewarmRoutine = StartCoroutine(PrewarmMagicChoicesRoutine());
    }

    private void StopMagicChoicePrewarm()
    {
        if (magicChoicePrewarmRoutine != null)
        {
            StopCoroutine(magicChoicePrewarmRoutine);
            magicChoicePrewarmRoutine = null;
        }
    }

    private IEnumerator PrewarmMagicChoicesRoutine()
    {
        yield return null;
        if (!gameObject.activeInHierarchy || magicClaimed)
        {
            magicChoicePrewarmRoutine = null;
            yield break;
        }

        EnsureMagicChoicePanel();
        List<MagicData> choices = currentMagicChoices;
        int choiceCount = Mathf.Min(choices.Count, rewardMagicViews.Count);
        for (int i = 0; i < choiceCount; i++)
        {
            MagicItemView view = rewardMagicViews[i];
            if (view != null)
                view.Bind(MagicFactory.Create(choices[i]));
        }
        magicChoicesPrebound = true;
        HideMagicChoices();
        magicChoicePrewarmRoutine = null;
    }

    private void SelectMagicReward(MagicData data, MagicItemView view)
    {
        if (magicClaimed)
            return;

        selectedMagicView = view;
        owner.SelectPendingRewardMagic(data);
        RefreshSelectedMagicVisuals();
    }

    private void RefreshSelectedMagicVisuals()
    {
        selectedMagicTween?.Kill(false);
        for (int i = 0; i < rewardMagicViews.Count; i++)
        {
            MagicItemView rewardView = rewardMagicViews[i];
            if (rewardView == null)
                continue;

            Transform rewardTransform = rewardView.transform;
            rewardTransform.DOKill(false);
            SetRewardMagicHighlightVisible(rewardView, rewardView == selectedMagicView || rewardView == hoveredMagicView);
            Tween tween = rewardTransform.DOScale(GetRewardMagicTargetScale(rewardView), 0.16f).SetEase(Ease.OutBack).SetTarget(this);
            if (rewardView == selectedMagicView)
                selectedMagicTween = tween;
        }
    }

    private void OnRewardMagicHoverChanged(MagicItemView view, bool hovering)
    {
        if (hovering)
            hoveredMagicView = view;
        else if (hoveredMagicView == view)
            hoveredMagicView = null;

        RefreshSelectedMagicVisuals();
    }

    private Vector3 GetRewardMagicTargetScale(MagicItemView view)
    {
        float scale = view == selectedMagicView ? SelectedMagicScale : 1f;
        if (view == hoveredMagicView)
            scale += HoverMagicScaleBonus;
        return Vector3.one * scale;
    }

    private void SetRewardMagicHighlightVisible(MagicItemView view, bool visible)
    {
        SpringLineHighlightUI highlight = FindRewardMagicHighlight(view);
        if (highlight == null)
            return;

        highlight.color = Color.white;
        highlight.gameObject.SetActive(visible);
    }

    private SpringLineHighlightUI FindRewardMagicHighlight(MagicItemView view)
    {
        if (view == null)
            return null;

        SpringLineHighlightUI[] highlights = view.GetComponentsInChildren<SpringLineHighlightUI>(true);
        for (int i = 0; i < highlights.Length; i++)
        {
            if (highlights[i] != null && highlights[i].transform != view.transform)
                return highlights[i];
        }
        return highlights.Length > 0 ? highlights[0] : null;
    }

    private void ConfigureMagicChoiceHover(MagicItemView view)
    {
        SpringLineHighlightUI highlight = FindRewardMagicHighlight(view);
        HoverHighlightTargetRelayUI relay = view != null ? view.GetComponent<HoverHighlightTargetRelayUI>() : null;
        if (relay != null && highlight != null)
            relay.Unregister(highlight.gameObject);

        EventTrigger trigger = view.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = view.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();
        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => OnRewardMagicHoverChanged(view, true));
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => OnRewardMagicHoverChanged(view, false));
        trigger.triggers.Add(exit);
    }

    private void ReturnFromMagicChoices()
    {
        HideMagicChoices();
        selectedMagicView = null;
        hoveredMagicView = null;
        owner.SelectPendingRewardMagic(null);
    }

    private void HideMagicChoices()
    {
        selectedMagicTween?.Kill(false);
        selectedMagicTween = null;
        hoveredMagicView = null;
        if (magicChoicePanel != null)
            magicChoicePanel.gameObject.SetActive(false);

        RectTransform rewardParent = (RectTransform)transform;
        for (int i = 0; i < rewardMagicViews.Count; i++)
        {
            if (rewardMagicViews[i] != null)
            {
                RectTransform rect = (RectTransform)rewardMagicViews[i].transform;
                rect.SetParent(rewardParent, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-160f + i * 160f, -18f);
                ApplyMagicChoiceCellSize(rect, GetMagicChoiceCellSize());
                rect.localScale = Vector3.one;
                SetRewardMagicHighlightVisible(rewardMagicViews[i], false);
                rewardMagicViews[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>箭头奖励卡（结算面板上的“一个特定箭头”）。</summary>
    private RectTransform CreateSlotArrowCard(RectTransform parent, RewardArrowOption option, out MaterialModel preview)
    {
        preview = new MaterialModel("reward_choice_arrow", option.material);
        if (option.HasModifier)
        {
            MaterialModifierModel modifier = MaterialModifierFactory.Create(option.modifierData);
            if (modifier != null)
                preview.AddModifier(modifier);
        }

        RectTransform prefab = GetMaterialCardPrefab();
        if (prefab == null || parent == null)
        {
            TMP_Text fallback = CreatePanelText(parent != null ? parent : (RectTransform)transform, "NameText", GetArrowOptionLabel(option), 20, FontStyles.Bold, Vector2.zero, new Vector2(180f, 40f));
            fallback.raycastTarget = false;
            return null;
        }

        RectTransform card = Instantiate(prefab, parent);
        card.name = "ArrowCard";
        card.gameObject.SetActive(true);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = new Vector2(92f, 132f);
        card.localScale = Vector3.one;

        MaterialCardView cardView = card.GetComponent<MaterialCardView>();
        if (cardView != null)
        {
            cardView.Bind(preview);
            DisableChildRaycasts(card);
        }
        CenterCardVisual(card);
        return card;
    }

    /// <summary>材质卡面的美术内容不一定居中于卡片矩形（如箭头图标靠顶端），按实际子节点中心校正一次。</summary>
    private static void CenterCardVisual(RectTransform card)
    {
        if (card == null || card.childCount == 0)
            return;

        Vector3 cardCenter = card.TransformPoint(card.rect.center);
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < card.childCount; i++)
        {
            RectTransform child = card.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf || child.GetComponent<Graphic>() == null)
                continue;
            sum += child.TransformPoint(child.rect.center).y;
            count++;
        }
        if (count == 0)
            return;

        float scale = Mathf.Abs(card.lossyScale.y);
        if (scale < 0.0001f)
            scale = 1f;
        float deltaPixels = (sum / count - cardCenter.y) / scale;
        if (Mathf.Abs(deltaPixels) < 0.5f)
            return;

        card.anchoredPosition = new Vector2(card.anchoredPosition.x, card.anchoredPosition.y - deltaPixels);
    }

    private void HideArrowChoiceCard()
    {
        if (arrowChoiceCard != null)
        {
            Destroy(arrowChoiceCard.gameObject);
            arrowChoiceCard = null;
        }
        arrowChoicePreview = null;
    }

    private void EnsureSlotHover(RectTransform slot, UnifiedDetailContent? detail)
    {
        if (slot == null)
            return;

        SpringLineHighlightUI frame = EnsureSlotHoverFrame(slot);
        RewardChoiceHoverRelay relay = slot.GetComponent<RewardChoiceHoverRelay>();
        if (relay == null)
            relay = slot.gameObject.AddComponent<RewardChoiceHoverRelay>();
        relay.Initialize(this, slot, detail, frame);
    }

    private static SpringLineHighlightUI EnsureSlotHoverFrame(RectTransform slot)
    {
        Transform existing = slot.Find("HoverFrame");
        if (existing != null)
            return existing.GetComponent<SpringLineHighlightUI>();

        GameObject frameObject = new GameObject("HoverFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(SpringLineHighlightUI));
        frameObject.transform.SetParent(slot, false);
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        SpringLineHighlightUI frame = frameObject.GetComponent<SpringLineHighlightUI>();
        frame.SetShape(SpringLineHighlightUI.HighlightShape.RoundedRect);
        frame.SetLineCount(2);
        frame.SetLineWidth(2.5f);
        frame.SetOutset(2.7f);
        frame.SetFillEnabled(false);
        frame.SetBindHoverTarget(false);
        frame.SetHideOnAwake(false);
        frame.color = Color.white;
        frame.raycastTarget = false;
        frameRect.SetAsLastSibling();
        frameObject.SetActive(false);
        return frame;
    }

    internal void SetChoiceHover(RectTransform slotRect, UnifiedDetailContent? detail, SpringLineHighlightUI hoverFrame, bool hovering)
    {
        if (slotRect == null)
            return;

        if (!hovering)
        {
            if (hoveredChoiceSlot == slotRect)
                ClearChoiceHover(true);
            return;
        }

        if (hoveredChoiceSlot == slotRect)
            return;

        ClearChoiceHover(true);
        hoveredChoiceSlot = slotRect;
        hoveredChoiceFrame = hoverFrame;
        if (hoverFrame != null)
            hoverFrame.gameObject.SetActive(true);

        slotRect.DOKill(false);
        slotRect.DOScale(Vector3.one * 1.05f, 0.16f).SetEase(Ease.OutBack);
        if (owner != null && detail.HasValue)
        {
            UIManager uiManager = owner.GetUIManager();
            if (uiManager != null)
                uiManager.ShowUnifiedDetailPopup(slotRect, detail.Value);
        }
    }

    private void ClearChoiceHover(bool animate)
    {
        RectTransform slotRect = hoveredChoiceSlot;
        SpringLineHighlightUI hoverFrame = hoveredChoiceFrame;
        hoveredChoiceSlot = null;
        hoveredChoiceFrame = null;
        if (hoverFrame != null)
            hoverFrame.gameObject.SetActive(false);
        if (slotRect == null)
            return;

        slotRect.DOKill(false);
        if (animate)
            slotRect.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutQuad);
        else
            slotRect.localScale = Vector3.one;

        if (owner != null)
        {
            UIManager uiManager = owner.GetUIManager();
            if (uiManager != null)
                uiManager.HideUnifiedDetailPopup(slotRect);
        }
    }

    private RectTransform GetMaterialCardPrefab()
    {
        if (materialCardPrefab != null)
        {
            cachedMaterialCardPrefab = materialCardPrefab;
            return materialCardPrefab;
        }
        if (cachedMaterialCardPrefab != null)
            return cachedMaterialCardPrefab;

        PrefabReferenceLibrary library = GetComponentInParent<PrefabReferenceLibrary>();
        if (library != null)
            cachedMaterialCardPrefab = library.MaterialCardPrefab;
        return cachedMaterialCardPrefab;
    }

    private static string GetArrowOptionLabel(RewardArrowOption option)
    {
        if (option == null)
            return string.Empty;

        string label = LocalizationKeys.GetMaterialName(option.material);
        if (option.HasModifier && !string.IsNullOrEmpty(option.modifierData.nameKey))
            label = label + " · " + LocalizationSystem.GetText(option.modifierData.nameKey, option.modifierData.id);
        return label;
    }

    private static void DisableChildRaycasts(RectTransform root)
    {
        if (root == null)
            return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }
    private Vector2 GetMagicChoiceCellSize()
    {
        return new Vector2(Mathf.Max(1f, magicChoiceCellSize.x), Mathf.Max(1f, magicChoiceCellSize.y));
    }

    private float GetMagicChoiceSpacing()
    {
        return Mathf.Max(1f, magicChoiceSpacing);
    }

    private static void ApplyMagicChoiceCellSize(RectTransform rect, Vector2 size)
    {
        rect.sizeDelta = size;
        LayoutElement[] layoutElements = rect.GetComponents<LayoutElement>();
        for (int i = 0; i < layoutElements.Length; i++)
        {
            if (layoutElements[i] == null)
                continue;
            layoutElements[i].preferredWidth = size.x;
            layoutElements[i].preferredHeight = size.y;
        }
    }

    private void EnsureMagicChoicePanel()
    {
        if (magicChoicePanel != null)
        {
            CacheMagicChoicePanelReferences();
            return;
        }

        RectTransform existingPanel = transform.parent != null ? transform.parent.Find("RewardMagicChoicePanel") as RectTransform : null;
        if (existingPanel != null)
        {
            magicChoicePanel = existingPanel;
            CacheMagicChoicePanelReferences();
            return;
        }

        RectTransform sourceRect = (RectTransform)transform;
        Image panelImage = new GameObject("RewardMagicChoicePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        magicChoicePanel = panelImage.rectTransform;
        magicChoicePanel.SetParent(transform.parent, false);
        magicChoicePanel.anchorMin = sourceRect.anchorMin;
        magicChoicePanel.anchorMax = sourceRect.anchorMax;
        magicChoicePanel.pivot = sourceRect.pivot;
        magicChoicePanel.anchoredPosition = sourceRect.anchoredPosition;
        magicChoicePanel.sizeDelta = sourceRect.sizeDelta;
        magicChoicePanel.localScale = Vector3.one;
        panelImage.color = new Color(0.02f, 0.02f, 0.04f, 1f);
        panelImage.raycastTarget = true;

        TMP_Text title = CreatePanelText(magicChoicePanel, "Title", LocalizationSystem.GetText("ui.reward_panel.magic_choice.title", "选择一个道具"), 26, FontStyles.Bold, new Vector2(0f, 112f), new Vector2(360f, 40f));
        title.color = new Color(1f, 0.9f, 0.55f, 1f);
        TMP_Text hint = CreatePanelText(magicChoicePanel, "Hint", LocalizationSystem.GetText("ui.reward_panel.magic_choice.hint", "选择后点击下方/场景中的道具槽覆盖；可重新选择。"), 16, FontStyles.Normal, new Vector2(0f, 72f), new Vector2(620f, 30f));
        hint.color = new Color(0.82f, 0.84f, 0.9f, 1f);

        magicChoiceBackButton = CreatePanelButton(magicChoicePanel, "BackButton", LocalizationSystem.GetText("ui.common.back", "返回"), new Vector2(-360f, 112f), new Vector2(110f, 42f));
        BindMagicChoiceBackButton();

        magicChoiceContent = new GameObject("MagicChoices", typeof(RectTransform)).GetComponent<RectTransform>();
        magicChoiceContent.SetParent(magicChoicePanel, false);
        magicChoiceContent.anchorMin = new Vector2(0.5f, 0.5f);
        magicChoiceContent.anchorMax = new Vector2(0.5f, 0.5f);
        magicChoiceContent.pivot = new Vector2(0.5f, 0.5f);
        magicChoiceContent.anchoredPosition = new Vector2(0f, -24f);
        magicChoiceContent.sizeDelta = new Vector2(760f, 120f);
        CacheMagicChoicePanelReferences();

        for (int i = 0; i < rewardMagicViews.Count; i++)
        {
            if (rewardMagicViews[i] != null)
                rewardMagicViews[i].transform.SetParent(magicChoiceContent, false);
        }
    }

    private void CacheMagicChoicePanelReferences()
    {
        if (magicChoicePanel == null)
            return;

        TMP_Text title = UIManager.FindChildComponent<TMP_Text>(magicChoicePanel, "Title");
        if (title != null)
            title.text = LocalizationSystem.GetText("ui.reward_panel.magic_choice.title", "选择一个道具");
        TMP_Text hint = UIManager.FindChildComponent<TMP_Text>(magicChoicePanel, "Hint");
        if (hint != null)
            hint.text = LocalizationSystem.GetText("ui.reward_panel.magic_choice.hint", "选择后点击下方/场景中的道具槽覆盖；可重新选择。");

        if (magicChoiceBackButton == null)
            magicChoiceBackButton = FindMagicChoiceBackButton();
        BindMagicChoiceBackButton();

        magicChoiceContent = UIManager.FindChildRect(magicChoicePanel, "MagicChoices");
        if (magicChoiceContent == null)
        {
            magicChoiceContent = new GameObject("MagicChoices", typeof(RectTransform)).GetComponent<RectTransform>();
            magicChoiceContent.SetParent(magicChoicePanel, false);
            magicChoiceContent.anchorMin = new Vector2(0.5f, 0.5f);
            magicChoiceContent.anchorMax = new Vector2(0.5f, 0.5f);
            magicChoiceContent.pivot = new Vector2(0.5f, 0.5f);
            magicChoiceContent.anchoredPosition = new Vector2(0f, -24f);
            magicChoiceContent.sizeDelta = new Vector2(760f, 120f);
        }
    }

    /// <summary>
    /// 未在 Inspector 绑定时才走名字查找。美术把返回按钮放到窗口底框下、且底框可能改名，
    /// 所以先找面板直接子物体，再按名字递归找，不依赖底框的具体名字。
    /// </summary>
    private Button FindMagicChoiceBackButton()
    {
        if (magicChoicePanel == null)
            return null;

        Transform direct = magicChoicePanel.Find("BackButton");
        if (direct != null)
        {
            Button directButton = direct.GetComponent<Button>();
            if (directButton != null)
                return directButton;
        }

        Button[] buttons = magicChoicePanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == "BackButton")
                return buttons[i];
        }

        return null;
    }

    private void BindMagicChoiceBackButton()
    {
        if (magicChoiceBackButton == null)
            return;

        magicChoiceBackButton.onClick.RemoveAllListeners();
        magicChoiceBackButton.onClick.AddListener(ReturnFromMagicChoices);
        // 按钮文案已由美术统一改为图标（X）：不再在运行时写文字。
    }

    private TMP_Text CreatePanelText(RectTransform parent, string name, string text, int fontSize, FontStyles fontStyle, Vector2 anchoredPosition, Vector2 size)
    {
        TMP_Text label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.transform.SetParent(parent, false);
        label.font = UIManager.GetDefaultTMPFont();
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = TextAlignmentOptions.Center;
        label.text = text;
        label.raycastTarget = false;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return label;
    }

    private Button CreatePanelButton(RectTransform parent, string name, string text, Vector2 anchoredPosition, Vector2 size)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(JuicyMotion)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = new Color(0.09f, 0.09f, 0.14f, 1f);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text label = CreatePanelText(rect, "Text", text, 18, FontStyles.Bold, Vector2.zero, size);
        label.color = Color.white;
        return image.GetComponent<Button>();
    }

    /// <summary>事件道具奖励（ShowMagicOnly）的单入口按钮：点击后进入三选一卡片面板。</summary>
    private void RefreshMagicOnlyOption()
    {
        if (!magicOnlyMode)
            return;

        EnsureOptionArea();
        if (optionArea != null)
            optionArea.gameObject.SetActive(true);
        EnsureMagicOnlyOptionView();
        HideOtherMagicOnlyOptions();
        if (magicOnlyOptionView == null)
            return;

        RectTransform optionRect = magicOnlyOptionView.transform as RectTransform;
        if (optionRect != null)
        {
            optionRect.anchorMin = new Vector2(0.5f, 0.5f);
            optionRect.anchorMax = new Vector2(0.5f, 0.5f);
            optionRect.pivot = new Vector2(0.5f, 0.5f);
            optionRect.anchoredPosition = Vector2.zero;
            optionRect.sizeDelta = new Vector2(170f, 54f);
        }

        if (!magicClaimed)
            magicOnlyOptionView.Bind(LocalizationSystem.GetText("ui.reward_panel.option.magic", "获得道具"), ShowMagicChoices);
        else
            magicOnlyOptionView.Hide();

        BindEndButton();
    }

    private void HideOtherMagicOnlyOptions()
    {
        if (optionArea == null || magicOnlyOptionView == null)
            return;

        Button[] buttons = optionArea.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || buttons[i].gameObject == magicOnlyOptionView.gameObject)
                continue;
            buttons[i].gameObject.SetActive(false);
        }
    }

    private void EnsureMagicOnlyOptionView()
    {
        if (magicOnlyOptionView != null)
            return;

        Transform optionRoot = EnsureOptionArea();
        if (optionRoot == null)
            return;

        Button[] buttons = optionRoot.GetComponentsInChildren<Button>(true);
        System.Array.Sort(buttons, (Button a, Button b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        for (int i = 0; i < buttons.Length; i++)
        {
            RewardOptionView view = buttons[i].GetComponent<RewardOptionView>();
            if (view == null)
                view = buttons[i].gameObject.AddComponent<RewardOptionView>();
            magicOnlyOptionView = view;
            break;
        }

        if (magicOnlyOptionView == null)
            magicOnlyOptionView = CreateMagicOnlyOptionView(optionRoot as RectTransform);
    }

    private RewardOptionView CreateMagicOnlyOptionView(RectTransform parent)
    {
        Image image = new GameObject("MagicOnlyOption", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(RewardOptionView), typeof(JuicyMotion)).GetComponent<Image>();
        image.transform.SetParent(parent != null ? parent : transform, false);
        image.color = new Color(0.08f, 0.08f, 0.12f, 1f);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(170f, 54f);

        TMP_Text text = CreatePanelText(rect, "Text", string.Empty, 18, FontStyles.Bold, Vector2.zero, new Vector2(170f, 54f));
        text.color = Color.white;
        return image.GetComponent<RewardOptionView>();
    }
    private void CacheReferences()
    {
        rewardMagicViews.Clear();
        MagicItemView[] views = GetComponentsInChildren<MagicItemView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            // 结算道具槽里的卡面不属于事件三选一的候选。
            if (IsSettlementChoiceView(views[i]))
                continue;
            rewardMagicViews.Add(views[i]);
        }

        if (magicChoicePanel != null)
        {
            MagicItemView[] panelViews = magicChoicePanel.GetComponentsInChildren<MagicItemView>(true);
            for (int i = 0; i < panelViews.Length; i++)
            {
                if (!rewardMagicViews.Contains(panelViews[i]))
                    rewardMagicViews.Add(panelViews[i]);
            }
        }
        rewardMagicViews.Sort(CompareMagicRewardViewNames);

        if (endButton == null)
            endButton = UIManager.FindChildComponent<Button>(transform, "EndButton");
        if (magicOnlyOptionView == null)
            EnsureMagicOnlyOptionView();
    }

    private static bool IsSettlementChoiceView(MagicItemView view)
    {
        if (view == null)
            return false;

        Transform current = view.transform.parent;
        while (current != null)
        {
            if (current.name == "ChoiceArea")
                return true;
            current = current.parent;
        }
        return false;
    }

    private static int CompareMagicRewardViewNames(MagicItemView left, MagicItemView right)
    {
        return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
    }
}
