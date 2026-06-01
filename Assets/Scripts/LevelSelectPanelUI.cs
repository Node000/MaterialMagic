using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelSelectPanelUI : MonoBehaviour
{
    [Header("动画参数")]
    [SerializeField] private float panelShowDuration = 0.22f;
    [SerializeField] private Ease panelShowEase = Ease.OutQuad;
    [SerializeField] private Vector2 panelMoveOffset = new Vector2(36f, -28f);
    [SerializeField] private Ease hideEase = Ease.InBack;
    [SerializeField] private float optionShowDuration = 0.28f;
    [SerializeField] private float optionDelayStep = 0.08f;
    [SerializeField] private Ease optionShowEase = Ease.OutBack;
    [SerializeField] private Vector2 leftOptionPosition = new Vector2(-150f, -22f);
    [SerializeField] private Vector2 rightOptionPosition = new Vector2(150f, -22f);
    [SerializeField] private RectTransform revealMask;
    [SerializeField] private RectTransform contentRoot;

    private readonly List<Button> optionButtons = new List<Button>();
    private HandSystemUI owner;
    private RectTransform rectTransform;
    private Vector2 fullPanelSize;
    private Vector2 shownAnchoredPosition;
    private bool hasShownAnchoredPosition;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        CacheReferences();
        SetRevealProgress(1f);
    }

    public void Show(IReadOnlyList<RunMapNodeModel> nodes, int currentNodeIndex)
    {
        if (nodes == null || currentNodeIndex < 0 || currentNodeIndex >= nodes.Count)
            return;

        RunMapNodeModel nodeModel = nodes[currentNodeIndex];
        CacheReferences();
        DOTween.Kill(this, false);
        ResetOptionScales();
        gameObject.SetActive(true);
        PreparePanelShow();
        TMP_Text title = UIManager.FindChildComponent<TMP_Text>(GetOptionRoot(), "Title");
        if (title != null)
            title.text = "选择下一关";

        float optionBaseDelay = panelShowDuration;
        if (nodeModel.fixedSingleChoice)
        {
            BindOption(0, nodeModel.leftLevel, optionBaseDelay);
            HideOption(1);
            if (optionButtons.Count > 0 && optionButtons[0] != null)
                optionButtons[0].GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            PlayPanelShow();
            return;
        }

        BindOption(0, nodeModel.leftLevel, optionBaseDelay);
        BindOption(1, nodeModel.rightLevel, optionBaseDelay + optionDelayStep);
        PlayPanelShow();
    }

    public void Hide()
    {
        CacheReferences();
        DOTween.Kill(this, false);
        ResetOptionScales();
        SetOptionButtonsInteractable(false);
        rectTransform.anchoredPosition = shownAnchoredPosition;
        ConfigureRevealLayout(true);
        SetRevealProgress(1f);
        gameObject.SetActive(false);
    }

    public void HideAnimated(Action onComplete = null)
    {
        CacheReferences();
        DOTween.Kill(this, false);
        ResetOptionScales();
        SetOptionButtonsInteractable(false);
        if (!gameObject.activeSelf)
        {
            rectTransform.anchoredPosition = shownAnchoredPosition;
            onComplete?.Invoke();
            return;
        }

        rectTransform.anchoredPosition = shownAnchoredPosition;
        ConfigureRevealLayout(false);
        SetRevealProgress(1f);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(DOVirtual.Float(1f, 0f, panelShowDuration, SetRevealProgress).SetEase(hideEase));
        sequence.Join(rectTransform.DOAnchorPos(shownAnchoredPosition + panelMoveOffset, panelShowDuration).SetEase(hideEase));
        sequence.OnComplete(() =>
        {
            ResetOptionScales();
            gameObject.SetActive(false);
            rectTransform.anchoredPosition = shownAnchoredPosition;
            ConfigureRevealLayout(true);
            SetRevealProgress(1f);
            onComplete?.Invoke();
        });
    }

    private void HideOption(int index)
    {
        EnsureOptionCapacity(index);
        Button button = optionButtons[index];
        if (button == null)
        {
            Transform optionTransform = GetOptionRoot().Find(index == 0 ? "LeftOption" : "RightOption");
            if (optionTransform != null)
                button = optionTransform.GetComponent<Button>();
            optionButtons[index] = button;
        }

        if (button != null)
        {
            button.GetComponent<RectTransform>().DOKill(false);
            button.transform.localScale = Vector3.one;
            button.gameObject.SetActive(false);
        }
    }

    private void BindOption(int index, LevelData level, float delay)
    {
        EnsureOptionCapacity(index);
        Button button = optionButtons[index];
        if (button == null)
        {
            Transform optionTransform = GetOptionRoot().Find(index == 0 ? "LeftOption" : "RightOption");
            if (optionTransform == null)
                return;
            button = optionTransform.GetComponent<Button>();
            optionButtons[index] = button;
        }

        if (button == null)
            return;

        button.interactable = true;
        button.gameObject.SetActive(true);
        TMP_Text text = UIManager.FindChildComponent<TMP_Text>(button.transform, "Text");
        if (text != null)
            text.text = UIManager.GetLevelTypeName(level.levelType);

        Image icon = UIManager.FindChildComponent<Image>(button.transform, "TypeIcon");
        if (icon != null)
        {
            icon.sprite = UIManager.LoadLevelTypeSprite(level.levelType);
            icon.color = icon.sprite != null ? Color.white : new Color(0.7f, 0.7f, 0.75f, 0.25f);
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => owner.StartLevel(level));
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.DOKill(false);
        rect.anchoredPosition = index == 0 ? leftOptionPosition : rightOptionPosition;
        rect.localScale = Vector3.zero;
        rect.DOScale(Vector3.one, optionShowDuration).SetDelay(delay).SetEase(optionShowEase).SetTarget(this);
    }

    private void EnsureOptionCapacity(int index)
    {
        while (optionButtons.Count <= index)
            optionButtons.Add(null);
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;

        if (!hasShownAnchoredPosition)
        {
            shownAnchoredPosition = rectTransform.anchoredPosition;
            hasShownAnchoredPosition = true;
        }

        if (revealMask == null)
        {
            Transform maskTransform = transform.Find("RevealMask");
            revealMask = maskTransform as RectTransform;
        }

        if (contentRoot == null)
        {
            Transform contentTransform = revealMask != null ? revealMask.Find("PanelContent") : transform.Find("PanelContent");
            contentRoot = contentTransform as RectTransform;
        }

        Vector2 rectSize = rectTransform.rect.size;
        fullPanelSize = rectSize.x > 0f && rectSize.y > 0f ? rectSize : rectTransform.sizeDelta;
        ConfigureRevealLayout(true);
    }

    private Transform GetOptionRoot()
    {
        return contentRoot != null ? contentRoot : transform;
    }

    private void ConfigureRevealLayout(bool fromTopLeft)
    {
        if (revealMask == null)
            return;

        revealMask.anchorMin = new Vector2(0.5f, 0.5f);
        revealMask.anchorMax = new Vector2(0.5f, 0.5f);
        revealMask.pivot = fromTopLeft ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
        revealMask.anchoredPosition = fromTopLeft
            ? new Vector2(fullPanelSize.x * -0.5f, fullPanelSize.y * 0.5f)
            : new Vector2(fullPanelSize.x * 0.5f, fullPanelSize.y * -0.5f);

        if (contentRoot == null)
            return;

        contentRoot.anchorMin = fromTopLeft ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
        contentRoot.anchorMax = contentRoot.anchorMin;
        contentRoot.pivot = new Vector2(0.5f, 0.5f);
        contentRoot.anchoredPosition = fromTopLeft
            ? new Vector2(fullPanelSize.x * 0.5f, fullPanelSize.y * -0.5f)
            : new Vector2(fullPanelSize.x * -0.5f, fullPanelSize.y * 0.5f);
        contentRoot.sizeDelta = fullPanelSize;
    }

    private void PreparePanelShow()
    {
        ConfigureRevealLayout(true);
        rectTransform.anchoredPosition = shownAnchoredPosition - panelMoveOffset;
        SetRevealProgress(0f);
    }

    private void PlayPanelShow()
    {
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(DOVirtual.Float(0f, 1f, panelShowDuration, SetRevealProgress).SetEase(panelShowEase));
        sequence.Join(rectTransform.DOAnchorPos(shownAnchoredPosition, panelShowDuration).SetEase(panelShowEase));
    }

    private void SetRevealProgress(float progress)
    {
        if (revealMask == null)
            return;

        float clampedProgress = Mathf.Clamp01(progress);
        revealMask.sizeDelta = new Vector2(fullPanelSize.x * clampedProgress, fullPanelSize.y * clampedProgress);
    }

    private void SetOptionButtonsInteractable(bool interactable)
    {
        for (int i = 0; i < optionButtons.Count; i++)
        {
            if (optionButtons[i] == null)
                continue;

            optionButtons[i].interactable = interactable;
        }
    }

    private void ResetOptionScales()
    {
        for (int i = 0; i < optionButtons.Count; i++)
        {
            if (optionButtons[i] == null)
                continue;

            optionButtons[i].transform.localScale = Vector3.one;
        }
    }
}
