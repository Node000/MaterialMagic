using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将道具栏（MagicBookArea）的槽位按从左到右的槽位索引沿一段圆弧排布，
/// 并让每个槽位按径向朝向圆心（槽位下缘对齐圆心，图标保持可读）。
/// 圆弧几何与缩放均为可调参数，不硬编码到逻辑里。
/// [ExecuteAlways]：Edit Mode 下也可即时预览调参；网格被关闭，AutoSpacing 仅在运行时关闭。
/// </summary>
[ExecuteAlways]
public class MagicBookCurveLayout : MonoBehaviour
{
    [Header("槽位间距")]
    [Tooltip("相邻道具中心间距（父 Rect 局部单位）。只显示已占用道具，并沿实际数量居中排布。")]
    [SerializeField] private float slotStep = 258f;
    [Header("圆弧")]
    [Tooltip("圆弧半径（父 Rect 局部单位）：道具数量变化时槽位始终落在这同一个圆上，数量越多覆盖的弧段越长。")]
    [SerializeField] private float arcRadius = 2000f;
    [Tooltip("弧顶相对父 Rect 底边（rect.yMin）的固定抬升：只决定这条圆弧锚在道具栏的哪个高度，不影响半径。")]
    [SerializeField] private float arcTopRaise = 150f;
    [Tooltip("两端相对中点的缩放，用于轻微透视感。")]
    [SerializeField] private float edgeScale = 0.88f;
    [Header("拖拽动画")]
    [Tooltip("拖拽排序时其它道具/占位块让位补位的动画时长（秒）。")]
    [SerializeField] private float reorderShiftDuration = 0.12f;

    private bool layoutDirty = true;
    private readonly List<RectTransform> activeSlots = new List<RectTransform>(16);

    private void Awake()
    {
        DisableLegacyLayout();
    }

    private void OnEnable()
    {
        DisableLegacyLayout();
        layoutDirty = true;
        RefreshLayout();
    }

    private void OnDisable()
    {
        layoutDirty = true;
    }

    private void Update()
    {
        if (!layoutDirty)
            return;

        layoutDirty = false;
        RefreshLayout();
    }

    public void MarkLayoutDirty()
    {
        layoutDirty = true;
    }

