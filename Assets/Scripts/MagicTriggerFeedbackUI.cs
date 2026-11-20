using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Pure presentation: each trigger starts its own set of waves on the slot.</summary>
public sealed class MagicTriggerFeedbackUI : MonoBehaviour
{
    [SerializeField] private Image sourceIcon;
    [SerializeField] private RectTransform waveRoot;
    [SerializeField] private MagicTriggerOutlineGraphic wavePrefab;
    [SerializeField] private CanvasGroup flashFrame;
    [SerializeField, Range(0f, 1f)] private float flashStrength = 0.65f;
    [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;
    [SerializeField, Min(0f)] private float layerInterval = 0.055f;
    [SerializeField, Min(0.01f)] private float layerDuration = 0.24f;
    [SerializeField, Min(1f)] private float endScale = 1.27f;
    [SerializeField, Min(0.1f)] private float outlineWidth = 2.5f;
    [SerializeField, Range(0f, 1f)] private float waveOpacity = 0.9f;

    private struct Wave
    {
        public MagicTriggerOutlineGraphic Graphic;
        public float Elapsed;
    }

    private readonly List<Wave> waves = new List<Wave>();
    private readonly Stack<MagicTriggerOutlineGraphic> pool = new Stack<MagicTriggerOutlineGraphic>();
    private float flashElapsed;

    public void Play(IReadOnlyList<MaterialEnum> materials)
    {
        if (!isActiveAndEnabled)
            return;

        flashElapsed = 0f;
        if (flashFrame != null)
            flashFrame.alpha = flashStrength;

        if (materials == null || sourceIcon == null || !sourceIcon.isActiveAndEnabled ||
            waveRoot == null || wavePrefab == null)
            return;

        for (int i = 0; i < materials.Count; i++)
        {
            MagicTriggerOutlineGraphic graphic = pool.Count > 0 ? pool.Pop() : Instantiate(wavePrefab, waveRoot, false);
            RectTransform rect = graphic.rectTransform;
            rect.SetAsLastSibling();
            rect.pivot = sourceIcon.rectTransform.pivot;
            rect.localScale = Vector3.one;
            graphic.color = MaterialVisualPalette.Active.GetMaterialColor(materials[i]);
            if (!graphic.Configure(sourceIcon, outlineWidth))
            {
                Recycle(graphic);
                continue;
            }
            graphic.gameObject.SetActive(true);
            graphic.canvasRenderer.SetAlpha(i == 0 ? waveOpacity : 0f);
            waves.Add(new Wave { Graphic = graphic, Elapsed = -i * layerInterval });
        }
    }

    private void Update()
    {
        if (waves.Count == 0 && (flashFrame == null || flashFrame.alpha <= 0f))
            return;
        float delta = Time.unscaledDeltaTime;
        if (flashFrame != null && flashFrame.alpha > 0f)
        {
            flashElapsed += delta;
            flashFrame.alpha = flashStrength * (1f - Mathf.Clamp01(flashElapsed / flashDuration));
        }

        for (int i = waves.Count - 1; i >= 0; i--)
        {
            Wave wave = waves[i];
            wave.Elapsed += delta;
            if (wave.Elapsed >= layerDuration)
            {
                Recycle(wave.Graphic);
                waves.RemoveAt(i);
                continue;
            }

            waves[i] = wave;
            if (wave.Elapsed < 0f)
                continue;
            float t = Mathf.Clamp01(wave.Elapsed / layerDuration);
            float fade = 1f - t;
            wave.Graphic.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, endScale, 1f - fade * fade);
            wave.Graphic.canvasRenderer.SetAlpha(waveOpacity * fade * fade);
        }
    }

    private void Recycle(MagicTriggerOutlineGraphic graphic)
    {
        graphic.gameObject.SetActive(false);
        pool.Push(graphic);
    }

    public void Clear()
    {
        foreach (Wave wave in waves)
            if (wave.Graphic != null)
                Recycle(wave.Graphic);
        waves.Clear();
        if (flashFrame != null)
            flashFrame.alpha = 0f;
    }

    private void OnDisable() => Clear();
}
