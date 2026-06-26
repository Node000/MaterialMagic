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
    [SerializeField] private bool syncTitleColorWithFrame = true;

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
}