    public void RefreshLayout()
    {
        RectTransform area = transform as RectTransform;
        if (area == null)
            return;

        DisableLegacyLayout();
        CollectActiveSlots(area);

        int count = activeSlots.Count;
        if (count == 0)
            return;

        float half = (count - 1) * 0.5f;
        float halfWidth = half * slotStep;
        float baseline = area.rect.yMin;
        float apexY = baseline + arcTopRaise;
        float radius = Mathf.Max(1f, arcRadius);
        float centerY = apexY - radius;

        for (int i = 0; i < count; i++)
        {
            RectTransform slot = activeSlots[i];
            if (slot == null)
                continue;

            float x = (i - half) * slotStep;
            float radial = Mathf.Sqrt(Mathf.Max(0f, radius * radius - x * x));
            float y = centerY + radial;
            float arcFactor = 1f - Mathf.Abs(x) / Mathf.Max(0.001f, halfWidth);

            slot.anchorMin = new Vector2(0.5f, 0.5f);
            slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.DOKill(false);
            slot.anchoredPosition = new Vector2(x, y);
            float rotationDeg = Mathf.Atan2(-x, Mathf.Max(0.0001f, radial)) * Mathf.Rad2Deg;
            slot.localEulerAngles = new Vector3(0f, 0f, rotationDeg);
            float scale = Mathf.Lerp(edgeScale, 1f, arcFactor);
            slot.localScale = new Vector3(scale, scale, 1f);
            SyncJuicyMotionBase(slot, scale, rotationDeg);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(area);
    }

    public void RefreshLayoutAnimated()
    {
        RectTransform area = transform as RectTransform;
        if (area == null)
            return;

        DisableLegacyLayout();
        CollectActiveSlots(area);
        if (activeSlots.Count == 0)
            return;

        List<RectTransform> ordered = new List<RectTransform>(activeSlots);
        ArrangeSlots(ordered);
    }

    public void ArrangeSlots(List<RectTransform> ordered)
    {
        RectTransform area = transform as RectTransform;
        if (area == null || ordered == null || ordered.Count == 0)
            return;

        DisableLegacyLayout();
        int count = ordered.Count;
        float half = (count - 1) * 0.5f;
        float halfWidth = half * slotStep;
        float baseline = area.rect.yMin;
        float apexY = baseline + arcTopRaise;
        float radius = Mathf.Max(1f, arcRadius);
        float centerY = apexY - radius;

        bool animate = Application.isPlaying && reorderShiftDuration > 0f;
        float duration = Mathf.Max(0.001f, reorderShiftDuration);

        for (int i = 0; i < count; i++)
        {
            RectTransform slot = ordered[i];
            if (slot == null)
                continue;

            float x = (i - half) * slotStep;
            float radial = Mathf.Sqrt(Mathf.Max(0f, radius * radius - x * x));
            float y = centerY + radial;
            float arcFactor = 1f - Mathf.Abs(x) / Mathf.Max(0.001f, halfWidth);
            float rotationDeg = Mathf.Atan2(-x, Mathf.Max(0.0001f, radial)) * Mathf.Rad2Deg;
            float scale = Mathf.Lerp(edgeScale, 1f, arcFactor);

            slot.anchorMin = new Vector2(0.5f, 0.5f);
            slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            SyncJuicyMotionBase(slot, scale, rotationDeg);

            if (animate)
            {
                slot.DOKill(false);
                slot.DOAnchorPos(new Vector2(x, y), duration).SetEase(Ease.OutCubic).SetTarget(slot);
                slot.DOLocalRotate(new Vector3(0f, 0f, rotationDeg), duration).SetEase(Ease.OutCubic).SetTarget(slot);
                slot.DOScale(new Vector3(scale, scale, 1f), duration).SetEase(Ease.OutCubic).SetTarget(slot);
            }
            else
            {
                slot.DOKill(false);
                slot.anchoredPosition = new Vector2(x, y);
                slot.localEulerAngles = new Vector3(0f, 0f, rotationDeg);
                slot.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }

    public Vector2 ComputeAnchorPosition(int count, int index)
    {
        ComputeSlotPose(count, index, out Vector2 pos, out _, out _);
        return pos;
    }

    /// <summary>计算第 index 个槽在 count 个槽圆弧上的完整姿态（位置、径向角度、缩放）。</summary>
    public void ComputeSlotPose(int count, int index, out Vector2 anchoredPosition, out float rotationDeg, out float scale)
    {
        float half = (count - 1) * 0.5f;
        float halfWidth = half * slotStep;
        RectTransform area = transform as RectTransform;
        float baseline = area != null ? area.rect.yMin : 0f;
        float apexY = baseline + arcTopRaise;
        float radius = Mathf.Max(1f, arcRadius);
        float centerY = apexY - radius;
        float x = (index - half) * slotStep;
        float radial = Mathf.Sqrt(Mathf.Max(0f, radius * radius - x * x));
        float y = centerY + radial;
        float arcFactor = 1f - Mathf.Abs(x) / Mathf.Max(0.001f, halfWidth);
        anchoredPosition = new Vector2(x, y);
        rotationDeg = Mathf.Atan2(-x, Mathf.Max(0.0001f, radial)) * Mathf.Rad2Deg;
        scale = Mathf.Lerp(edgeScale, 1f, arcFactor);
    }

    /// <summary>
    /// 新道具插入时，让现有占用槽沿 futureCount 个槽的圆弧移动（用于与飞入动画同步让位）。
    /// 现有槽保持原索引，新增槽位位于最右侧（索引 futureCount-1）。
    /// </summary>
    public void ArrangeExistingSlotsForCount(int futureCount, float duration)
    {
        RectTransform area = transform as RectTransform;
        if (area == null)
            return;

        DisableLegacyLayout();
        CollectActiveSlots(area);
        if (activeSlots.Count == 0)
            return;

        bool animate = Application.isPlaying && duration > 0f;
        float d = Mathf.Max(0.001f, duration);
        for (int i = 0; i < activeSlots.Count; i++)
        {
            RectTransform slot = activeSlots[i];
            if (slot == null)
                continue;

            ComputeSlotPose(futureCount, i, out Vector2 pos, out float rot, out float scale);
            slot.anchorMin = new Vector2(0.5f, 0.5f);
            slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            SyncJuicyMotionBase(slot, scale, rot);

            if (animate)
            {
                slot.DOKill(false);
                slot.DOAnchorPos(pos, d).SetEase(Ease.OutCubic).SetTarget(slot);
                slot.DOLocalRotate(new Vector3(0f, 0f, rot), d).SetEase(Ease.OutCubic).SetTarget(slot);
                slot.DOScale(new Vector3(scale, scale, 1f), d).SetEase(Ease.OutCubic).SetTarget(slot);
            }
            else
            {
                slot.DOKill(false);
                slot.anchoredPosition = pos;
                slot.localEulerAngles = new Vector3(0f, 0f, rot);
                slot.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }

    /// <summary>根据任意局部 X 计算其在圆弧上的姿态（拖拽时只喂 X，道具沿曲线移动）。</summary>
    public void ComputeCurvePoseAtX(int count, float x, out Vector2 anchoredPosition, out float rotationDeg, out float scale)
    {
        RectTransform area = transform as RectTransform;
        float half = (count - 1) * 0.5f;
        float halfWidth = half * slotStep;
        float baseline = area != null ? area.rect.yMin : 0f;
        float apexY = baseline + arcTopRaise;
        float radius = Mathf.Max(1f, arcRadius);
        float centerY = apexY - radius;
        float radial = Mathf.Sqrt(Mathf.Max(0f, radius * radius - x * x));
        float y = centerY + radial;
        float arcFactor = 1f - Mathf.Abs(x) / Mathf.Max(0.001f, halfWidth);
        anchoredPosition = new Vector2(x, y);
        rotationDeg = Mathf.Atan2(-x, Mathf.Max(0.0001f, radial)) * Mathf.Rad2Deg;
        scale = Mathf.Lerp(edgeScale, 1f, arcFactor);
    }

    public int GetSlotAtLocalX(float localX)
    {
        CollectActiveSlots(transform as RectTransform);
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < activeSlots.Count; i++)
        {
            RectTransform slot = activeSlots[i];
            if (slot == null)
                continue;

            float distance = Mathf.Abs(slot.anchoredPosition.x - localX);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        layoutDirty = true;
        if (isActiveAndEnabled)
            RefreshLayout();
    }

    private static void SyncJuicyMotionBase(RectTransform slot, float scale, float rotationDeg)
    {
        JuicyMotion motion = slot != null ? slot.GetComponent<JuicyMotion>() : null;
        if (motion != null)
            motion.SetBaseTransform(new Vector3(scale, scale, 1f), new Vector3(0f, 0f, rotationDeg), false);
    }

    private void DisableLegacyLayout()
    {
        GridLayoutGroup grid = GetComponent<GridLayoutGroup>();
        if (grid != null && grid.enabled)
            grid.enabled = false;

        // AutoSpacing 的 Awake 会补充 MagicBookBackgroundLayer 背景层；
        // 只在运行时关闭它，避免编辑态保存后丢失背景层。
        if (Application.isPlaying)
        {
            MagicBookAutoSpacing autoSpacing = GetComponent<MagicBookAutoSpacing>();
            if (autoSpacing != null && autoSpacing.enabled)
                autoSpacing.enabled = false;
        }
    }

    private void CollectActiveSlots(RectTransform area)
    {
        activeSlots.Clear();
        if (area == null)
            return;

        for (int i = 0; i < area.childCount; i++)
        {
            RectTransform child = area.GetChild(i) as RectTransform;
            if (child != null && child.gameObject.activeSelf && child.GetComponent<MagicItemView>() != null)
                activeSlots.Add(child);
        }
    }
}
