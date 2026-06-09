using DG.Tweening;
using UnityEngine;
using TMPro;

public class EnemyIntentTooltipUI : MonoBehaviour
{
    private static readonly Vector3 HiddenScale = new Vector3(0.82f, 0.82f, 1f);

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private float tooltipFadeDuration = 0.12f;
    [SerializeField] private float tooltipScaleDuration = 0.18f;
    [SerializeField] private Ease tooltipShowEase = Ease.OutBack;
    [SerializeField] private Ease tooltipHideEase = Ease.InBack;
    [SerializeField] private float tooltipXOffset = 24f;
    [SerializeField] private float tooltipYOffset = 0f;
    [SerializeField] private Vector2 tooltipSize = new Vector2(380f, 168f);
    [SerializeField] private float descriptionWidth = 300f;

    private CanvasGroup canvasGroup;
    private Tween tween;
    private EnemyIntentView currentView;

    public void Initialize(HandSystemUI owner)
    {
        CacheReferences();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        transform.localScale = HiddenScale;
        gameObject.SetActive(false);
    }

    public void Show(EnemyIntentView view, EnemyModel enemy, EnemyIntentData intent, PlayerState playerState)
    {
        if (view == null || enemy == null || intent == null)
            return;

        CacheReferences();
        currentView = view;
        if (titleText != null)
            titleText.text = enemy.GetIntentTooltipTitle(intent);
        if (descriptionText != null)
            descriptionText.text = enemy.GetIntentTooltipDescription(intent, playerState);

        RectTransform rect = (RectTransform)transform;
        rect.sizeDelta = tooltipSize;
        gameObject.SetActive(true);
        transform.localScale = HiddenScale;
        canvasGroup.alpha = 0f;
        Vector3 localLeftPosition = new Vector3(
            -view.RectTransform.rect.width * 0.5f - tooltipSize.x * 0.5f - tooltipXOffset,
            tooltipYOffset,
            0f);
        rect.position = view.RectTransform.TransformPoint(localLeftPosition);
        PopupLayerUtility.ApplyTo(rect);
        transform.SetAsLastSibling();

        tween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(canvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(transform.DOScale(Vector3.one, tooltipScaleDuration).SetEase(tooltipShowEase));
        tween = sequence;
    }

    public void Hide(EnemyIntentView view)
    {
        if (view != null && currentView != null && view != currentView)
            return;
        if (!gameObject.activeSelf)
            return;

        currentView = null;
        tween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(canvasGroup.DOFade(0f, tooltipFadeDuration));
        sequence.Join(transform.DOScale(HiddenScale, tooltipScaleDuration).SetEase(tooltipHideEase));
        sequence.OnComplete(() => gameObject.SetActive(false));
        tween = sequence;
    }

    private void CacheReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        if (titleText == null)
        {
            Transform titleTransform = UIManager.FindChildRecursive(transform, "Title");
            titleText = titleTransform != null ? titleTransform.GetComponent<TMP_Text>() : null;
        }
        if (descriptionText == null)
        {
            Transform descriptionTransform = UIManager.FindChildRecursive(transform, "Description");
            descriptionText = descriptionTransform != null ? descriptionTransform.GetComponent<TMP_Text>() : null;
        }
        if (descriptionText != null)
        {
            descriptionText.enableWordWrapping = true;
            descriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, descriptionWidth);
        }
    }

    private void OnDestroy()
    {
        tween?.Kill(false);
    }
}
