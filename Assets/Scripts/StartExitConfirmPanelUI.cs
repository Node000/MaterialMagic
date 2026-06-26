using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartExitConfirmPanelUI : MonoBehaviour
{
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string prompt = "是否退出游戏？";
    [SerializeField] private Vector2 shownPosition = new Vector2(280f, -220f);
    [SerializeField] private Vector2 hiddenPosition = new Vector2(-620f, -220f);
    [SerializeField] private float moveDuration = 0.32f;
    [SerializeField] private Ease showEase = Ease.OutCubic;
    [SerializeField] private Ease hideEase = Ease.OutCubic;

    private Tween moveTween;

    public bool IsShowing => gameObject.activeSelf;

    private void Awake()
    {
        ResolveReferences();
        if (promptText != null)
            promptText.text = prompt;
        if (panelRect != null)
            panelRect.anchoredPosition = hiddenPosition;
    }

    private void OnDestroy()
    {
        moveTween?.Kill(false);
    }

    public void Show()
    {
        Show(prompt);
    }

    public void Show(string message)
    {
        if (panelRect == null)
            return;

        if (promptText != null)
            promptText.text = message;
        gameObject.SetActive(true);
        moveTween?.Kill(false);
        panelRect.anchoredPosition = hiddenPosition;
        moveTween = panelRect.DOAnchorPos(shownPosition, moveDuration)
            .SetEase(showEase)
            .SetUpdate(true)
            .SetTarget(this);
    }

    public void Hide()
    {
        if (!gameObject.activeSelf || panelRect == null)
            return;

        moveTween?.Kill(false);
        moveTween = panelRect.DOAnchorPos(hiddenPosition, moveDuration)
            .SetEase(hideEase)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(() => gameObject.SetActive(false));
    }

    public bool Contains(Transform hit)
    {
        return hit != null && hit.IsChildOf(transform);
    }

    private void ResolveReferences()
    {
        if (panelRect == null)
            panelRect = transform as RectTransform;
        if (promptText == null)
            promptText = transform.Find("Text")?.GetComponent<TMP_Text>();
    }
}
