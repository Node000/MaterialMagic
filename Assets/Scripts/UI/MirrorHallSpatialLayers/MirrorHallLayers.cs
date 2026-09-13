using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// For every adjacent pair A/B: clip the subject to outer A and draw it
/// over inner B. Uses normal nested RectMask2D components and UI ordering.
/// The source texture must exclude this presentation and its subjects.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class MirrorHallLayers : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private RenderTexture sceneTexture;
    [SerializeField] private Sprite subjectSprite;

    [Header("Hall")]
    [SerializeField, Range(1, 32)] private int layerCount = 20;
    [SerializeField, Range(0.3f, 0.9f)] private float shrink = 0.72f;
    [SerializeField] private Vector2 offsetPerStep = Vector2.zero;
    [SerializeField] private Color frameColor = new Color(0.9f, 0.15f, 0.15f, 1f);
    [SerializeField, Min(0f)] private float frameThickness = 2f;

    [Header("Subject: fractions of its OUTER frame A")]
    [Tooltip("0 is A's centre, 0.5 is A's right edge. At Shrink 0.72, inner B's right edge is 0.36.")]
    [SerializeField] private Vector2 subjectPosition = new Vector2(0.36f, 0f);
    [SerializeField] private Vector2 subjectSize = new Vector2(0.16f, 0.62f);
    [SerializeField] private Color subjectColor = Color.white;

    private sealed class LayerView
    {
        public RectTransform rect;
        public RawImage background;
        public Image subject;
        public Image[] edges;
    }

    private RectTransform host;
    private RectTransform generatedRoot;
    private LayerView[] layers;

    private void OnEnable()
    {
        Rebuild();
    }

    [ContextMenu("Rebuild Layers")]
    public void Rebuild()
    {
        if (!Application.isPlaying)
            return;

        ClearGenerated();
        if (sceneTexture == null || subjectSprite == null)
        {
            Debug.LogWarning("MirrorHallLayers: assign Scene Texture and Subject Sprite, then enable the component again.", this);
            return;
        }

        host = (RectTransform)transform;
        // One terminal picture behind the last subject.
        layers = new LayerView[Mathf.Clamp(layerCount, 1, 32) + 1];
        generatedRoot = CreateLayer(0, host);
        Stretch(generatedRoot);
        UpdateLayout();
    }

    private RectTransform CreateLayer(int index, RectTransform parent)
    {
        var go = new GameObject("Layer " + index,
            typeof(RectTransform), typeof(RectMask2D));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        CentreAnchors(rect);

        var view = new LayerView { rect = rect };
        layers[index] = view;
        view.background = CreateGraphic<RawImage>("Background", rect);
        Stretch(view.background.rectTransform);

        if (index + 1 < layers.Length)
        {
            // Entire inner B subtree is drawn BEFORE this layer's subject.
            CreateLayer(index + 1, rect);
            view.subject = CreateGraphic<Image>("Subject " + index, rect);
            view.subject.preserveAspect = true;
            view.subject.maskable = true;
        }

        // The outer rim is drawn last; its centre remains transparent.
        var frame = new GameObject("Frame", typeof(RectTransform))
            .GetComponent<RectTransform>();
        frame.SetParent(rect, false);
        Stretch(frame);
        view.edges = new Image[4];
        string[] names = { "Top", "Bottom", "Left", "Right" };
        for (int i = 0; i < view.edges.Length; i++)
            view.edges[i] = CreateGraphic<Image>(names[i], frame);
        return rect;
    }

    private void LateUpdate()
    {
        if (generatedRoot != null)
            UpdateLayout();
    }

    private void UpdateLayout()
    {
        Vector2 fullSize = host.rect.size;
        float ratio = Mathf.Clamp(shrink, 0.3f, 0.9f);
        float scale = 1f;

        for (int i = 0; i < layers.Length; i++)
        {
            LayerView view = layers[i];
            if (i > 0)
            {
                view.rect.anchoredPosition = offsetPerStep * scale;
                scale *= ratio;
                view.rect.sizeDelta = fullSize * scale;
            }

            Vector2 size = fullSize * scale;
            view.background.texture = sceneTexture;
            if (view.subject != null)
            {
                view.subject.sprite = subjectSprite;
                view.subject.color = subjectColor;
                view.subject.rectTransform.anchoredPosition =
                    Vector2.Scale(size, subjectPosition);
                view.subject.rectTransform.sizeDelta = Vector2.Scale(size,
                    new Vector2(Mathf.Max(0f, subjectSize.x), Mathf.Max(0f, subjectSize.y)));
            }

            float maxThickness = Mathf.Max(0f, Mathf.Min(size.x, size.y) * 0.5f);
            float t = frameThickness <= 0f ? 0f :
                Mathf.Min(maxThickness, Mathf.Max(0.5f, frameThickness * scale));
            SetEdge(view.edges[0], new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -t), Vector2.zero);
            SetEdge(view.edges[1], Vector2.zero, new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, t));
            SetEdge(view.edges[2], Vector2.zero, new Vector2(0f, 1f),
                new Vector2(0f, t), new Vector2(t, -t));
            SetEdge(view.edges[3], new Vector2(1f, 0f), Vector2.one,
                new Vector2(-t, t), new Vector2(0f, -t));
        }
    }

    private void SetEdge(Image edge, Vector2 minAnchor, Vector2 maxAnchor,
        Vector2 minOffset, Vector2 maxOffset)
    {
        RectTransform rect = edge.rectTransform;
        rect.anchorMin = minAnchor;
        rect.anchorMax = maxAnchor;
        rect.offsetMin = minOffset;
        rect.offsetMax = maxOffset;
        edge.color = frameColor;
    }

    public void SetSubjectSprite(Sprite sprite)
    {
        subjectSprite = sprite;
    }

    private static T CreateGraphic<T>(string objectName, RectTransform parent)
        where T : Graphic
    {
        var go = new GameObject(objectName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(T));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        CentreAnchors(rect);
        T graphic = go.GetComponent<T>();
        graphic.raycastTarget = false;
        graphic.color = Color.white;
        return graphic;
    }

    private static void CentreAnchors(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnDisable()
    {
        ClearGenerated();
    }

    private void ClearGenerated()
    {
        if (generatedRoot == null)
            return;
        generatedRoot.gameObject.SetActive(false);
        Destroy(generatedRoot.gameObject);
        generatedRoot = null;
        layers = null;
    }
}
