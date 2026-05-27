using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum RewardOptionKind
{
    Gold,
    Magic
}

public class RewardOptionView : MonoBehaviour
{
    [SerializeField] private Text labelText;
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
            labelText = UIManager.FindChildComponent<Text>(transform, "Text");
        if (labelText == null)
            labelText = GetComponentInChildren<Text>(true);
    }
}

public class RewardPanelUI : MonoBehaviour
{
    [SerializeField] private int battleGoldReward = 20;

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
    private Tween selectedMagicTween;

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
        selectedMagicView = null;
        owner.SelectPendingRewardMagic(null);
        gameObject.SetActive(true);
        Text title = UIManager.FindChildComponent<Text>(transform, "Title");
        if (title != null)
            title.text = "战斗奖励";

        Text hint = UIManager.FindChildComponent<Text>(transform, "Hint");
        if (hint != null)
            hint.text = "选择奖励；法术奖励选中后，点击场景中的法术槽覆盖。";

        CacheReferences();
        HideMagicChoices();
        RefreshOptions();
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
            optionViews[index++].Bind("获得金币 +" + battleGoldReward, ClaimGoldReward);
        if (!magicClaimed)
            optionViews[index++].Bind("获得法术", ShowMagicChoices);
        for (int i = index; i < optionViews.Count; i++)
            optionViews[i].Hide();

        if (endButton != null)
        {
            endButton.onClick.RemoveAllListeners();
            endButton.onClick.AddListener(owner.FinishReward);
            Text text = UIManager.FindChildComponent<Text>(endButton.transform, "Text");
            if (text != null)
                text.text = "离开";
        }
    }

    private void ClaimGoldReward()
    {
        if (goldClaimed)
            return;
        owner.PlayerState.AddGold(battleGoldReward);
        goldClaimed = true;
        RefreshOptions();
    }

    private void ShowMagicChoices()
    {
        if (magicClaimed)
            return;

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
            rect.localScale = view == selectedMagicView ? Vector3.one * 1.18f : Vector3.one;

            MagicData data = choices[i];
            view.Bind(MagicFactory.Create(data));
            Button button = view.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectMagicReward(data, view));
            }
            UIManager.AddJuicyMotion(view.transform);
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
            Vector3 targetScale = rewardView == selectedMagicView ? Vector3.one * 1.24f : Vector3.one;
            Tween tween = rewardTransform.DOScale(targetScale, 0.16f).SetEase(Ease.OutBack).SetTarget(this);
            if (rewardView == selectedMagicView)
                selectedMagicTween = tween;
        }
    }

    private void ReturnFromMagicChoices()
    {
        selectedMagicView = null;
        owner.SelectPendingRewardMagic(null);
        HideMagicChoices();
    }

    private void HideMagicChoices()
    {
        selectedMagicTween?.Kill(false);
        selectedMagicTween = null;
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

        Text title = CreatePanelText(magicChoicePanel, "Title", "选择一个法术", 26, FontStyle.Bold, new Vector2(0f, 112f), new Vector2(360f, 40f));
        title.color = new Color(1f, 0.9f, 0.55f, 1f);
        Text hint = CreatePanelText(magicChoicePanel, "Hint", "选择后点击下方/场景中的法术槽覆盖；可重新选择。", 16, FontStyle.Normal, new Vector2(0f, 72f), new Vector2(620f, 30f));
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

    private Text CreatePanelText(RectTransform parent, string name, string text, int fontSize, FontStyle fontStyle, Vector2 anchoredPosition, Vector2 size)
    {
        Text label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = TextAnchor.MiddleCenter;
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

        Text label = CreatePanelText(rect, "Text", text, 18, FontStyle.Bold, Vector2.zero, size);
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

        Text text = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(rect, false);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
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
