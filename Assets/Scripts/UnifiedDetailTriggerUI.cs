using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 详情面板（<see cref="UnifiedDetailPopupUI"/>）的通用交互组件：挂到任意可交互物体上，它就负责“显示/收起详情”，
/// 不必再在各业务脚本里手写 OnPointerEnter/OnPointerExit。
///
/// 交互规则：
/// - PC / 非移动端：指针进入显示详情、离开隐藏（原有 hover 语义），点击流程完全不受本组件影响。
/// - PE / 移动端（<see cref="ShouldUseMobileInteraction"/>）：长按超过阈值显示详情并保持，松手不再触发点击动作
///   （在 OnPointerUp 里清掉 <c>PointerEventData.eligibleForClick</c>，因此 Button.onClick 与业务点击都不会响）；
///   未达阈值的短按按 <see cref="ShortPressBehavior"/> 处理（让点击照常触发 / 直接看详情 / 调用绑定动作）。
/// - 按压期间指针位移超过 slop 视为滑动或拖拽，取消本次按压；本组件不实现拖拽接口，所以不会抢 ScrollRect 的滚动。
/// - 已固定（Pin）的详情面板展开时，第一下点击只用于“关详情”，不会顺带触发被点物体的动作。
///
/// 内容来源两种：Inspector 里填的本地化文案（<see cref="ContentMode.LocalizedText"/>，挂上即用），或由业务代码注入
/// <see cref="SetContentProvider"/>（<see cref="ContentMode.Provider"/>，用于内容随运行期状态变化的站点）。
/// </summary>
public class UnifiedDetailTriggerUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public enum ContentMode
    {
        /// <summary>用面板上序列化的标题/正文/图标。</summary>
        LocalizedText = 0,
        /// <summary>用 <see cref="SetContentProvider"/> 注入的内容（业务代码按当前状态构建）。</summary>
        Provider = 1
    }

    public enum IconSource
    {
        None = 0,
        /// <summary>自动取自身或子物体上第一个有效 Sprite（SpriteRenderer / Image）。</summary>
        SelfGraphic = 1,
        /// <summary>用下面拖入的 Sprite。</summary>
        Sprite = 2,
        /// <summary>用 Resources/Images/Events/&lt;iconName&gt;。</summary>
        EventIconName = 3
    }

    /// <summary>PE 短按（未达长按阈值）的行为。</summary>
    public enum ShortPressBehavior
    {
        /// <summary>不干预：让原有点击逻辑（Button.onClick 或业务自己的 IPointerClickHandler）照常触发。</summary>
        ClickThrough = 0,
        /// <summary>本组件直接显示详情（用于没有动作的说明图标：短按=看详情）。</summary>
        ShowDetail = 1,
        /// <summary>调用 <see cref="SetAction"/> 绑定的动作，并吃掉底层点击（避免与 Button.onClick 重复触发）。</summary>
        InvokeAction = 2
    }

    [Header("内容")]
    [SerializeField] private ContentMode contentMode = ContentMode.LocalizedText;
    [SerializeField] private UnifiedDetailSourceType sourceType = UnifiedDetailSourceType.None;
    [Tooltip("标题/正文的本地化 key；body 支持 <icon>/<attack> 等内联富文本。")]
    [SerializeField] private string titleKey;
    [SerializeField] private string titleFallback;
    [SerializeField] private string bodyKey;
    [SerializeField] private string bodyFallback;
    [SerializeField] private IconSource iconSource = IconSource.SelfGraphic;
    [SerializeField] private Sprite iconSprite;
    [Tooltip("图标名（只写图片名，不含路径与扩展名），运行时拼 Resources/Images/Events/ 加载。")]
    [SerializeField] private string iconName;
    [Tooltip("勾选后由本组件指定面板边框强调色；取消则原样沿用美术在场景里给面板设的颜色（代码不写颜色）。")]
    [SerializeField] private bool overrideAccentColor;
    [SerializeField] private Color accentColor = Color.white;

    [Header("交互")]
    [Tooltip("PC：点一下把详情固定展开（等价于点击钉住）。PE 的短按由 短按行为 控制，不受此项影响。")]
    [SerializeField] private bool pinOnClick;
    [Tooltip("取消勾选则本物体不参与 PE 长短按区分（PC 悬停显示详情仍然有效）。")]
    [SerializeField] private bool enablePressGesture = true;
    [Tooltip("PE 短按行为：ClickThrough=放行原有点击；ShowDetail=短按看详情；InvokeAction=调用绑定动作。")]
    [SerializeField] private ShortPressBehavior shortPress = ShortPressBehavior.ClickThrough;

    private UIManager uiManager;
    private HandSystemUI handSystem;
    private Selectable selectable;
    private Func<UnifiedDetailContent> contentProvider;
    private Action clickAction;
    private Action<bool> hoverAction;
    private object anchorOverride;

    private bool pressing;
    private bool longPressFired;
    private bool dismissTapPressed;
    private float pressStartTime;
    private Vector2 pressStartPosition;

    private static BattleInputConfig cachedInputConfig;

    /// <summary>当前详情是否由本组件固定展开。</summary>
    public bool IsDetailPinned
    {
        get
        {
            UnifiedDetailPopupUI popup = GetPopup();
            return popup != null && popup.IsPinnedFor(GetAnchor());
        }
    }

    /// <summary>业务代码注入内容（内容随运行期状态变化的站点用这个）。</summary>
    public void SetContentProvider(Func<UnifiedDetailContent> provider)
    {
        contentProvider = provider;
        contentMode = ContentMode.Provider;
    }

    /// <summary>业务代码绑定“短按动作”（PE）；同时把短按行为切到 InvokeAction。</summary>
    public void SetAction(Action action, ShortPressBehavior behavior = ShortPressBehavior.InvokeAction)
    {
        clickAction = action;
        shortPress = behavior;
    }

    /// <summary>
    /// 绑定悬停状态回调（进入/离开，PE 触摸按下时同样会进入）。给“悬停除了弹详情还要高亮/改变样式”的站点用，
    /// 例如强化选项行：悬停要换边框色，而弹详情由本组件统一负责。
    /// </summary>
    public void SetHoverActions(Action<bool> onHoverChanged)
    {
        hoverAction = onHoverChanged;
    }

    /// <summary>用本地化 key 直接指定内容（业务代码在运行期注入文案时用，例如顶栏按钮的说明）。</summary>
    public void SetLocalizedContent(string newTitleKey, string newBodyKey, string newTitleFallback, string newBodyFallback)
    {
        contentMode = ContentMode.LocalizedText;
        titleKey = newTitleKey;
        bodyKey = newBodyKey;
        titleFallback = newTitleFallback;
        bodyFallback = newBodyFallback;
    }

    /// <summary>本物体当前是否走移动端交互（触摸手势）。</summary>
    public bool IsMobileInteraction => ShouldUseMobileInteraction();

    public void SetShortPressBehavior(ShortPressBehavior behavior)
    {
        shortPress = behavior;
    }

    /// <summary>
    /// 指定详情面板的归属 anchor（默认是本组件自身）。既有代码里按其它对象（例如按钮）作为 anchor 收起面板时，
    /// 用这个保持引用一致，避免迁移时改动业务侧。
    /// </summary>
    public void SetAnchor(object anchor)
    {
        anchorOverride = anchor;
    }

    /// <summary>外部要立刻收起本组件负责的详情（例如商店关闭、面板刷新）。</summary>
    public void HideDetailNow()
    {
        CancelPress();
        HideDetail();
    }

    /// <summary>外部要立刻显示详情（例如教程引导）。</summary>
    public void ShowDetailNow()
    {
        ShowDetail();
    }

    /// <summary>外部要立刻固定展开详情（例如点击奖励图标/敌人意图钉住它）。</summary>
    public void PinDetailNow()
    {
        ShowDetailAndKeep();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable())
            return;

        hoverAction?.Invoke(true);

        // PE 下进入不弹详情（触摸按下会立刻触发 enter，那时还分不清长短按）。
        if (ShouldUseMobileInteraction())
            return;

        ShowDetail();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hoverAction?.Invoke(false);

        if (ShouldUseMobileInteraction())
            return;

        HideDetail();
    }

    /// <summary>PC：点一下固定展开详情（沿用各站点原有的“点击钉住”行为）。</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!pinOnClick || !IsInteractable() || ShouldUseMobileInteraction())
            return;

        ShowDetailAndKeep();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || !enablePressGesture || !ShouldUseMobileInteraction() || !IsInteractable())
            return;

        pressing = true;
        longPressFired = false;
        pressStartTime = Time.unscaledTime;
        pressStartPosition = eventData.position;
        // 已固定展开的详情面板：这一下点击的用途是“关详情”，不该顺带触发被点物体的动作。
        dismissTapPressed = IsTapThatDismissesPinnedDetail(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressing)
            return;

        bool wasLongPress = longPressFired;
        pressing = false;
        longPressFired = false;

        if (wasLongPress)
        {
            // 长按=看详情：松手不触发点击动作（吃掉这一次 click）。
            SuppressClick(eventData);
            if (!KeepDetailAfterLongPress)
                HideDetail();
            return;
        }

        if (dismissTapPressed)
        {
            SuppressClick(eventData);
            dismissTapPressed = false;
            return;
        }

        switch (shortPress)
        {
            case ShortPressBehavior.ShowDetail:
                ShowDetailAndKeep();
                break;
            case ShortPressBehavior.InvokeAction:
                SuppressClick(eventData);
                HideDetail();
                clickAction?.Invoke();
                break;
            default:
                break;
        }
    }

    private void Update()
    {
        TickPress();
    }

    /// <summary>按压推进：到长按阈值就弹出详情；中途位移超 slop 则取消本次按压。</summary>
    private void TickPress()
    {
        if (!pressing || longPressFired)
            return;

        if (HasMovedBeyondSlop())
        {
            CancelPress();
            return;
        }

        if (Time.unscaledTime - pressStartTime < GetLongPressThreshold())
            return;

        longPressFired = true;
        ShowDetailAndKeep();
    }

    private void OnDisable()
    {
        CancelPress();
        HideDetail();
    }

    private void CancelPress()
    {
        pressing = false;
        longPressFired = false;
        dismissTapPressed = false;
    }

    private bool HasMovedBeyondSlop()
    {
        float slop = GetMoveSlop();
        if (slop <= 0f)
            return false;

        Vector2 current = GetCurrentPointerPosition();
        return (current - pressStartPosition).sqrMagnitude > slop * slop;
    }

    private static Vector2 GetCurrentPointerPosition()
    {
        if (Input.touchCount > 0)
            return Input.GetTouch(0).position;
        return Input.mousePosition;
    }

    /// <summary>
    /// 这一下点击是否正好用来收起“已固定”的详情：面板自身在 Update 里只对“面板外且锚点外”的按下收起，
    /// 所以这里用同样的判定；点在锚点自己身上时不算收起（那一下要留给业务动作，例如手牌出牌、增益槽开合）。
    /// </summary>
    private bool IsTapThatDismissesPinnedDetail(Vector2 screenPosition)
    {
        UnifiedDetailPopupUI popup = GetPopup();
        if (popup == null || !popup.IsPinned)
            return false;
        if (popup.ContainsScreenPoint(screenPosition))
            return false;

        return !IsScreenPointInsideAnchor(screenPosition);
    }

    private bool IsScreenPointInsideAnchor(Vector2 screenPosition)
    {
        RectTransform anchorRect = ResolveAnchorRectTransform();
        return anchorRect != null && RectTransformUtility.RectangleContainsScreenPoint(anchorRect, screenPosition, GetEventCamera());
    }

    private RectTransform ResolveAnchorRectTransform()
    {
        if (anchorOverride is RectTransform overrideRect)
            return overrideRect;
        if (anchorOverride is Component overrideComponent)
            return overrideComponent.transform as RectTransform;
        if (anchorOverride is GameObject overrideObject)
            return overrideObject.transform as RectTransform;
        return transform as RectTransform;
    }

    private Camera GetEventCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static void SuppressClick(PointerEventData eventData)
    {
        if (eventData != null)
            eventData.eligibleForClick = false;
    }

    /// <summary>PC 悬停：跟随式显示，离开即隐藏。</summary>
    private void ShowDetail()
    {
        UIManager ui = GetUIManager();
        if (ui == null)
            return;

        UnifiedDetailContent content = BuildContent();
        if (!HasVisibleContent(content))
            return;

        ui.ShowUnifiedDetailPopup(GetAnchor(), content);
    }

    /// <summary>PE 按压/短按：固定展开，点面板外收起。</summary>
    private void ShowDetailAndKeep()
    {
        UIManager ui = GetUIManager();
        if (ui == null)
            return;

        UnifiedDetailContent content = BuildContent();
        if (!HasVisibleContent(content))
            return;

        ui.PinUnifiedDetailPopup(GetAnchor(), content);
    }

    /// <summary>
    /// 没内容就不弹：内容由业务代码在运行期注入，注入之前（或当前状态确实没有详情时，例如空增益槽）
    /// 本组件保持静默，避免出现空白详情面板。
    /// </summary>
    private static bool HasVisibleContent(UnifiedDetailContent content)
    {
        return !string.IsNullOrEmpty(content.Title) || !string.IsNullOrEmpty(content.Body) || content.Icon != null;
    }

    private void HideDetail()
    {
        UIManager ui = GetUIManager();
        if (ui == null)
            return;

        UnifiedDetailPopupUI popup = ui.UnifiedDetailPopup;
        if (popup != null && popup.IsPinnedFor(GetAnchor()))
            popup.Unpin();
        else
            ui.HideUnifiedDetailPopup(GetAnchor());
    }

    private UnifiedDetailContent BuildContent()
    {
        UnifiedDetailContent content;
        if (contentMode == ContentMode.Provider && contentProvider != null)
        {
            content = contentProvider();
        }
        else
        {
            content = new UnifiedDetailContent
            {
                SourceType = sourceType,
                Title = LocalizationSystem.GetText(titleKey, titleFallback),
                Body = LocalizationSystem.GetText(bodyKey, bodyFallback),
                Icon = ResolveIcon()
            };
        }

        // 颜色一律不写：只给“没指定强调色”（alpha=0）的内容补上美术在面板上设的边框色；
        // 需要由代码指定时才用 Inspector 上的 accentColor。
        if (overrideAccentColor)
            content.AccentColor = accentColor;
        else if (content.AccentColor.a <= 0.001f)
            content.AccentColor = GetPanelDefaultAccentColor();

        return content;
    }

    private Color GetPanelDefaultAccentColor()
    {
        UnifiedDetailPopupUI popup = GetPopup();
        return popup != null ? popup.DefaultAccentColor : Color.white;
    }

    private Sprite ResolveIcon()
    {
        switch (iconSource)
        {
            case IconSource.Sprite:
                return iconSprite;
            case IconSource.EventIconName:
                return string.IsNullOrEmpty(iconName) ? null : Resources.Load<Sprite>("Images/Events/" + iconName);
            case IconSource.SelfGraphic:
                return FindSelfSprite();
            default:
                return null;
        }
    }

    /// <summary>自身/子物体上第一个有效 Sprite（按钮图标、顶栏图标这类直接挂载的场景）。</summary>
    private Sprite FindSelfSprite()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null && spriteRenderer.sprite != null)
            return spriteRenderer.sprite;

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].sprite != null)
                return images[i].sprite;
        }

        return null;
    }

    private bool IsInteractable()
    {
        if (selectable == null)
            selectable = GetComponent<Selectable>();
        return selectable == null || selectable.interactable;
    }

    private object GetAnchor()
    {
        return anchorOverride ?? (object)this;
    }

    private UIManager GetUIManager()
    {
        if (uiManager == null)
            uiManager = GetComponentInParent<UIManager>();
        return uiManager;
    }

    private UnifiedDetailPopupUI GetPopup()
    {
        UIManager ui = GetUIManager();
        return ui != null ? ui.UnifiedDetailPopup : null;
    }

    private bool ShouldUseMobileInteraction()
    {
        if (handSystem == null)
            handSystem = GetComponentInParent<HandSystemUI>();

        if (handSystem != null)
            return handSystem.ShouldUseMobileInteraction();

        return Application.isMobilePlatform;
    }

    private float GetLongPressThreshold()
    {
        return GetInputConfig().DetailLongPressThreshold;
    }

    private float GetMoveSlop()
    {
        return GetInputConfig().DetailPressMoveSlop;
    }

    private bool KeepDetailAfterLongPress => GetInputConfig().KeepDetailAfterLongPress;

    private static BattleInputConfig GetInputConfig()
    {
        if (cachedInputConfig == null)
        {
            cachedInputConfig = Resources.Load<BattleInputConfig>("Config/BattleInputConfig");
            if (cachedInputConfig == null)
                cachedInputConfig = ScriptableObject.CreateInstance<BattleInputConfig>();
        }

        return cachedInputConfig;
    }
}
