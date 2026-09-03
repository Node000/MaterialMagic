using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class MagicBookBackgroundLayer : MonoBehaviour
{
    private sealed class BackgroundPair
    {
        public SpringLineHighlightUI source;
        public SpringLineHighlightUI background;
        public RectTransform sourceRect;
        public RectTransform backgroundRect;
    }

    private readonly List<BackgroundPair> pairs = new List<BackgroundPair>();
    private RectTransform backgroundLayer;
    private bool isRefreshing;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        EnsureBackgroundLayer();
        Refresh();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            Refresh();
    }

    private void OnTransformChildrenChanged()
    {
        if (Application.isPlaying)
            Refresh();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        for (int i = 0; i < pairs.Count; i++)
            SyncPair(pairs[i]);
    }

    public void Refresh()
    {
        // 背景层已废弃：纯黑背景由槽位自身的弹簧线框填充承担，不再复制一份背景壳，
        // 否则在道具飞入重排时其运行时同步会错乱，导致布局后所有道具背景消失。
        return;
    }

    private bool HasPair(Transform sourceTransform)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].source != null && pairs[i].source.transform == sourceTransform)
                return true;
        }

        return false;
    }

    private BackgroundPair CreatePair(SpringLineHighlightUI source)
    {
        GameObject background = new GameObject(source.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SpringLineHighlightUI), typeof(LayoutElement));
        background.transform.SetParent(backgroundLayer, false);
        background.transform.SetAsFirstSibling();
        background.GetComponent<LayoutElement>().ignoreLayout = true;

        SpringLineHighlightUI backgroundHighlight = background.GetComponent<SpringLineHighlightUI>();
        backgroundHighlight.CopyVisualSettingsFrom(source);
        backgroundHighlight.SetRenderingEnabled(true);

        Shadow sourceShadow = source.GetComponent<Shadow>();
        if (sourceShadow != null)
        {
            Shadow backgroundShadow = background.AddComponent<Shadow>();
            backgroundShadow.effectColor = sourceShadow.effectColor;
            backgroundShadow.effectDistance = sourceShadow.effectDistance;
            backgroundShadow.useGraphicAlpha = sourceShadow.useGraphicAlpha;
        }

        source.SetRenderingEnabled(false);
        return new BackgroundPair
        {
            source = source,
            background = backgroundHighlight,
            sourceRect = source.rectTransform,
            backgroundRect = backgroundHighlight.rectTransform
        };
    }

    private void SyncPair(BackgroundPair pair)
    {
        if (pair.source == null || pair.background == null)
            return;

        pair.source.SetRenderingEnabled(false);
        pair.background.SetFillEnabled(pair.source.FillEnabled);
        pair.background.gameObject.SetActive(pair.source.gameObject.activeSelf);

        pair.backgroundRect.anchorMin = pair.sourceRect.anchorMin;
        pair.backgroundRect.anchorMax = pair.sourceRect.anchorMax;
        pair.backgroundRect.pivot = pair.sourceRect.pivot;
        pair.backgroundRect.anchoredPosition = pair.sourceRect.anchoredPosition;
        pair.backgroundRect.sizeDelta = pair.sourceRect.sizeDelta;
        pair.backgroundRect.localRotation = pair.sourceRect.localRotation;
        pair.backgroundRect.localScale = pair.sourceRect.localScale;
    }

    private void EnsureBackgroundLayer()
    {
        if (backgroundLayer != null)
            return;

        backgroundLayer = transform as RectTransform;
    }
}
