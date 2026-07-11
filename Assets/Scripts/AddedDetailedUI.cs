using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AddedDetailedUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Graphic frameGraphic;
    [SerializeField] private SpringLineHighlightUI springLine;
    [SerializeField] private GameObject bodyRoot;
    [SerializeField] private ScrollRect bodyScrollRect;
    [SerializeField] private RectTransform bodyViewport;
    [SerializeField] private RectTransform bodyContent;
    [SerializeField] private bool syncTitleColorWithFrame = true;
    [SerializeField] private float autoScrollStartDelay = 1.2f;
    [SerializeField] private float autoScrollDuration = 3f;
    [SerializeField] private float autoScrollPause = 1.2f;
    [SerializeField] private float manualScrollResumeDelay = 2f;

    private Tween autoScrollTween;
    private bool bodyCanScroll;
    private bool scrollInteractionEnabled;
    private bool scrollListenerRegistered;
    private bool updatingAutoScroll;
    private float manualScrollResumeTime;

    public RectTransform RectTransform => transform as RectTransform;

    public void Apply(string title, string body, Color lineColor)
    {
        CacheReferences();
        if (titleText != null)
        {
            titleText.richText = true;
            titleText.text = InlineIconTextFormatter.Format(title);
            if (syncTitleColorWithFrame)
                titleText.color = lineColor;
        }
        if (bodyText != null)
        {
            bodyText.richText = true;
            bodyText.text = InlineIconTextFormatter.Format(body);
        }
        if (bodyRoot != null)
            bodyRoot.SetActive(!string.IsNullOrEmpty(body));
        if (frameGraphic != null)
            frameGraphic.color = lineColor;
        if (springLine != null)
            springLine.SetVerticesDirty();
        RefreshBodyScroll();
    }

    public void SetScrollInteractionEnabled(bool enabled)
    {
        CacheReferences();
        scrollInteractionEnabled = enabled;
        if (scrollInteractionEnabled && bodyCanScroll)
        {
            StopAutoScroll();
            manualScrollResumeTime = Time.unscaledTime + autoScrollStartDelay;
        }
        if (bodyScrollRect != null)
            bodyScrollRect.enabled = scrollInteractionEnabled && bodyCanScroll;
    }

    private void RefreshBodyScroll()
    {
        StopAutoScroll();
        bodyCanScroll = false;
        if (bodyText == null || bodyScrollRect == null || bodyViewport == null || bodyContent == null)
            return;

        Canvas.ForceUpdateCanvases();
        bodyText.ForceMeshUpdate();
        float viewportHeight = Mathf.Max(1f, bodyViewport.rect.height);
        float preferredHeight = Mathf.Ceil(bodyText.preferredHeight);
        float contentHeight = Mathf.Max(viewportHeight, preferredHeight);
        bodyCanScroll = preferredHeight > viewportHeight + 1f;
        bodyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        bodyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        bodyScrollRect.content = bodyContent;
        SetScrollPosition(1f);
        bodyScrollRect.enabled = scrollInteractionEnabled && bodyCanScroll;
        if (scrollInteractionEnabled && bodyCanScroll)
            manualScrollResumeTime = Time.unscaledTime + autoScrollStartDelay;
    }

    private void Update()
    {
        if (!bodyCanScroll || autoScrollTween != null)
            return;
        if (Time.unscaledTime < manualScrollResumeTime)
            return;

        StartAutoScroll();
    }

    private void PauseAutoScrollAfterManualInput()
    {
        StopAutoScroll();
        manualScrollResumeTime = Time.unscaledTime + manualScrollResumeDelay;
    }

    private void StartAutoScroll()
    {
        if (bodyScrollRect == null)
            return;

        Sequence sequence = DOTween.Sequence().SetTarget(this).SetUpdate(true);
        sequence.AppendInterval(autoScrollPause);
        sequence.Append(DOVirtual.Float(1f, 0f, autoScrollDuration, SetScrollPosition).SetEase(Ease.InOutSine));
        sequence.AppendInterval(autoScrollPause);
        sequence.Append(DOVirtual.Float(0f, 1f, autoScrollDuration, SetScrollPosition).SetEase(Ease.InOutSine));
        sequence.SetLoops(-1);
        autoScrollTween = sequence;
    }

    private void SetScrollPosition(float value)
    {
        if (bodyScrollRect == null)
            return;

        updatingAutoScroll = true;
        bodyScrollRect.verticalNormalizedPosition = value;
        updatingAutoScroll = false;
    }

    private void OnScrollPositionChanged(Vector2 value)
    {
        if (!updatingAutoScroll)
            PauseAutoScrollAfterManualInput();
    }

    private void StopAutoScroll()
    {
        autoScrollTween?.Kill(false);
        autoScrollTween = null;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (titleText == null)
            titleText = FindChildText("TitleText");
        if (bodyText == null)
            bodyText = FindChildText("BodyText");
        if (bodyRoot == null && bodyText != null)
            bodyRoot = bodyText.gameObject;
        if (bodyScrollRect == null)
            bodyScrollRect = GetComponentInChildren<ScrollRect>(true);
        if (bodyScrollRect != null && !scrollListenerRegistered)
        {
            bodyScrollRect.onValueChanged.AddListener(OnScrollPositionChanged);
            scrollListenerRegistered = true;
        }
        if (bodyViewport == null && bodyScrollRect != null)
            bodyViewport = bodyScrollRect.viewport;
        if (bodyContent == null && bodyScrollRect != null)
            bodyContent = bodyScrollRect.content;
        if (springLine == null)
            springLine = GetComponent<SpringLineHighlightUI>();
        if (frameGraphic == null)
            frameGraphic = springLine != null ? springLine : GetComponent<Graphic>();
    }

    private TMP_Text FindChildText(string childName)
    {
        Transform found = UIManager.FindChildRecursive(transform, childName);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    private void OnDisable()
    {
        StopAutoScroll();
    }

    private void OnDestroy()
    {
        if (bodyScrollRect != null && scrollListenerRegistered)
            bodyScrollRect.onValueChanged.RemoveListener(OnScrollPositionChanged);
        StopAutoScroll();
    }
}
