using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 出牌区与道具栏之间的“本回合已打出 / 打出上限”显示。
/// 文字内容与三段配色（正常 / 预警 / 打满）全部在 Inspector 上配置；
/// 进出战斗的开关动画与商店面板一致：压成中心水平亮线 ↔ 竖向展开。
/// </summary>
public class PlayLimitDisplayUI : MonoBehaviour
{
    private const string DefaultFormatKey = "ui.battle.play_limit_format";
    private const string DefaultFormatFallback = "{0}/{1}";

    [Header("引用")]
    [Tooltip("显示文本；留空时自动取本物体上的 TMP_Text。")]
    [SerializeField] private TMP_Text label;

    [Header("文本")]
    [Tooltip("本地化格式键，{0} = 本回合已打出数量，{1} = 打出上限。")]
    [SerializeField] private string formatKey = DefaultFormatKey;

    [SerializeField] private string formatFallback = DefaultFormatFallback;

    [Header("配色")]
    [Tooltip("剩余额度充足时的颜色。")]
    [SerializeField] private Color normalColor = Color.white;

    [Tooltip("剩余额度进入预警区间时的颜色。")]
    [SerializeField] private Color warningColor = new Color(1f, 0.84f, 0.35f, 1f);

    [Tooltip("已打满上限时的颜色。")]
    [SerializeField] private Color reachedColor = new Color(1f, 0.45f, 0.38f, 1f);

    [Header("预警阈值")]
    [Tooltip("剩余可打出数量小于等于该值时使用预警色；已打满上限时使用打满色。")]
    [SerializeField] private int warningRemainingThreshold = 2;

    [Header("开关动画（对齐商店面板）")]
    [Tooltip("展开/压线的时长。")]
    [SerializeField] private float crtCollapseDuration = 0.32f;

    [SerializeField] private Ease crtCollapseEase = Ease.InCubic;

    [Tooltip("收起时亮线停留时长。")]
    [SerializeField] private float crtLineHoldDuration = 0.12f;

    [Tooltip("收起时横向收缩时长。")]
    [SerializeField] private float crtShrinkDuration = 0.18f;

    [SerializeField] private Ease crtShrinkEase = Ease.InCubic;

    [Tooltip("亮线高度占整体高度的比例。")]
    [SerializeField, Range(0.005f, 0.2f)] private float crtLineYRatio = 0.02f;

    [Header("目标")]
    [Tooltip("参与开合缩放的目标；留空时用本物体。")]
    [SerializeField] private RectTransform scaleTarget;

    [Tooltip("显隐作用的物体；留空时用本物体。当“底”是父物体、脚本留在文字子物体上时，指定“底”即可连同底一起开关。")]
    [SerializeField] private GameObject visibilityTarget;

    private RectTransform rect;
    private Vector3 baseScale = Vector3.one;
    private Vector2 baseAnchoredPosition;
    private bool layoutCaptured;
    private Tween stateTween;

    /// <summary>当前是否处于“已展开显示”状态。</summary>
    public bool IsShown { get; private set; }

    /// <summary>开合缩放作用的物体（默认本物体）。</summary>
    public RectTransform ScaleTarget => Rect;

    /// <summary>显隐作用的物体（默认本物体）。</summary>
    public GameObject VisibilityRoot => visibilityTarget != null ? visibilityTarget : gameObject;

    private RectTransform Rect
    {
        get
        {
            if (scaleTarget != null)
                return scaleTarget;

            if (rect == null)
                rect = transform as RectTransform;
            return rect;
        }
    }

    private void Awake()
    {
        CaptureLayout();
        if (Application.isPlaying)
            ApplyHiddenState();
    }

    private void OnDisable()
    {
        KillStateTween();
    }

    /// <summary>进入战斗时显示：从一条中心水平亮线竖向展开（instant 用于读档直接进战斗）。</summary>
    public void ShowForBattle(bool instant = false)
    {
        CaptureLayout();
        KillStateTween();
        VisibilityRoot.SetActive(true);
        // 显隐目标可能是外层父物体；本物体与显示文本也一并确保激活。
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        IsShown = true;

        RectTransform target = Rect;
        if (target == null)
            return;

        float duration = instant ? 0f : Mathf.Max(0f, crtCollapseDuration);
        if (duration <= 0f)
        {
            target.localScale = baseScale;
            target.anchoredPosition = baseAnchoredPosition;
            return;
        }

        target.localScale = GetLineScale();
        stateTween = target.DOScale(baseScale, duration).SetEase(crtCollapseEase).SetTarget(this);
    }

