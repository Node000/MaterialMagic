using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(fileName = "UnifiedDetailPopupTheme", menuName = "Config/Unified Detail Popup Theme")]
public class UnifiedDetailPopupTheme : ScriptableObject
{
    [Header("Animation")]
    [SerializeField] private Vector3 hiddenScale = new Vector3(0.82f, 0.82f, 1f);
    [SerializeField] private float fadeDuration = 0.12f;
    [SerializeField] private float scaleDuration = 0.18f;
    [SerializeField] private float hoverExitHideDelay = 0.15f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;

    public Vector3 HiddenScale => hiddenScale;
    public float FadeDuration => Mathf.Max(0f, fadeDuration);
    public float ScaleDuration => Mathf.Max(0f, scaleDuration);
    public float HoverExitHideDelay => Mathf.Max(0f, hoverExitHideDelay);
    public Ease ShowEase => showEase;
    public Ease HideEase => hideEase;
}
