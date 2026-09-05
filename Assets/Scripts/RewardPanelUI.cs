using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public enum RewardOptionKind
{
    None,
    Gold,
    Magic,
    MagicModifier,
    ArrowModifier
}

public class RewardOptionsModel
{
    public int GoldReward { get; }
    public List<MagicData> MagicChoices { get; }

    public RewardOptionsModel(int goldReward, List<MagicData> magicChoices)
    {
        GoldReward = goldReward;
        MagicChoices = magicChoices ?? new List<MagicData>();
    }
}

public class RewardArrowOption
{
    public MaterialEnum material;
    public MaterialModifierData modifierData;

    public bool HasModifier => modifierData != null;
}

public sealed class RewardArrowChoiceHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RewardPanelUI owner;
    private RectTransform choiceRect;
    private MaterialModel preview;
    private SpringLineHighlightUI hoverFrame;

    public void Initialize(RewardPanelUI owner, RectTransform choiceRect, MaterialModel preview, SpringLineHighlightUI hoverFrame)
    {
        this.owner = owner;
        this.choiceRect = choiceRect;
        this.preview = preview;
        this.hoverFrame = hoverFrame;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
            owner.SetArrowChoiceHover(choiceRect, preview, hoverFrame, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
            owner.SetArrowChoiceHover(choiceRect, preview, hoverFrame, false);
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
    [SerializeField] private int battleGoldReward = 1;
    [SerializeField] private Vector2 magicChoiceCellSize = new Vector2(196f, 92f);
    [SerializeField] private float magicChoiceSpacing = 230f;
    [SerializeField] private RectTransform materialCardPrefab;

    private readonly List<MagicItemView> rewardMagicViews = new List<MagicItemView>();
    private readonly List<RewardOptionView> optionViews = new List<RewardOptionView>();
    private HandSystemUI owner;
    private Button endButton;
    private RectTransform magicChoicePanel;
    private RectTransform magicChoiceContent;
    private Button magicChoiceBackButton;
    private bool goldClaimed;
    private bool goldClaimInProgress;
    private bool magicClaimed;
    private MagicItemView selectedMagicView;
    private MagicItemView hoveredMagicView;
    private Tween selectedMagicTween;
    private int currentGoldReward;
    private bool magicOnlyMode;
    private Action magicOnlyCompleted;
    private bool arrowRewardMode;
    private bool arrowClaimed;
    private readonly List<RewardArrowOption> currentArrowOptions = new List<RewardArrowOption>();
    private RectTransform arrowChoicePanel;
    private RectTransform arrowChoiceContent;
    private Button arrowChoiceBackButton;
    private RectTransform cachedMaterialCardPrefab;
    private RectTransform hoveredArrowChoice;
    private SpringLineHighlightUI hoveredArrowHighlight;
    private RewardOptionsModel currentRewardOptions;
    private RewardOptionKind eliteExtraRewardKind;
    private Coroutine magicChoicePrewarmRoutine;
    private bool magicChoicesPrebound;

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
        goldClaimed = false;
        goldClaimInProgress = false;
        magicClaimed = false;
        arrowRewardMode = true;
        arrowClaimed = false;
        currentArrowOptions.Clear();
        List<RewardArrowOption> arrowOptions = owner.GetRewardArrowOptions(3);
        if (arrowOptions != null)
            currentArrowOptions.AddRange(arrowOptions);
        currentRewardOptions = new RewardOptionsModel(RollBattleGoldReward(), arrowRewardMode ? new List<MagicData>() : owner.GetRewardMagicChoices(3));
        eliteExtraRewardKind = owner.RollEliteExtraRewardKind();
        currentGoldReward = currentRewardOptions.GoldReward;
        selectedMagicView = null;
        hoveredMagicView = null;
        owner.SelectPendingRewardMagic(null);
        gameObject.SetActive(true);
        TMP_Text title = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (title != null)
            title.text = LocalizationSystem.GetText("ui.reward_panel.title", "战斗奖励");

        TMP_Text hint = UIManager.FindChildComponent<TMP_Text>(transform, "Hint");
        if (hint != null)
            hint.text = arrowRewardMode
                ? LocalizationSystem.GetText("ui.reward_panel.arrow.hint", "选择奖励；领取箭头后会自动进入商店。")
                : LocalizationSystem.GetText("ui.reward_panel.hint", "选择奖励；道具奖励选中后，点击场景中的道具槽覆盖。");

        CacheReferences();
        HideMagicChoices();
        HideArrowChoices();
        RefreshOptions();
        if (!arrowRewardMode)
            ScheduleMagicChoicePrewarm();
        owner.GetUIManager().TutorialManager?.OnRewardPanelShown();
    }

    public void ShowMagicOnly(Action completed)
    {
        if (owner == null)
            return;

        magicOnlyMode = true;
        magicOnlyCompleted = completed;
        goldClaimed = true;
        goldClaimInProgress = false;
        magicClaimed = false;
        arrowRewardMode = false;
        arrowClaimed = false;
        HideArrowChoices();
        currentRewardOptions = new RewardOptionsModel(0, owner.GetRewardMagicChoices(3));
        eliteExtraRewardKind = RewardOptionKind.None;
        currentGoldReward = currentRewardOptions.GoldReward;
        selectedMagicView = null;
        hoveredMagicView = null;
        owner.SelectPendingRewardMagic(null);
        gameObject.SetActive(true);
        TMP_Text title = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (title != null)
            title.text = LocalizationSystem.GetText("ui.reward_panel.magic_only.title", "道具奖励");

        TMP_Text hint = UIManager.FindChildComponent<TMP_Text>(transform, "Hint");
        if (hint != null)
            hint.text = LocalizationSystem.GetText("ui.reward_panel.magic_only.hint", "选择一个道具后，点击场景中的道具槽覆盖。");

        CacheReferences();
        HideMagicChoices();
        HideArrowChoices();
        RefreshOptions();
        ScheduleMagicChoicePrewarm();
    }

    public void Hide()
    {
        StopMagicChoicePrewarm();
        owner?.SelectPendingRewardMagic(null);
        HideMagicChoices();
        HideArrowChoices();
        currentRewardOptions = null;
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
        RefreshOptions();
    }

    public void RefreshCurrentOptions()
    {
        RefreshOptions();
    }

    public void CompleteMagicRewardSelection()
    {
        magicClaimed = true;
        owner?.SelectPendingRewardMagic(null);
        HideMagicChoices();
        if (magicOnlyMode)
        {
            CompleteMagicOnlyReward();
            return;
        }
        RefreshOptions();
    }

    private void CompleteMagicOnlyReward()
    {
        Action completed = magicOnlyCompleted;
        magicOnlyMode = false;
        magicOnlyCompleted = null;
        currentRewardOptions = null;
        owner?.SelectPendingRewardMagic(null);
        HideMagicChoices();
        gameObject.SetActive(false);
        completed?.Invoke();
    }

    private void RefreshOptions()
    {
        EnsureOptionCount(GetRewardOptionCount());
        LayoutRewardOptions(GetRewardOptionCount());

        if (magicOnlyMode)
        {
            if (optionViews.Count > 0)
            {
                if (!magicClaimed)
                    optionViews[0].Bind(LocalizationSystem.GetText("ui.reward_panel.option.magic", "获得道具"), ShowMagicChoices);
                else
                    optionViews[0].Hide();
            }
            for (int i = 1; i < optionViews.Count; i++)
                optionViews[i].Hide();
        }
        else
        {
            if (optionViews.Count > 0)
            {
                if (!goldClaimed && !goldClaimInProgress)
                    optionViews[0].Bind(string.Format(LocalizationSystem.GetText("ui.reward_panel.option.gold", "金币x{0}"), currentGoldReward), ClaimGoldReward);
                else
                    optionViews[0].Hide();
            }
            if (optionViews.Count > 1)
            {
                bool itemAvailable = arrowRewardMode ? !arrowClaimed : !magicClaimed;
                if (itemAvailable && !goldClaimInProgress)
                {
                    if (arrowRewardMode)
                        optionViews[1].Bind(LocalizationSystem.GetText("ui.reward_panel.option.arrow", "获得箭头"), ShowArrowChoices);
                    else
                        optionViews[1].Bind(LocalizationSystem.GetText("ui.reward_panel.option.magic", "获得道具"), ShowMagicChoices);
                }
                else
                    optionViews[1].Hide();
            }
            if (optionViews.Count > 2)
            {
                if (eliteExtraRewardKind == RewardOptionKind.MagicModifier && !goldClaimInProgress)
                    optionViews[2].Bind(LocalizationSystem.GetText("ui.reward_panel.option.magic_modifier", "道具强化"), ClaimEliteMagicModifierReward);
                else if (eliteExtraRewardKind == RewardOptionKind.ArrowModifier && !goldClaimInProgress)
                    optionViews[2].Bind(LocalizationSystem.GetText("ui.reward_panel.option.arrow_modifier", "箭头附魔"), ClaimEliteArrowModifierReward);
                else
                    optionViews[2].Hide();
            }
            for (int i = 3; i < optionViews.Count; i++)
                optionViews[i].Hide();
        }

        if (endButton != null)
        {
            endButton.onClick.RemoveAllListeners();
            if (magicOnlyMode)
                endButton.onClick.AddListener(CompleteMagicOnlyReward);
            else
                endButton.onClick.AddListener(owner.FinishReward);
            endButton.interactable = !goldClaimInProgress;
            TMP_Text text = UIManager.FindChildComponent<TMP_Text>(endButton.transform, "Text");
            if (text != null)
                text.text = magicOnlyMode ? LocalizationSystem.GetText("ui.common.skip", "跳过") : LocalizationSystem.GetText("ui.common.leave", "离开");
        }
    }

    private void LayoutRewardOptions(int count)
    {
        RectTransform parent = FindOptionParent();
        if (parent == null || count <= 0)
            return;

        float optionWidth = count >= 3 ? 134f : 170f;
        float spacing = count >= 3 ? 146f : 190f;
        float startX = count > 1 ? -spacing * (count - 1) * 0.5f : 0f;
        for (int i = 0; i < optionViews.Count; i++)
        {
            RectTransform rect = optionViews[i] != null ? optionViews[i].transform as RectTransform : null;
            if (rect == null)
                continue;

            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(startX + spacing * i, 0f);
            rect.sizeDelta = new Vector2(optionWidth, 54f);
        }
    }

    private int GetRewardOptionCount()
    {
        if (magicOnlyMode)
            return 1;
        return eliteExtraRewardKind != RewardOptionKind.None ? 3 : 2;
    }

    private void ClaimEliteMagicModifierReward()
    {
        if (owner == null || goldClaimInProgress)
            return;

        owner.ClaimEliteMagicModifierReward(() =>
        {
            eliteExtraRewardKind = RewardOptionKind.None;
            RefreshOptions();
        });
    }

    private void ClaimEliteArrowModifierReward()
    {
        if (owner == null || goldClaimInProgress)
            return;

        owner.ClaimEliteArrowModifierReward(() =>
        {
            eliteExtraRewardKind = RewardOptionKind.None;
            RefreshOptions();
        });
    }

    private void ClaimGoldReward()
    {
        if (goldClaimed || goldClaimInProgress)
            return;
        StartCoroutine(ClaimGoldRewardRoutine());
    }

    private IEnumerator ClaimGoldRewardRoutine()
    {
        goldClaimInProgress = true;
        if (endButton != null)
            endButton.interactable = false;
        RectTransform sourceRect = optionViews.Count > 0 ? optionViews[0].transform as RectTransform : transform as RectTransform;
        yield return owner.GainGoldAnimated(currentGoldReward, sourceRect, false);
        goldClaimed = true;
        goldClaimInProgress = false;
        RefreshOptions();
    }

    private int RollBattleGoldReward()
    {
        EconomyConfigData economy = GameDataDatabase.GetDefaultEconomyConfig();
        if (economy == null)
            return battleGoldReward;

        RunManager runManager = owner != null ? owner.RunManager : null;
        ChapterData chapter = runManager != null ? runManager.ActiveChapter : null;
        LevelData level = runManager != null ? runManager.CurrentLevel : null;
        bool bossFlag = runManager != null && runManager.CurrentBattle != null && runManager.CurrentBattle.CurrentLevelIsBoss;

        int reward = ResolveBattleGold(economy, chapter, level, bossFlag);
        return DifficultyUpgradeSystem.ModifyGoldGain(reward);
    }

    private static int ResolveBattleGold(EconomyConfigData economy, ChapterData chapter, LevelData level, bool bossFlag)
    {
        if (level == null)
            return economy.battleGoldMin;

        bool bossLevel = bossFlag || ContainsId(chapter != null ? chapter.BossPool : null, level.numericId);
        bool eliteLevel = level.levelType == LevelType.Elite || ContainsId(chapter != null ? chapter.ElitePool : null, level.numericId);
        if (bossLevel || eliteLevel)
            return economy.eliteBattleGoldMin;

        if (ContainsId(chapter != null ? chapter.BeginPool : null, level.numericId))
            return economy.weakBattleGold > 0 ? economy.weakBattleGold : economy.battleGoldMin;
        if (ContainsId(chapter != null ? chapter.MidPool : null, level.numericId))
            return economy.battleGoldMin;
        if (ContainsId(chapter != null ? chapter.NormalPool : null, level.numericId))
            return economy.strongBattleGold > 0 ? economy.strongBattleGold : economy.battleGoldMin;
        return economy.battleGoldMin;
    }

    private static bool ContainsId(int[] pool, int numericId)
    {
        if (pool == null)
            return false;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] == numericId)
                return true;
        }
        return false;
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

        if (currentRewardOptions == null)
            currentRewardOptions = new RewardOptionsModel(currentGoldReward, owner.GetRewardMagicChoices(3));

        List<MagicData> choices = currentRewardOptions.MagicChoices;
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
        if (currentRewardOptions == null)
            currentRewardOptions = new RewardOptionsModel(currentGoldReward, owner.GetRewardMagicChoices(3));

        List<MagicData> choices = currentRewardOptions.MagicChoices;
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

    private void ShowArrowChoices()
    {
        if (arrowClaimed || goldClaimInProgress)
            return;

        HideMagicChoices();
        EnsureArrowChoicePanel();
        arrowChoicePanel.gameObject.SetActive(true);
        arrowChoicePanel.SetAsLastSibling();
        RebuildArrowChoiceViews();
    }

    private void HideArrowChoices()
    {
        ClearArrowChoiceHover(false);
        if (arrowChoicePanel != null)
            arrowChoicePanel.gameObject.SetActive(false);
    }

    private void EnsureArrowChoicePanel()
    {
        if (arrowChoicePanel != null)
        {
            CacheArrowChoicePanelReferences();
            return;
        }

        RectTransform existingPanel = transform.parent != null ? transform.parent.Find("RewardArrowChoicePanel") as RectTransform : null;
        if (existingPanel != null)
        {
            arrowChoicePanel = existingPanel;
            CacheArrowChoicePanelReferences();
            return;
        }

        RectTransform sourceRect = (RectTransform)transform;
        Image panelImage = new GameObject("RewardArrowChoicePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        arrowChoicePanel = panelImage.rectTransform;
        arrowChoicePanel.SetParent(transform.parent, false);
        arrowChoicePanel.anchorMin = sourceRect.anchorMin;
        arrowChoicePanel.anchorMax = sourceRect.anchorMax;
        arrowChoicePanel.pivot = sourceRect.pivot;
        arrowChoicePanel.anchoredPosition = sourceRect.anchoredPosition;
        arrowChoicePanel.sizeDelta = sourceRect.sizeDelta;
        arrowChoicePanel.localScale = Vector3.one;
        panelImage.color = new Color(0.02f, 0.02f, 0.04f, 1f);
        panelImage.raycastTarget = true;

        TMP_Text title = CreatePanelText(arrowChoicePanel, "Title", LocalizationSystem.GetText("ui.reward_panel.arrow_choice.title", "选择一张箭头"), 26, FontStyles.Bold, new Vector2(0f, 150f), new Vector2(420f, 40f));
        title.color = new Color(1f, 0.9f, 0.55f, 1f);
        TMP_Text hint = CreatePanelText(arrowChoicePanel, "Hint", LocalizationSystem.GetText("ui.reward_panel.arrow_choice.hint", "获得后会加入你的箭头牌组；小概率带附魔。"), 16, FontStyles.Normal, new Vector2(0f, 104f), new Vector2(620f, 30f));
        hint.color = new Color(0.82f, 0.84f, 0.9f, 1f);

        arrowChoiceBackButton = CreatePanelButton(arrowChoicePanel, "BackButton", LocalizationSystem.GetText("ui.common.back", "返回"), new Vector2(-360f, 150f), new Vector2(110f, 42f));
        arrowChoiceBackButton.onClick.RemoveAllListeners();
        arrowChoiceBackButton.onClick.AddListener(HideArrowChoices);

        arrowChoiceContent = new GameObject("ArrowChoices", typeof(RectTransform)).GetComponent<RectTransform>();
        arrowChoiceContent.SetParent(arrowChoicePanel, false);
        arrowChoiceContent.anchorMin = new Vector2(0.5f, 0.5f);
        arrowChoiceContent.anchorMax = new Vector2(0.5f, 0.5f);
        arrowChoiceContent.pivot = new Vector2(0.5f, 0.5f);
        arrowChoiceContent.anchoredPosition = new Vector2(0f, -24f);
        arrowChoiceContent.sizeDelta = new Vector2(760f, 160f);
        arrowChoicePanel.gameObject.SetActive(false);
    }

    private void CacheArrowChoicePanelReferences()
    {
        if (arrowChoicePanel == null)
            return;

        TMP_Text title = UIManager.FindChildComponent<TMP_Text>(arrowChoicePanel, "Title");
        if (title != null)
            title.text = LocalizationSystem.GetText("ui.reward_panel.arrow_choice.title", "选择一张箭头");
        TMP_Text hint = UIManager.FindChildComponent<TMP_Text>(arrowChoicePanel, "Hint");
        if (hint != null)
            hint.text = LocalizationSystem.GetText("ui.reward_panel.arrow_choice.hint", "获得后会加入你的箭头牌组；小概率带附魔。");

        arrowChoiceBackButton = UIManager.FindChildComponent<Button>(arrowChoicePanel, "BackButton");
        if (arrowChoiceBackButton == null)
        {
            Transform styled = arrowChoicePanel.Find("PopupDragonWindowBackground/BackButton");
            arrowChoiceBackButton = styled != null ? styled.GetComponent<Button>() : null;
        }
        if (arrowChoiceBackButton != null)
        {
            arrowChoiceBackButton.onClick.RemoveAllListeners();
            arrowChoiceBackButton.onClick.AddListener(HideArrowChoices);
        }

        arrowChoiceContent = UIManager.FindChildRect(arrowChoicePanel, "ArrowChoices");
        if (arrowChoiceContent == null)
        {
            arrowChoiceContent = new GameObject("ArrowChoices", typeof(RectTransform)).GetComponent<RectTransform>();
            arrowChoiceContent.SetParent(arrowChoicePanel, false);
            arrowChoiceContent.anchorMin = new Vector2(0.5f, 0.5f);
            arrowChoiceContent.anchorMax = new Vector2(0.5f, 0.5f);
            arrowChoiceContent.pivot = new Vector2(0.5f, 0.5f);
            arrowChoiceContent.anchoredPosition = new Vector2(0f, -24f);
            arrowChoiceContent.sizeDelta = new Vector2(760f, 160f);
        }
    }

    private void RebuildArrowChoiceViews()
    {
        if (arrowChoiceContent == null)
            return;

        ClearArrowChoiceHover(false);
        for (int i = arrowChoiceContent.childCount - 1; i >= 0; i--)
        {
            Transform child = arrowChoiceContent.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }

        int visibleChoiceCount = Mathf.Min(currentArrowOptions.Count, 3);
        float spacing = 150f;
        float startX = visibleChoiceCount > 1 ? -spacing * (visibleChoiceCount - 1) * 0.5f : 0f;
        for (int i = 0; i < visibleChoiceCount; i++)
            CreateArrowChoiceView(i, startX + spacing * i);
    }

    private void CreateArrowChoiceView(int index, float centerX)
    {
        RewardArrowOption option = index >= 0 && index < currentArrowOptions.Count ? currentArrowOptions[index] : null;
        if (option == null || option.material == MaterialEnum.None)
            return;

        Image image = new GameObject("RewardArrow" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Image>();
        image.transform.SetParent(arrowChoiceContent, false);
        image.color = Color.clear;
        image.raycastTarget = true;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(centerX, 0f);
        rect.sizeDelta = new Vector2(100f, 130f);

        MaterialModel preview;
        RectTransform previewRect = CreateArrowPreview(rect, option, index, out preview);
        SpringLineHighlightUI hoverFrame = CreateArrowChoiceHoverFrame(rect, previewRect);
        RewardArrowChoiceHoverRelay hoverRelay = image.gameObject.AddComponent<RewardArrowChoiceHoverRelay>();
        hoverRelay.Initialize(this, rect, preview, hoverFrame);

        Button button = image.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        RectTransform source = previewRect != null ? previewRect : rect;
        button.onClick.AddListener(() => OnArrowOptionClicked(option, source));
    }

    private RectTransform CreateArrowPreview(RectTransform parent, RewardArrowOption option, int index, out MaterialModel preview)
    {
        preview = new MaterialModel("reward_arrow_" + index + "_" + option.material, option.material);
        if (option.HasModifier)
        {
            MaterialModifierModel modifier = MaterialModifierFactory.Create(option.modifierData);
            if (modifier != null)
                preview.AddModifier(modifier);
        }

        RectTransform prefab = GetMaterialCardPrefab();
        if (prefab == null)
        {
            TMP_Text fallback = CreatePanelText(parent, "Text", GetArrowOptionLabel(option), 20, FontStyles.Bold, new Vector2(0f, -20f), new Vector2(180f, 40f));
            fallback.raycastTarget = false;
            return null;
        }

        RectTransform previewRect = Instantiate(prefab, parent);
        previewRect.name = "ArrowPreview" + index;
        previewRect.gameObject.SetActive(true);
        previewRect.anchorMin = new Vector2(0.5f, 0.5f);
        previewRect.anchorMax = new Vector2(0.5f, 0.5f);
        previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.anchoredPosition = new Vector2(0f, 0f);
        previewRect.sizeDelta = new Vector2(82f, 118f);
        previewRect.localScale = Vector3.one;

        MaterialCardView cardView = previewRect.GetComponent<MaterialCardView>();
        if (cardView != null)
        {
            cardView.Bind(preview);
            DisableChildRaycasts(previewRect);
        }
        return previewRect;
    }

    private SpringLineHighlightUI CreateArrowChoiceHoverFrame(RectTransform parent, RectTransform previewRect)
    {
        GameObject frameObject = new GameObject("HoverFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(SpringLineHighlightUI));
        frameObject.transform.SetParent(parent, false);
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.anchoredPosition = previewRect != null ? previewRect.anchoredPosition : new Vector2(0f, -20f);
        frameRect.sizeDelta = previewRect != null ? previewRect.sizeDelta : new Vector2(180f, 80f);

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

    internal void SetArrowChoiceHover(RectTransform choiceRect, MaterialModel preview, SpringLineHighlightUI hoverFrame, bool hovering)
    {
        if (choiceRect == null)
            return;

        if (!hovering)
        {
            if (hoveredArrowChoice == choiceRect)
                ClearArrowChoiceHover(true);
            return;
        }

        if (hoveredArrowChoice == choiceRect)
            return;

        ClearArrowChoiceHover(true);
        hoveredArrowChoice = choiceRect;
        hoveredArrowHighlight = hoverFrame;
        if (hoverFrame != null)
            hoverFrame.gameObject.SetActive(true);

        choiceRect.DOKill(false);
        choiceRect.DOScale(Vector3.one * 1.08f, 0.16f).SetEase(Ease.OutBack);
        choiceRect.DOLocalRotate(new Vector3(0f, 0f, 2f), 0.16f).SetEase(Ease.OutBack);
        if (owner != null && preview != null)
        {
            UIManager uiManager = owner.GetUIManager();
            if (uiManager != null)
                uiManager.ShowUnifiedDetailPopup(choiceRect, UnifiedDetailContentBuilder.Build(preview));
        }
    }

    private void ClearArrowChoiceHover(bool animate)
    {
        RectTransform choiceRect = hoveredArrowChoice;
        SpringLineHighlightUI hoverFrame = hoveredArrowHighlight;
        hoveredArrowChoice = null;
        hoveredArrowHighlight = null;
        if (hoverFrame != null)
            hoverFrame.gameObject.SetActive(false);
        if (choiceRect == null)
            return;

        choiceRect.DOKill(false);
        if (animate)
        {
            choiceRect.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutQuad);
            choiceRect.DOLocalRotate(Vector3.zero, 0.12f).SetEase(Ease.OutQuad);
        }
        else
        {
            choiceRect.localScale = Vector3.one;
            choiceRect.localEulerAngles = Vector3.zero;
        }

        if (owner != null)
        {
            UIManager uiManager = owner.GetUIManager();
            if (uiManager != null)
                uiManager.HideUnifiedDetailPopup(choiceRect);
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

    private void OnArrowOptionClicked(RewardArrowOption option, RectTransform sourceRect)
    {
        if (option == null || arrowClaimed || goldClaimInProgress || owner == null)
            return;

        StartCoroutine(ClaimArrowRewardRoutine(option, sourceRect));
    }

    private IEnumerator ClaimArrowRewardRoutine(RewardArrowOption option, RectTransform sourceRect)
    {
        arrowClaimed = true;
        HideArrowChoices();
        RefreshOptions();
        yield return owner.GainRewardArrow(option, sourceRect);
        RefreshOptions();
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

    private Button FindMagicChoiceBackButton()
    {
        if (magicChoicePanel == null)
            return null;

        Transform direct = magicChoicePanel.Find("BackButton");
        Button button = direct != null ? direct.GetComponent<Button>() : null;
        if (button != null)
            return button;

        Transform styled = magicChoicePanel.Find("PopupDragonWindowBackground/BackButton");
        return styled != null ? styled.GetComponent<Button>() : null;
    }

    private void BindMagicChoiceBackButton()
    {
        if (magicChoiceBackButton == null)
            return;

        magicChoiceBackButton.onClick.RemoveAllListeners();
        magicChoiceBackButton.onClick.AddListener(ReturnFromMagicChoices);
        TMP_Text text = UIManager.FindChildComponent<TMP_Text>(magicChoiceBackButton.transform, "Text");
        if (text != null)
            text.text = LocalizationSystem.GetText("ui.common.back", "返回");
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

    private void EnsureOptionCount(int count)
    {
        CacheOptionViews();
        RectTransform parent = FindOptionParent();
        while (optionViews.Count < count)
            optionViews.Add(CreateOptionView(parent, optionViews.Count));
    }

    private RectTransform FindOptionParent()
    {
        Transform optionRoot = transform.Find("OptionArea");
        return optionRoot as RectTransform ?? (RectTransform)transform;
    }

    private RewardOptionView CreateOptionView(RectTransform parent, int index)
    {
        Image image = new GameObject("RewardOption" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(RewardOptionView), typeof(JuicyMotion)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = new Color(0.08f, 0.08f, 0.12f, 1f);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(160f, 54f);

        TMP_Text text = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        text.transform.SetParent(rect, false);
        text.font = UIManager.GetDefaultTMPFont();
        text.fontSize = 18;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return image.GetComponent<RewardOptionView>();
    }

    private void CacheReferences()
    {
        rewardMagicViews.Clear();
        MagicItemView[] views = GetComponentsInChildren<MagicItemView>(true);
        for (int i = 0; i < views.Length; i++)
            rewardMagicViews.Add(views[i]);

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
        CacheOptionViews();
    }

    private static int CompareMagicRewardViewNames(MagicItemView left, MagicItemView right)
    {
        return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
    }

    private void CacheOptionViews()
    {
        optionViews.Clear();
        Transform optionRoot = transform.Find("OptionArea");
        if (optionRoot != null)
        {
            Button[] buttons = optionRoot.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                RewardOptionView view = buttons[i].GetComponent<RewardOptionView>();
                if (view == null)
                    view = buttons[i].gameObject.AddComponent<RewardOptionView>();
                optionViews.Add(view);
            }
        }
        else
        {
            RewardOptionView[] views = GetComponentsInChildren<RewardOptionView>(true);
            for (int i = 0; i < views.Length; i++)
                optionViews.Add(views[i]);
        }
        optionViews.Sort(CompareOptionViewNames);
    }

    private static int CompareOptionViewNames(RewardOptionView left, RewardOptionView right)
    {
        return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
    }
}
