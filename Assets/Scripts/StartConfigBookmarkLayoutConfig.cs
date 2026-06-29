using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "StartConfigBookmarkLayoutConfig", menuName = "Config/Start Config Bookmark Layout Config")]
public class StartConfigBookmarkLayoutConfig : ScriptableObject
{
    [SerializeField] private float materialCardScale = 0.42f;
    [SerializeField] private Vector2 materialCardAnchoredPosition = Vector2.zero;
    [SerializeField] private Vector2 materialItemFallbackSize = new Vector2(118f, 72f);
    [SerializeField] private Vector2 materialItemFallbackSpacing = new Vector2(126f, 76f);
    [SerializeField, Min(1)] private int materialItemFallbackColumns = 2;
    [SerializeField] private Color materialPreviewFrameColor = Color.clear;
    [SerializeField] private bool materialPreviewFrameRaycastTarget = true;
    [SerializeField] private bool materialPreviewShadowEnabled;
    [SerializeField] private Vector2 materialPreviewIconAnchorMin = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 materialPreviewIconAnchorMax = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 materialPreviewIconPivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 materialPreviewIconAnchoredPosition = Vector2.zero;
    [SerializeField] private Vector2 materialPreviewIconSize = new Vector2(96f, 96f);
    [SerializeField] private Color materialPreviewIconColor = Color.white;
    [SerializeField] private bool materialPreviewIconRaycastTarget;
    [SerializeField] private bool materialPreviewIconPreserveAspect = true;
    [SerializeField] private Vector2 materialCountTextAnchorMin = new Vector2(1f, 0.5f);
    [SerializeField] private Vector2 materialCountTextAnchorMax = new Vector2(1f, 0.5f);
    [SerializeField] private Vector2 materialCountTextPivot = new Vector2(1f, 0.5f);
    [SerializeField] private Vector2 materialCountTextAnchoredPosition = new Vector2(0f, -6f);
    [SerializeField] private Vector2 materialCountTextSize = new Vector2(52f, 28f);
    [SerializeField] private TMP_FontAsset materialCountTextFont;
    [SerializeField] private float materialCountTextFontSize = 22f;
    [SerializeField] private FontStyles materialCountTextFontStyle = FontStyles.Bold;
    [SerializeField] private TextAlignmentOptions materialCountTextAlignment = TextAlignmentOptions.MidlineRight;
    [SerializeField] private Color materialCountTextColor = new Color(1f, 0.94f, 0.7f, 1f);

    public float MaterialCardScale => Mathf.Max(0.01f, materialCardScale);
    public Vector2 MaterialCardAnchoredPosition => materialCardAnchoredPosition;
    public Vector2 MaterialItemFallbackSize => materialItemFallbackSize;
    public Vector2 MaterialItemFallbackSpacing => materialItemFallbackSpacing;
    public int MaterialItemFallbackColumns => Mathf.Max(1, materialItemFallbackColumns);
    public Color MaterialPreviewFrameColor => materialPreviewFrameColor;
    public bool MaterialPreviewFrameRaycastTarget => materialPreviewFrameRaycastTarget;
    public bool MaterialPreviewShadowEnabled => materialPreviewShadowEnabled;
    public Vector2 MaterialPreviewIconAnchorMin => materialPreviewIconAnchorMin;
    public Vector2 MaterialPreviewIconAnchorMax => materialPreviewIconAnchorMax;
    public Vector2 MaterialPreviewIconPivot => materialPreviewIconPivot;
    public Vector2 MaterialPreviewIconAnchoredPosition => materialPreviewIconAnchoredPosition;
    public Vector2 MaterialPreviewIconSize => materialPreviewIconSize;
    public Color MaterialPreviewIconColor => materialPreviewIconColor;
    public bool MaterialPreviewIconRaycastTarget => materialPreviewIconRaycastTarget;
    public bool MaterialPreviewIconPreserveAspect => materialPreviewIconPreserveAspect;
    public Vector2 MaterialCountTextAnchorMin => materialCountTextAnchorMin;
    public Vector2 MaterialCountTextAnchorMax => materialCountTextAnchorMax;
    public Vector2 MaterialCountTextPivot => materialCountTextPivot;
    public Vector2 MaterialCountTextAnchoredPosition => materialCountTextAnchoredPosition;
    public Vector2 MaterialCountTextSize => materialCountTextSize;
    public TMP_FontAsset MaterialCountTextFont => materialCountTextFont;
    public float MaterialCountTextFontSize => materialCountTextFontSize;
    public FontStyles MaterialCountTextFontStyle => materialCountTextFontStyle;
    public TextAlignmentOptions MaterialCountTextAlignment => materialCountTextAlignment;
    public Color MaterialCountTextColor => materialCountTextColor;
}
