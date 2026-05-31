using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public enum RewardOptionKind
{
    Gold,
    Magic
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

    private readonly List<MagicItemView> rewardMagicViews = new List<MagicItemView>();
    private readonly List<RewardOptionView> optionViews = new List<RewardOptionView>();
    private HandSystemUI owner;
    private Button endButton;
    private RectTransform magicChoicePanel;
    private RectTransform magicChoiceContent;
    private Button magicChoiceBackButton;
    private bool goldClaimed;
    private bool magicClaimed;
    private MagicItemView selectedMagicView;
    private MagicItemView hoveredMagicView;
    private Tween selectedMagicTween;
    private int currentGoldReward;

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

        goldClaimed = false;
        magicClaimed = false;
        currentGoldReward = RollBattleGoldReward();
        selectedMagicView = null;
        hoveredMagicView = null;
        owner.SelectPendingRewardMagic(null);
        gameObject.SetActive(true);
        TMP_Text title = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (title != null)
            title.text = "战斗奖励";

        TMP_Text hint = UIManager.FindChildComponent<TMP_Text>(transform, "Hint");
        if (hint != null)
            hint.text = "选择奖励；法术奖励选中后，点击场景中的法术槽覆盖。";

        CacheReferences();
        HideMagicChoices();
        RefreshOptions();
        owner.GetUIManager().TutorialManager?.OnRewardPanelShown();
    }

    public void Hide()
    {
        owner?.SelectPendingRewardMagic(null);
        HideMagicChoices();
        gameObject.SetActive(false);
    }

    public void CompleteMagicRewardSelection()
    {
        magicClaimed = true;
        owner?.SelectPendingRewardMagic(null);
        HideMagicChoices();
        RefreshOptions();
    }

    private void RefreshOptions()
    {
        EnsureOptionCount(2);
        int index = 0;
        if (!goldClaimed)
            optionViews[index++].Bind("获得金币 +" + currentGoldReward, ClaimGoldReward);
        if (!magicClaimed)
            optionViews[index++].Bind("获得法术", ShowMagicChoices);
        for (int i = index; i < optionViews.Count; i++)
            optionViews[i].Hide();

        if (endButton != null)
        {
            endButton.onClick.RemoveAllListeners();
            endButton.onClick.AddListener(owner.FinishReward);
            TMP_Text text = UIManager.FindChildComponent<TMP_Text>(endButton.transform, "Text");
            if (text != null)
                text.text = "离开";
        }
    }

    private void ClaimGoldReward()
    {
        if (goldClaimed)
            return;
        owner.PlayerState.AddGold(currentGoldReward);
        goldClaimed = true;
        RefreshOptions();
    }

    private int RollBattleGoldReward()
    {
        EconomyConfigData economy = GameDataDatabase.GetDefaultEconomyConfig();
        if (economy == null)
            return battleGoldReward;

        int min = Mathf.Min(economy.battleGoldMin, economy.battleGoldMax);
        int max = Mathf.Max(economy.battleGoldMin, economy.battleGoldMax);
        return UnityEngine.Random.Range(min, max + 1);
    }

    private void ShowMagicChoices()
    {
        if (magicClaimed)
            return;

        owner.GetUIManager().TutorialManager?.OnMagicRewardChoicesShown();
        EnsureMagicChoicePanel();
        magicChoicePanel.gameObject.SetActive(true);
        magicChoicePanel.SetAsLastSibling();

        List<MagicData> choices = owner.GetRewardMagicChoices();
        for (int i = 0; i < rewardMagicViews.Count; i++)
        {
            MagicItemView view = rewardMagicViews[i];
            if (view == null)
                continue;

            bool visible = i < choices.Count;
            view.gameObject.SetActive(visible);
            if (!visible)
                continue;

            RectTransform rect = (RectTransform)view.transform;
            rect.SetParent(magicChoiceContent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-230f + i * 230f, 0f);
            rect.sizeDelta = new Vector2(196f, 92f);
            rect.localScale = GetRewardMagicTargetScale(view);
            UIManager.RemoveJuicyMotion(view.transform);

            MagicData data = choices[i];
            view.Bind(MagicFactory.Create(data));
            Button button = view.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectMagicReward(data, view));
            }
            ConfigureMagicChoiceHover(view);
        }
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

    private void ConfigureMagicChoiceHover(MagicItemView view)
    {
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
        selectedMagicView = null;
        hoveredMagicView = null;
        owner.SelectPendingRewardMagic(null);
        HideMagicChoices();
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
                rect.sizeDelta = new Vector2(196f, 92f);
                rect.localScale = Vector3.one;
                rewardMagicViews[i].gameObject.SetActive(false);
            }
        }
    }

    private void EnsureMagicChoicePanel()
    {
        if (magicChoicePanel != null)
            return;

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
        panelImage.color = new Color(0.02f, 0.02f, 0.04f, 0.98f);
        panelImage.raycastTarget = true;

        TMP_Text title = CreatePanelText(magicChoicePanel, "Title", "选择一个法术", 26, FontStyles.Bold, new Vector2(0f, 112f), new Vector2(360f, 40f));
        title.color = new Color(1f, 0.9f, 0.55f, 1f);
        TMP_Text hint = CreatePanelText(magicChoicePanel, "Hint", "选择后点击下方/场景中的法术槽覆盖；可重新选择。", 16, FontStyles.Normal, new Vector2(0f, 72f), new Vector2(620f, 30f));
        hint.color = new Color(0.82f, 0.84f, 0.9f, 1f);

        magicChoiceBackButton = CreatePanelButton(magicChoicePanel, "BackButton", "返回", new Vector2(-360f, 112f), new Vector2(110f, 42f));
        magicChoiceBackButton.onClick.AddListener(ReturnFromMagicChoices);

        magicChoiceContent = new GameObject("MagicChoices", typeof(RectTransform)).GetComponent<RectTransform>();
        magicChoiceContent.SetParent(magicChoicePanel, false);
        magicChoiceContent.anchorMin = new Vector2(0.5f, 0.5f);
        magicChoiceContent.anchorMax = new Vector2(0.5f, 0.5f);
        magicChoiceContent.pivot = new Vector2(0.5f, 0.5f);
        magicChoiceContent.anchoredPosition = new Vector2(0f, -24f);
        magicChoiceContent.sizeDelta = new Vector2(760f, 120f);

        for (int i = 0; i < rewardMagicViews.Count; i++)
        {
            if (rewardMagicViews[i] != null)
                rewardMagicViews[i].transform.SetParent(magicChoiceContent, false);
        }
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
        image.color = new Color(0.09f, 0.09f, 0.14f, 0.96f);
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
        image.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-180f + index * 180f, -70f);
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
        RewardOptionView[] views = GetComponentsInChildren<RewardOptionView>(true);
        for (int i = 0; i < views.Length; i++)
            optionViews.Add(views[i]);
    }
}
