using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public enum UnifiedDetailIconTintMode
{
    None = 0,
    White = 1,
    Accent = 2,
    StyleLineColor = 3
}

[Serializable]
public class UnifiedDetailStyle
{
    public UnifiedDetailSourceType sourceType;
    public Color lineColor = Color.white;
    public Color titleColor = Color.white;
    public Color bodyColor = Color.white;
    public Color backgroundColor = new Color(0.06f, 0.06f, 0.08f, 0.96f);
    public UnifiedDetailIconTintMode iconTintMode = UnifiedDetailIconTintMode.White;
}

[CreateAssetMenu(fileName = "UnifiedDetailPopupTheme", menuName = "GlobalConfig/Unified Detail Popup Theme")]
public class UnifiedDetailPopupTheme : ScriptableObject
{
    [Header("Animation")]
    [SerializeField] private Vector3 hiddenScale = new Vector3(0.82f, 0.82f, 1f);
    [SerializeField] private float fadeDuration = 0.12f;
    [SerializeField] private float scaleDuration = 0.18f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;

    [Header("Layout")]
    [SerializeField] private Vector2 popupSize = new Vector2(660f, 213f);
    [SerializeField] private Vector2 iconSize = new Vector2(136f, 136f);

    [Header("Typography")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private float titleFontSize = 44f;
    [SerializeField] private FontStyles titleFontStyle = FontStyles.Bold;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private float bodyFontSize = 30f;
    [SerializeField] private FontStyles bodyFontStyle = FontStyles.Normal;
    [SerializeField] private float bodyLineSpacing = 0f;

    [Header("Per Source Type")]
    [SerializeField] private List<UnifiedDetailStyle> styles = new List<UnifiedDetailStyle>();

    private Dictionary<UnifiedDetailSourceType, UnifiedDetailStyle> styleByType;

    public Vector3 HiddenScale => hiddenScale;
    public float FadeDuration => Mathf.Max(0f, fadeDuration);
    public float ScaleDuration => Mathf.Max(0f, scaleDuration);
    public Ease ShowEase => showEase;
    public Ease HideEase => hideEase;
    public Vector2 PopupSize => popupSize;
    public Vector2 IconSize => iconSize;
    public TMP_FontAsset TitleFont => titleFont;
    public float TitleFontSize => Mathf.Max(1f, titleFontSize);
    public FontStyles TitleFontStyle => titleFontStyle;
    public TMP_FontAsset BodyFont => bodyFont;
    public float BodyFontSize => Mathf.Max(1f, bodyFontSize);
    public FontStyles BodyFontStyle => bodyFontStyle;
    public float BodyLineSpacing => bodyLineSpacing;

    public UnifiedDetailStyle GetStyle(UnifiedDetailSourceType sourceType)
    {
        EnsureStyleMap();
        if (styleByType.TryGetValue(sourceType, out UnifiedDetailStyle style) && style != null)
            return style;
        return new UnifiedDetailStyle { sourceType = sourceType };
    }

    private void OnEnable()
    {
        styleByType = null;
    }

    private void OnValidate()
    {
        styleByType = null;
    }

    private void EnsureStyleMap()
    {
        if (styleByType != null)
            return;

        styleByType = new Dictionary<UnifiedDetailSourceType, UnifiedDetailStyle>();
        for (int i = 0; i < styles.Count; i++)
        {
            UnifiedDetailStyle style = styles[i];
            if (style != null)
                styleByType[style.sourceType] = style;
        }
    }
}
