using UnityEngine;

[CreateAssetMenu(fileName = "BuffPopupEffectSettings", menuName = "Config/Buff Popup Effect Settings")]
public class BuffPopupEffectSettings : ScriptableObject
{
    [SerializeField] private float fadeInDuration = 0.12f;
    [SerializeField] private float fadeOutDuration = 0.18f;
    [SerializeField] private float displayDuration = 0.18f;
    [SerializeField] private float interval = 0.08f;
    [SerializeField] private float startScale = 1f;
    [SerializeField] private float endScale = 1.5f;
    [SerializeField] [Range(0f, 1f)] private float peakAlpha = 0.5f;

    public float FadeInDuration => Mathf.Max(0f, fadeInDuration);
    public float FadeOutDuration => Mathf.Max(0f, fadeOutDuration);
    public float DisplayDuration => Mathf.Max(0f, displayDuration);
    public float Interval => Mathf.Max(0f, interval);
    public float StartScale => Mathf.Max(0f, startScale);
    public float EndScale => Mathf.Max(0f, endScale);
    public float PeakAlpha => Mathf.Clamp01(peakAlpha);
}
