using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LevelSelectPanelUI : MonoBehaviour
{
    [Header("动画参数")]
    [SerializeField] private float hideDuration = 0.18f;
    [SerializeField] private Ease hideEase = Ease.InBack;
    [SerializeField] private float optionShowDuration = 0.28f;
    [SerializeField] private float optionDelayStep = 0.08f;
    [SerializeField] private Ease optionShowEase = Ease.OutBack;
    [SerializeField] private Vector2 leftOptionPosition = new Vector2(-150f, -22f);
    [SerializeField] private Vector2 rightOptionPosition = new Vector2(150f, -22f);

    private readonly List<Button> optionButtons = new List<Button>();
    private HandSystemUI owner;
    private RectTransform rectTransform;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        rectTransform = (RectTransform)transform;
    }

    public void Show(IReadOnlyList<RunMapNodeModel> nodes, int currentNodeIndex)
    {
        if (nodes == null || currentNodeIndex >= nodes.Count)
            return;

        RunMapNodeModel nodeModel = nodes[currentNodeIndex];
        gameObject.SetActive(true);
        transform.localScale = Vector3.one;
        Text title = UIManager.FindChildComponent<Text>(transform, "Title");
        if (title != null)
            title.text = "选择下一关";

        if (nodeModel.fixedSingleChoice)
        {
            BindOption(0, nodeModel.leftLevel, 0f);
            HideOption(1);
            if (optionButtons.Count > 0 && optionButtons[0] != null)
                optionButtons[0].GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            return;
        }

        BindOption(0, nodeModel.leftLevel, 0f);
        BindOption(1, nodeModel.rightLevel, optionDelayStep);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void HideAnimated()
    {
        RectTransform panel = rectTransform != null ? rectTransform : (RectTransform)transform;
        panel.DOScale(Vector3.zero, hideDuration).SetEase(hideEase).SetTarget(this).OnComplete(() =>
        {
            panel.gameObject.SetActive(false);
            panel.localScale = Vector3.one;
        });
    }

    private void HideOption(int index)
    {
        EnsureOptionCapacity(index);
        Button button = optionButtons[index];
        if (button == null)
        {
            Transform optionTransform = transform.Find(index == 0 ? "LeftOption" : "RightOption");
            if (optionTransform != null)
                button = optionTransform.GetComponent<Button>();
            optionButtons[index] = button;
        }

        if (button != null)
            button.gameObject.SetActive(false);
    }

    private void BindOption(int index, LevelData level, float delay)
    {
        EnsureOptionCapacity(index);
        Button button = optionButtons[index];
        if (button == null)
        {
            Transform optionTransform = transform.Find(index == 0 ? "LeftOption" : "RightOption");
            if (optionTransform == null)
                return;
            button = optionTransform.GetComponent<Button>();
            optionButtons[index] = button;
        }

        if (button == null)
            return;

        button.gameObject.SetActive(true);
        Text text = UIManager.FindChildComponent<Text>(button.transform, "Text");
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
        rect.anchoredPosition = index == 0 ? leftOptionPosition : rightOptionPosition;
        rect.localScale = Vector3.zero;
        rect.DOScale(Vector3.one, optionShowDuration).SetDelay(delay).SetEase(optionShowEase).SetTarget(this);
    }

    private void EnsureOptionCapacity(int index)
    {
        while (optionButtons.Count <= index)
            optionButtons.Add(null);
    }
}
