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
        currentRewardOptions = new RewardOptionsModel(RollBattleGoldReward(), owner.GetRewardMagicChoices(3));
        eliteExtraRewardKind = owner.RollEliteExtraRewardKind();
        currentGoldReward = currentRewardOptions.GoldReward;
        selectedMagicView = null;
        hoveredMagicView = null;
        owner.SelectPendingRewardMagic(null);
        gameObject.SetActive(true);
        TMP_Text title = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (title != null)
            title.text = "战斗奖励";

        TMP_Text hint = UIManager.FindChildComponent<TMP_Text>(transform, "Hint");
        if (hint != null)
            hint.text = "选择奖励；道具奖励选中后，点击场景中的道具槽覆盖。";

        CacheReferences();
        HideMagicChoices();
        RefreshOptions();
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
        currentRewardOptions = new RewardOptionsModel(0, owner.GetRewardMagicChoices(3));
        eliteExtraRewardKind = RewardOptionKind.None;
        currentGoldReward = currentRewardOptions.GoldReward;
        selectedMagicView = null;
        hoveredMagicView = null;
        owner.SelectPendingRewardMagic(null);
        gameObject.SetActive(true);
        TMP_Text title = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (title != null)
            title.text = "道具奖励";

        TMP_Text hint = UIManager.FindChildComponent<TMP_Text>(transform, "Hint");
        if (hint != null)
            hint.text = "选择一个道具后，点击场景中的道具槽覆盖。";

        CacheReferences();
        HideMagicChoices();
        RefreshOptions();
        ScheduleMagicChoicePrewarm();
    }

    public void Hide()
    {
        StopMagicChoicePrewarm();
        owner?.SelectPendingRewardMagic(null);
        HideMagicChoices();
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
                    optionViews[0].Bind("获得道具", ShowMagicChoices);
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
                    optionViews[0].Bind("金币x" + currentGoldReward, ClaimGoldReward);
                else
                    optionViews[0].Hide();
            }
            if (optionViews.Count > 1)
            {
                if (!magicClaimed && !goldClaimInProgress)
                    optionViews[1].Bind("获得道具", ShowMagicChoices);
                else
                    optionViews[1].Hide();
            }
            if (optionViews.Count > 2)
            {
                if (eliteExtraRewardKind == RewardOptionKind.MagicModifier && !goldClaimInProgress)
                    optionViews[2].Bind("道具强化", ClaimEliteMagicModifierReward);
                else if (eliteExtraRewardKind == RewardOptionKind.ArrowModifier && !goldClaimInProgress)
                    optionViews[2].Bind("箭头附魔", ClaimEliteArrowModifierReward);
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
                text.text = magicOnlyMode ? "跳过" : "离开";
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
        yield return owner.GainGoldAnimated(currentGoldReward, sourceRect);
        goldClaimed = true;
        goldClaimInProgress = false;
        RefreshOptions();
    }

    private int RollBattleGoldReward()
    {
        EconomyConfigData economy = GameDataDatabase.GetDefaultEconomyConfig();
        if (economy == null)
            return battleGoldReward;

        bool eliteReward = owner != null && owner.RunManager != null && owner.RunManager.CurrentLevel != null && owner.RunManager.CurrentLevel.levelType == LevelType.Elite;
        int min = eliteReward ? Mathf.Min(economy.eliteBattleGoldMin, economy.eliteBattleGoldMax) : Mathf.Min(economy.battleGoldMin, economy.battleGoldMax);
        int max = eliteReward ? Mathf.Max(economy.eliteBattleGoldMin, economy.eliteBattleGoldMax) : Mathf.Max(economy.battleGoldMin, economy.battleGoldMax);
        return owner != null && owner.RunManager != null ? owner.RunManager.NextRandomInt(min, max + 1) : UnityEngine.Random.Range(min, max + 1);
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

        TMP_Text title = CreatePanelText(magicChoicePanel, "Title", "选择一个道具", 26, FontStyles.Bold, new Vector2(0f, 112f), new Vector2(360f, 40f));
        title.color = new Color(1f, 0.9f, 0.55f, 1f);
        TMP_Text hint = CreatePanelText(magicChoicePanel, "Hint", "选择后点击下方/场景中的道具槽覆盖；可重新选择。", 16, FontStyles.Normal, new Vector2(0f, 72f), new Vector2(620f, 30f));
        hint.color = new Color(0.82f, 0.84f, 0.9f, 1f);

        magicChoiceBackButton = CreatePanelButton(magicChoicePanel, "BackButton", "返回", new Vector2(-360f, 112f), new Vector2(110f, 42f));
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
            title.text = "选择一个道具";
        TMP_Text hint = UIManager.FindChildComponent<TMP_Text>(magicChoicePanel, "Hint");
        if (hint != null)
            hint.text = "选择后点击下方/场景中的道具槽覆盖；可重新选择。";

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