    /// <summary>离开战斗时收起：压成中心亮线 → 短暂停留 → 横向收缩消失。</summary>
    public void HideForBattle(bool instant = false)
    {
        if (instant || !Application.isPlaying)
        {
            KillStateTween();
            ApplyHiddenState();
            return;
        }

        CaptureLayout();
        KillStateTween();
        IsShown = false;

        RectTransform target = Rect;
        if (target == null)
        {
            ApplyHiddenState();
            return;
        }

        Vector3 lineScale = GetLineScale();
        Sequence sequence = DOTween.Sequence().SetTarget(this);

        float collapse = Mathf.Max(0f, crtCollapseDuration);
        if (collapse > 0f)
            sequence.Append(target.DOScale(lineScale, collapse).SetEase(crtCollapseEase));
        else
            target.localScale = lineScale;

        if (crtLineHoldDuration > 0f)
            sequence.AppendInterval(crtLineHoldDuration);

        float shrink = Mathf.Max(0f, crtShrinkDuration);
        if (shrink > 0f)
            sequence.Append(target.DOScale(new Vector3(baseScale.x * 0.001f, lineScale.y, baseScale.z), shrink).SetEase(crtShrinkEase));

        sequence.OnComplete(ApplyHiddenState);
        stateTween = sequence;
    }

    private void ApplyHiddenState()
    {
        RectTransform target = Rect;
        if (target != null)
        {
            target.localScale = baseScale;
            target.anchoredPosition = baseAnchoredPosition;
        }

        stateTween = null;
        IsShown = false;
        VisibilityRoot.SetActive(false);
    }

    private void CaptureLayout()
    {
        if (layoutCaptured || Rect == null)
            return;

        baseScale = Rect.localScale;
        baseAnchoredPosition = Rect.anchoredPosition;
        layoutCaptured = true;
    }

    private Vector3 GetLineScale()
    {
        return new Vector3(baseScale.x, baseScale.y * crtLineYRatio, baseScale.z);
    }

    private void KillStateTween()
    {
        if (stateTween != null && stateTween.IsActive())
            stateTween.Kill(false);
        stateTween = null;
    }

    public TMP_Text Label
    {
        get
        {
            if (label == null)
                label = GetComponent<TMP_Text>();
            return label;
        }
    }

    /// <summary>刷新显示的数值与颜色。</summary>
    public void Refresh(int played, int limit)
    {
        TMP_Text target = Label;
        if (target == null)
            return;

        int safeLimit = Mathf.Max(0, limit);
        target.text = string.Format(LocalizationSystem.GetText(formatKey, formatFallback), played, safeLimit);
        target.color = ResolveColor(played, safeLimit);
    }

    /// <summary>已打满上限取打满色；剩余额度进入阈值取预警色；其余取正常色。</summary>
    public Color ResolveColor(int played, int limit)
    {
        if (played >= limit)
            return reachedColor;

        if (limit - played <= warningRemainingThreshold)
            return warningColor;

        return normalColor;
    }

    [ContextMenu("预览配色：正常")]
    private void PreviewNormalColor()
    {
        ApplyEditorPreview(0, 7);
    }

    [ContextMenu("预览配色：预警")]
    private void PreviewWarningColor()
    {
        ApplyEditorPreview(5, 7);
    }

    [ContextMenu("预览配色：打满")]
    private void PreviewReachedColor()
    {
        ApplyEditorPreview(7, 7);
    }

    private void ApplyEditorPreview(int played, int limit)
    {
        if (Application.isPlaying)
            return;

        TMP_Text target = Label;
        if (target == null)
            return;

        target.text = string.Format(formatFallback, played, limit);
        target.color = ResolveColor(played, limit);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(target);
#endif
    }
}
