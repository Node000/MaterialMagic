using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Hover Highlight Target Relay")]
public class HoverHighlightTargetRelayUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private readonly List<GameObject> highlights = new List<GameObject>(2);
    private readonly List<JaggedWaveHighlightUI> jaggedWaveHighlights = new List<JaggedWaveHighlightUI>(2);
    private RectTransform cachedRectTransform;
    private Selectable selectable;

    private void Awake()
    {
        cachedRectTransform = transform as RectTransform;
        selectable = GetComponent<Selectable>();
    }

    public void Register(GameObject highlight)
    {
        if (highlight == null || highlights.Contains(highlight))
            return;

        highlights.Add(highlight);
    }

    public void Unregister(GameObject highlight)
    {
        if (highlight == null)
            return;

        highlights.Remove(highlight);
    }

    public void Register(JaggedWaveHighlightUI highlight)
    {
        if (highlight == null || jaggedWaveHighlights.Contains(highlight))
            return;

        jaggedWaveHighlights.Add(highlight);
    }

    public void Unregister(JaggedWaveHighlightUI highlight)
    {
        if (highlight == null)
            return;

        jaggedWaveHighlights.Remove(highlight);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable != null && !selectable.interactable)
            return;

        SetHighlightsActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlightsActive(false);
    }

    private void OnDisable()
    {
        SetHighlightsActive(false);
    }

    private void SetHighlightsActive(bool active)
    {
        for (int i = highlights.Count - 1; i >= 0; i--)
        {
            GameObject highlight = highlights[i];
            if (highlight == null)
            {
                highlights.RemoveAt(i);
                continue;
            }

            highlight.SetActive(active);
        }

        if (cachedRectTransform == null)
            cachedRectTransform = transform as RectTransform;

        for (int i = jaggedWaveHighlights.Count - 1; i >= 0; i--)
        {
            JaggedWaveHighlightUI highlight = jaggedWaveHighlights[i];
            if (highlight == null)
            {
                jaggedWaveHighlights.RemoveAt(i);
                continue;
            }

            highlight.SetHoverVisibleFromTarget(cachedRectTransform, active);
        }
    }
}
