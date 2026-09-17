using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum TutorialStep
{
    None,
    BattleInfo,
    BattleArrowBase,
    BattleMagicBook,
    BattlePlayLimit,
    BattleEnemyInfo,
    BattlePlay,
    BattleRefresh,
    MapPanel,
    MapMovement,
    MapStepLimit,
    RewardClaim,
    ShopBuyHint,
    EventOptions,
    Completed
}

public class TutorialManagerUI : MonoBehaviour
{
    public const int TutorialChapterNumericId = 100;
    public const int TutorialBattleLevelId = 1001;
    public const int TutorialEventLevelId = 1002;
    public const int TutorialRestLevelId = 201;
    public const int TutorialBossLevelId = 1005;
    public const int TutorialEventNumericId = 1001;

    /// <summary>教程开局固定金币（不走进阶金币倍率，也不影响普通新局）。</summary>
    public const int TutorialStartGold = 10;

    [SerializeField] private RectTransform stepsRoot;
    [SerializeField] private TutorialCutoutMaskUI cutoutMask;
    [SerializeField] private TutorialInputBlockerUI inputBlocker;

    private HandSystemUI owner;
    private readonly Dictionary<TutorialStep, GameObject> stepObjects = new Dictionary<TutorialStep, GameObject>();
    private TutorialStep currentStep;
    private bool mainTutorialRunning;
    private bool tutorialBattleRunning;
    private bool waitingForStepClick;
    private int battleTurnIndex;
    private bool tutorialBattleInputUnlocked;
    private readonly List<RectTransform> cutoutTargets = new List<RectTransform>();

    /// <summary>页码的完整本地化正文（按步骤缓存，供段落拆分使用）。</summary>
    private readonly Dictionary<TutorialStep, string> stepBodies = new Dictionary<TutorialStep, string>();

    /// <summary>当前步骤的段落列表（不拆分的步骤只有 1 段）。</summary>
    private readonly List<string> stepParagraphs = new List<string>();

    /// <summary>正文段落分隔符（文案表里用空行分段）。</summary>
    private const string ParagraphSeparator = "\n\n";

    private int stepParagraphIndex;

    /// <summary>
    /// 当前步骤在看完最后一段后是否还需“点击推进”（阅读页 true，行为页 false）。
    /// 拆分步骤的中间段落总是靠点击推进。
    /// </summary>
    private bool stepAdvanceByClick;

    /// <summary>
    /// 每步只使用一个高亮框：页面下名为 Cutout 的子对象。
    /// 历史上 Battle_Play/Battle_Refresh/Event_Refresh 曾附带 Cutout2/Cutout3 作为第二/第三洞，
    /// 现在统一收敛为单框（多余子对象已在场景内停用）。需要换高亮区域时改 Cutout 的矩形即可。
    /// </summary>
    private const string CutoutChildName = "Cutout";

    /// <summary>移动端战斗场景名（与 SceneTransitionManager.peGameSceneName 保持一致）。</summary>
    private const string MobileSceneName = "SampleScene_PE";
    private bool mapTutorialShown;
    private bool shopTutorialShown;
    private bool tutorialCompleted;
    private bool tutorialEventShown;
    private bool consumedStepClickThisFrame;

    public TutorialStep CurrentStep => currentStep;
    public bool MainTutorialRunning => mainTutorialRunning;
    public bool TutorialBattleRunning => tutorialBattleRunning;
    public bool ShouldKillTutorialEnemyAfterPlayerTurn => tutorialBattleRunning && battleTurnIndex >= 2;
    public bool IsCompleted => tutorialCompleted;
    public bool IsTutorialRun { get; private set; }
    public bool IsMapTutorialBlockingInput => currentStep == TutorialStep.MapPanel || currentStep == TutorialStep.MapMovement || currentStep == TutorialStep.MapStepLimit;

    /// <summary>当前步骤是否允许“换牌”快捷键（R）。</summary>
    public bool AllowsRefreshShortcut => StepAllowsShortcut(TutorialStep.BattleRefresh);

    /// <summary>当前步骤是否允许“换牌”按钮：换牌页或教学战斗已解锁时为 true。
    /// 出牌页（`Battle_Play`）会临时禁用换牌按钮。</summary>
    public bool AllowsRefreshButton => StepAllowsShortcut(TutorialStep.BattleRefresh);

    /// <summary>当前步骤是否允许“出手”按钮：出牌页或教学战斗已解锁时为 true。
    /// 换牌页（`Battle_Refresh`）会临时禁用出手按钮，直到点过换牌按钮。</summary>
    public bool AllowsEndTurnButton => StepAllowsShortcut(TutorialStep.BattlePlay);

    /// <summary>当前步骤是否允许“撤回”快捷键（R / Backspace）。
    /// 教程已不再专门教学撤回，所以只要求“当前没有正在显示的教程页”（阅读页仍会整屏拦截）。</summary>
    public bool AllowsUndoShortcut => StepAllowsShortcut(TutorialStep.None);

    /// <summary>步骤切换通知（显示新页 / 收起所有页），供业务 UI 同步按钮可用性等派生状态。</summary>
    public event System.Action StepChanged;

    /// <summary>无活动步骤、教学已解锁输入、或正处于允许该操作的页面时为 true。</summary>
    private bool StepAllowsShortcut(TutorialStep step)
    {
        // 段落阅读中（拆分步骤的中间段落）不允许任何快捷键/按钮。
        if (waitingForStepClick)
            return false;

        if (currentStep == TutorialStep.None || tutorialBattleInputUnlocked)
            return true;

        return currentStep == step;
    }

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        CacheSteps();
        CacheCutoutMask();
        CacheInputBlocker();
        LocalizeSteps();
        HideAllSteps();
        SetMapTutorialInputLocked(false);
        IsTutorialRun = owner != null && owner.ActiveChapterNumericId == TutorialChapterNumericId;
        mainTutorialRunning = IsTutorialRun;
    }

    private void Update()
    {
        consumedStepClickThisFrame = false;
        if (Input.GetMouseButtonDown(0))
            AdvanceStepByClick();
    }

    public void OnLevelSelectShown(int nodeIndex)
    {
        if (!mainTutorialRunning || mapTutorialShown || nodeIndex != 0)
            return;

        mapTutorialShown = true;
        ShowStep(TutorialStep.MapPanel, true);
    }

    public bool ShouldUseTutorialBattle(LevelData level)
    {
        return IsTutorialRun && level != null && level.numericId == TutorialBattleLevelId;
    }

    public void BeginTutorialBattle()
    {
        tutorialBattleRunning = true;
        battleTurnIndex = 0;
        tutorialBattleInputUnlocked = false;
        HideAllSteps();
    }

    public void EndTutorialBattle()
    {
        tutorialBattleRunning = false;
        tutorialBattleInputUnlocked = false;
        battleTurnIndex = 0;
        if (currentStep == TutorialStep.BattleInfo || currentStep == TutorialStep.BattleMagicBook || currentStep == TutorialStep.BattleEnemyInfo || currentStep == TutorialStep.BattlePlay || currentStep == TutorialStep.BattleRefresh)
            HideAllSteps();
    }

    public bool TryApplyFixedTurnHand(PlayerState playerState)
    {
        if (!tutorialBattleRunning || playerState == null)
            return false;

        battleTurnIndex++;
        tutorialBattleInputUnlocked = false;
        switch (battleTurnIndex)
        {
            case 1:
                SetFixedHand(playerState, MaterialEnum.Fire, MaterialEnum.Fire);
                ShowStep(TutorialStep.BattleInfo, true);
                return true;
            case 2:
                SetFixedHand(playerState, MaterialEnum.Earth, MaterialEnum.Earth, MaterialEnum.Earth);
                ShowStep(TutorialStep.BattleRefresh, false);
                return true;
        }

        return false;
    }

    public bool CanMoveCardToPlay(MaterialModel card, IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning || tutorialBattleInputUnlocked)
            return true;
        if (waitingForStepClick)
            return false;

        switch (currentStep)
        {
            case TutorialStep.BattlePlay:
                // 教学不限制张数，放哪张都行。
                return card != null;
            case TutorialStep.BattleRefresh:
                // 换牌页同样不限制：玩家可自行决定放入几张再去点换牌按钮。
                return card != null;
            default:
                return false;
        }
    }

    public bool CanMovePlayCardToHand(MaterialModel card)
    {
        if (!tutorialBattleRunning || tutorialBattleInputUnlocked)
            return true;
        if (waitingForStepClick)
            return false;

        if (currentStep == TutorialStep.BattlePlay || currentStep == TutorialStep.BattleRefresh)
            return card != null;

        return false;
    }

    public bool CanReplacePlayZone(int playZoneCount)
    {
        if (!tutorialBattleRunning || tutorialBattleInputUnlocked)
            return true;
        if (waitingForStepClick)
            return false;

        // 只要求出牌区里有箭头；不限张数。
        return currentStep == TutorialStep.BattleRefresh && playZoneCount > 0;
    }

    /// <summary>
    /// 换牌页的强制换牌结果：按玩家放进出牌区的张数，依次取「上 上 下」序列，不足循环补齐，
    /// 保证“放 3 张 → 上 上 下”的演示不变，同时支持 1–任意张。
    /// </summary>
    public bool TryGetForcedRefreshMaterialsForPlayZone(int playZoneCount, List<MaterialEnum> materials)
    {
        if (materials == null)
            return false;

        materials.Clear();
        if (!tutorialBattleRunning || currentStep != TutorialStep.BattleRefresh || playZoneCount <= 0)
            return false;

        MaterialEnum[] scripted = { MaterialEnum.Fire, MaterialEnum.Fire, MaterialEnum.Water };
        for (int i = 0; i < playZoneCount; i++)
            materials.Add(scripted[i % scripted.Length]);
        return true;
    }

    public void OnRefreshCompleted(PlayerState playerState)
    {
        if (tutorialBattleRunning && currentStep == TutorialStep.BattleRefresh)
        {
            tutorialBattleInputUnlocked = true;
            HideAllSteps();
        }
    }

    public bool CanEndTurn(IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning || tutorialBattleInputUnlocked)
            return true;
        if (waitingForStepClick)
            return false;

        return currentStep == TutorialStep.BattlePlay;
    }

    public void OnBattleCardsPlayed(IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning)
            return;
    }

    public void OnBattleCardCanceled(IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning || currentStep != TutorialStep.BattlePlay)
            return;
    }

    public void OnBattleReadyToEndTurn(IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning || currentStep != TutorialStep.BattlePlay)
            return;
    }

    public void OnBattleEndTurnStarted()
    {
        if (!tutorialBattleRunning || currentStep != TutorialStep.BattlePlay)
            return;

        HideAllSteps();
    }

    public bool CanToggleFocus(EnemyModel enemy)
    {
        return true;
    }

    public void OnFocusTargetChanged(EnemyModel enemy)
    {
    }

    public void OnRewardPanelShown()
    {
        if (mainTutorialRunning && !tutorialCompleted)
            ShowStep(TutorialStep.RewardClaim, true);
    }

    /// <summary>选中奖励道具（钩子保留作语义显式：道具现在会自动进入第一位空槽）。</summary>
    public void OnRewardMagicSelected()
    {
    }

    public void CompleteTutorial(PlayerState playerState, IReadOnlyList<RunMapNodeModel> mapNodes, int currentMapNodeIndex, ChapterData chapter, LevelData currentLevel)
    {
        if (!IsTutorialRun)
            return;

        tutorialCompleted = true;
        mainTutorialRunning = false;
        tutorialBattleRunning = false;
        ShowStep(TutorialStep.Completed, false);
        HideAllSteps();
    }

    public void OnEventOptionsShown()
    {
        if (IsTutorialRun && !tutorialEventShown)
            ShowStep(TutorialStep.EventOptions, true);
    }

    public void OnEventOptionResolved()
    {
        if (currentStep != TutorialStep.EventOptions)
            return;

        tutorialEventShown = true;
        HideAllSteps();
    }

    public bool ShouldUseTutorialEventFixedDraw(EventData eventData)
    {
        return IsTutorialRun && eventData != null && eventData.numericId == TutorialEventNumericId;
    }

    /// <summary>商店面板显示时调用。教程只在**首次**打开商店时给一页购买提示（战后自动商店）。</summary>
    public void OnShopPanelShown()
    {
        if (!IsTutorialRun || shopTutorialShown)
            return;

        shopTutorialShown = true;
        ShowStep(TutorialStep.ShopBuyHint, true);
    }

    public bool ConsumeBlockingTutorialClick(PointerEventData eventData)
    {
        if (!waitingForStepClick)
            return false;
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return false;

        AdvanceStepByClick();
        consumedStepClickThisFrame = true;
        return true;
    }

    public bool WasTutorialClickConsumedThisFrame()
    {
        return consumedStepClickThisFrame;
    }

    private void AdvanceStepByClick()
    {
        if (!waitingForStepClick)
            return;

        // 拆分步骤：先逐段展示同一下面的正文，高亮框与页面对象保持不动，只换文本框。
        if (stepParagraphIndex + 1 < stepParagraphs.Count)
        {
            stepParagraphIndex++;
            ApplyParagraph();
            return;
        }

        switch (currentStep)
        {
            // 教学战斗阅读页顺序：信息 → 敌人意图 → 手牌/出牌区与四方向 → 道具栏 → 出牌限制 → 出牌（行为）
            case TutorialStep.BattleInfo:
                ShowStep(TutorialStep.BattleEnemyInfo, true);
                break;
            case TutorialStep.BattleEnemyInfo:
                ShowStep(TutorialStep.BattleArrowBase, true);
                break;
            case TutorialStep.BattleArrowBase:
                ShowStep(TutorialStep.BattleMagicBook, true);
                break;
            case TutorialStep.BattleMagicBook:
                ShowStep(TutorialStep.BattlePlayLimit, true);
                break;
            case TutorialStep.BattlePlayLimit:
                ShowStep(TutorialStep.BattlePlay, false);
                break;
            case TutorialStep.MapPanel:
                ShowStep(TutorialStep.MapMovement, true);
                break;
            case TutorialStep.MapMovement:
                ShowStep(TutorialStep.MapStepLimit, true);
                break;
            case TutorialStep.MapStepLimit:
                HideAllSteps();
                break;
            default:
                HideAllSteps();
                break;
        }
    }

    private void SetFixedHand(PlayerState playerState, params MaterialEnum[] materials)
    {
        if (playerState == null)
            return;

        List<MaterialModel> removedTemporaryCards = new List<MaterialModel>();
        playerState.ReturnHandCardsToDiscardPile(new List<MaterialModel>(playerState.Hand), removedTemporaryCards);
        playerState.ReturnPlayZoneCardsToDiscardPile(removedTemporaryCards);
        playerState.DrawSpecificMaterialsToHand(materials, true);
    }

    private void CacheSteps()
    {
        stepObjects.Clear();
        RectTransform root = stepsRoot != null ? stepsRoot : transform.Find("Steps") as RectTransform;
        if (root == null)
            root = transform as RectTransform;

        AddStep(root, TutorialStep.BattleInfo, "Battle_Info");
        AddStep(root, TutorialStep.BattleEnemyInfo, "Battle_EnemyInfo");
        AddStep(root, TutorialStep.BattleArrowBase, "Battle_ArrowBase");
        AddStep(root, TutorialStep.BattleMagicBook, "Battle_MagicBook");
        AddStep(root, TutorialStep.BattlePlayLimit, "Battle_PlayLimit");
        AddStep(root, TutorialStep.BattlePlay, "Battle_Play");
        AddStep(root, TutorialStep.BattleRefresh, "Battle_Refresh");
        AddStep(root, TutorialStep.MapPanel, "Map_Panel");
        AddStep(root, TutorialStep.MapMovement, "Map_Movement");
        AddStep(root, TutorialStep.MapStepLimit, "Map_StepLimit");
        AddStep(root, TutorialStep.RewardClaim, "Reward_Claim");
        AddStep(root, TutorialStep.ShopBuyHint, "Shop_BuyHint");
        AddStep(root, TutorialStep.EventOptions, "Event_Options");
        // 已从流程中删除的页面（旧 20 页版）：Battle_Combo / Battle_Cancel / Reward_UndoHint /
        // Shop_OrderHint / Shop_UndoHint / Event_Refresh / Rest_Options —— 场景对象已删，
        // 需要恢复时从 git 取回旧场景对象名（与上述名字一致）并在此处重新登记即可。
    }

    private void AddStep(RectTransform root, TutorialStep step, string objectName)
    {
        Transform child = root != null ? root.Find(objectName) : null;
        if (child != null)
            stepObjects[step] = child.gameObject;
    }

    private void LocalizeSteps()
    {
        foreach (KeyValuePair<TutorialStep, GameObject> pair in stepObjects)
        {
            SetStepText(pair.Value, "Title", GetStepTitleKey(pair.Key));
            SetStepText(pair.Value, "Body", GetStepBodyKey(pair.Key));

            // 缓存完整正文：段落拆分时反复覆写 Body，需要保留原文。
            TMP_Text body = FindStepText(pair.Value.transform, "Body");
            if (body != null)
                stepBodies[pair.Key] = body.text;
        }
    }

    /// <summary>
    /// 评审要求：除前 5 步（地图 3 页 + 玩家信息 + 敌人信息）外，每个 > 段落 = 一次点击推进。
    /// 这里声明的步骤会按空行拆段、逐段展示（高亮框不动）。
    /// </summary>
    private static bool StepSplitsParagraphs(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.BattleArrowBase:
            case TutorialStep.BattleMagicBook:
            case TutorialStep.BattlePlayLimit:
            case TutorialStep.BattleRefresh:
            case TutorialStep.EventOptions:
                return true;
            default:
                return false;
        }
    }

    /// <summary>准备当前步骤的段落列表（每次展示页面时重新按完整正文拆分）。</summary>
    private void PrepareParagraphs(TutorialStep step)
    {
        stepParagraphs.Clear();
        stepParagraphIndex = 0;

        GameObject stepObject;
        if (!stepObjects.TryGetValue(step, out stepObject) || stepObject == null)
            return;

        string fullBody;
        if (!stepBodies.TryGetValue(step, out fullBody) || string.IsNullOrEmpty(fullBody))
        {
            TMP_Text body = FindStepText(stepObject.transform, "Body");
            fullBody = body != null ? body.text : string.Empty;
        }

        if (!StepSplitsParagraphs(step))
        {
            stepParagraphs.Add(fullBody);
            return;
        }

        string[] parts = fullBody.Split(new[] { ParagraphSeparator }, System.StringSplitOptions.None);
        for (int i = 0; i < parts.Length; i++)
        {
            string paragraph = parts[i].Trim();
            if (paragraph.Length > 0)
                stepParagraphs.Add(paragraph);
        }

        if (stepParagraphs.Count == 0)
            stepParagraphs.Add(fullBody);
    }

    /// <summary>
    /// 把当前段落写进文本框，并根据“是否还有下一段”重算点击推进与输入拦截。
    /// 拆分步骤的中间段落：点击推进；最后一段：阅读页继续点击推进，行为页则等玩家完成操作。
    /// </summary>
    private void ApplyParagraph()
    {
        string text = stepParagraphs.Count > 0 ? stepParagraphs[Mathf.Clamp(stepParagraphIndex, 0, stepParagraphs.Count - 1)] : string.Empty;
        SetStepBodyText(currentStep, text);

        bool lastParagraph = stepParagraphIndex >= stepParagraphs.Count - 1;
        waitingForStepClick = stepParagraphs.Count > 1 ? (!lastParagraph || stepAdvanceByClick) : stepAdvanceByClick;

        UpdateCutoutTarget(currentStep, stepParagraphIndex);
        UpdateInputBlocker(waitingForStepClick);
        SetMapTutorialInputLocked(IsMapTutorialBlockingInput);
        StepChanged?.Invoke();
    }

    private void SetStepBodyText(TutorialStep step, string text)
    {
        GameObject stepObject;
        if (!stepObjects.TryGetValue(step, out stepObject) || stepObject == null)
            return;

        TMP_Text body = FindStepText(stepObject.transform, "Body");
        if (body != null)
            body.text = text;
    }

    private void SetStepText(GameObject stepObject, string childName, string key)
    {
        if (stepObject == null || string.IsNullOrEmpty(key))
            return;

        TMP_Text text = FindStepText(stepObject.transform, childName);
        if (text != null)
            text.text = LocalizationSystem.GetText(ResolveStepKey(key), LocalizationSystem.GetText(key, text.text));
    }

    /// <summary>
    /// 步骤文案的本地化 key：移动端（PE）优先用 <c>&lt;key&gt;_pe</c>，缺失时回退到两端共用的文案。
    /// 背景：教程文案里的方位词描述的是 PC 排布（例如换牌的“更换”按钮 PC 在左下、PE 在右下），
    /// 而本地化表两端共用，因此 PE 的差异文案单独挂一条 `_pe` key。
    /// </summary>
    private string ResolveStepKey(string key)
    {
        return IsMobileTutorialContext() ? key + "_pe" : key;
    }

    /// <summary>
    /// 当前教程是否跑在移动端（PE）排布下：有常驻 SceneTransitionManager 时问它；
    /// 编辑器里单独 Play PE 场景时没有管理器，退回按场景名/平台判断。
    /// </summary>
    private bool IsMobileTutorialContext()
    {
        if (SceneTransitionManager.Instance != null)
            return SceneTransitionManager.Instance.ShouldUseMobileScene();

        return Application.isMobilePlatform || gameObject.scene.name == MobileSceneName;
    }

    private TMP_Text FindStepText(Transform root, string name)
    {
        if (root == null)
            return null;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && text.name == name)
                return text;
        }
        return null;
    }

    private string GetStepTitleKey(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.BattleInfo: return "tutorial.battle.info.title";
            case TutorialStep.BattleArrowBase: return "tutorial.battle.arrow_base.title";
            case TutorialStep.BattleMagicBook: return "tutorial.battle.magic_book.title";
            case TutorialStep.BattlePlayLimit: return "tutorial.battle.play_limit.title";
            case TutorialStep.BattleEnemyInfo: return "tutorial.battle.enemy_info.title";
            case TutorialStep.BattlePlay: return "tutorial.battle.play.title";
            case TutorialStep.BattleRefresh: return "tutorial.battle.refresh.title";
            case TutorialStep.MapPanel: return "tutorial.map.panel.title";
            case TutorialStep.MapMovement: return "tutorial.map.movement.title";
            case TutorialStep.MapStepLimit: return "tutorial.map.step_limit.title";
            case TutorialStep.RewardClaim: return "tutorial.reward.claim.title";
            case TutorialStep.ShopBuyHint: return "tutorial.shop.buy_hint.title";
            case TutorialStep.EventOptions: return "tutorial.event.options.title";
            default: return string.Empty;
        }
    }

    private string GetStepBodyKey(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.BattleInfo: return "tutorial.battle.info.body";
            case TutorialStep.BattleArrowBase: return "tutorial.battle.arrow_base.body";
            case TutorialStep.BattleMagicBook: return "tutorial.battle.magic_book.body";
            case TutorialStep.BattlePlayLimit: return "tutorial.battle.play_limit.body";
            case TutorialStep.BattleEnemyInfo: return "tutorial.battle.enemy_info.body";
            case TutorialStep.BattlePlay: return "tutorial.battle.play.body";
            case TutorialStep.BattleRefresh: return "tutorial.battle.refresh.body";
            case TutorialStep.MapPanel: return "tutorial.map.panel.body";
            case TutorialStep.MapMovement: return "tutorial.map.movement.body";
            case TutorialStep.MapStepLimit: return "tutorial.map.step_limit.body";
            case TutorialStep.RewardClaim: return "tutorial.reward.claim.body";
            case TutorialStep.ShopBuyHint: return "tutorial.shop.buy_hint.body";
            case TutorialStep.EventOptions: return "tutorial.event.options.body";
            default: return string.Empty;
        }
    }

    private void ShowStep(TutorialStep step, bool waitForClick)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        currentStep = step;
        stepAdvanceByClick = waitForClick;
        PrepareParagraphs(step);
        foreach (KeyValuePair<TutorialStep, GameObject> pair in stepObjects)
        {
            bool active = pair.Key == step;
            pair.Value.SetActive(active);
            SetStepRaycastTarget(pair.Value, active && waitForClick);
        }
        UpdateCutoutTarget(step, 0);
        ApplyParagraph();
    }

    /// <summary>
    /// 输入拦截只在「点击推进」的阅读页生效（整屏挡，点哪里都只推进教程）。
    /// 行为页（出牌/换牌/事件换牌）不拦截：这些页面玩家必须同时操作多个区域
    /// （手牌 + 出牌区 + 出手按钮），单框遮挡会挡住必要操作；
    /// 行为页的限制由教程白名单负责（CanMoveCardToPlay / CanMovePlayCardToHand / CanEndTurn /
    /// CanReplacePlayZone / AllowsRefreshShortcut / AllowsUndoShortcut）。
    /// </summary>
    private void UpdateInputBlocker(bool waitForClick)
    {
        if (inputBlocker == null)
            return;

        inputBlocker.BlockWholeScreen = true;
        inputBlocker.SetTargetList(cutoutTargets);
        inputBlocker.RefreshFromConfig();
        SetInputBlockerActive(!tutorialBattleInputUnlocked && waitForClick);
    }

    private void SetStepRaycastTarget(GameObject stepObject, bool raycastTarget)
    {
        if (stepObject == null)
            return;

        Graphic[] graphics = stepObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = raycastTarget;
    }

    private void HideAllSteps()
    {
        bool wasBlockingMapInput = IsMapTutorialBlockingInput;
        currentStep = TutorialStep.None;
        waitingForStepClick = false;
        stepParagraphs.Clear();
        stepParagraphIndex = 0;
        stepAdvanceByClick = false;
        foreach (KeyValuePair<TutorialStep, GameObject> pair in stepObjects)
            pair.Value.SetActive(false);
        if (cutoutMask != null)
            cutoutMask.gameObject.SetActive(false);
        SetInputBlockerActive(false);
        if (wasBlockingMapInput)
            SetMapTutorialInputLocked(false);
        StepChanged?.Invoke();
    }

    private void SetMapTutorialInputLocked(bool locked)
    {
        if (owner == null)
            return;

        owner.GetUIManager().ChapterGridPanel?.SetInputLocked(locked);
    }

    private void CacheCutoutMask()
    {
        if (cutoutMask == null)
            cutoutMask = GetComponentInChildren<TutorialCutoutMaskUI>(true);
    }

    private void CacheInputBlocker()
    {
        if (inputBlocker == null)
        {
            Transform existing = transform.Find("InputBlocker");
            if (existing != null)
                inputBlocker = existing.GetComponent<TutorialInputBlockerUI>();
        }

        if (inputBlocker == null)
        {
            // 场景里没有时的运行时兵底（正式结构应直接搭在 TutorialRoot/InputBlocker）。
            GameObject blocker = new GameObject("InputBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialInputBlockerUI));
            blocker.transform.SetParent(transform, false);
            RectTransform rectTransform = blocker.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            inputBlocker = blocker.GetComponent<TutorialInputBlockerUI>();
        }

        inputBlocker.raycastTarget = true;
        inputBlocker.transform.SetAsFirstSibling();
        inputBlocker.gameObject.SetActive(false);
    }

    private void SetInputBlockerActive(bool active)
    {
        if (inputBlocker == null)
            return;

        inputBlocker.gameObject.SetActive(active);
        if (active)
            inputBlocker.transform.SetAsFirstSibling();
    }

    /// <summary>
    /// 每段一个高亮框：第 1 段用子对象 `Cutout`，第 2 段 `Cutout2`，第 3 段 `Cutout3`…
    /// 对应名字的子对象不存在或未启用时回退到 `Cutout`，所以单框页面不用改场景。
    /// 切段时只换洞目标，高亮框沿用该段自己的矩形。
    /// </summary>
    private void UpdateCutoutTarget(TutorialStep step, int paragraphIndex)
    {
        cutoutTargets.Clear();
        RectTransform cutout = GetStepParagraphCutoutTarget(step, paragraphIndex);
        if (cutout != null)
            cutoutTargets.Add(cutout);

        if (cutoutMask == null)
            return;

        cutoutMask.gameObject.SetActive(true);
        cutoutMask.SetTargetList(cutoutTargets);
        cutoutMask.transform.SetAsFirstSibling();
    }

    /// <summary>取某一段的高亮目标：优先 Cutout(N)（N = 段落序号 + 1），否则回退 Cutout。</summary>
    private RectTransform GetStepParagraphCutoutTarget(TutorialStep step, int paragraphIndex)
    {
        if (paragraphIndex > 0)
        {
            RectTransform named = GetStepCutoutTarget(step, CutoutChildName + (paragraphIndex + 1));
            if (named != null && named.gameObject.activeSelf)
                return named;
        }

        return GetStepCutoutTarget(step, CutoutChildName);
    }

    private RectTransform GetStepCutoutTarget(TutorialStep step, string childName)
    {
        GameObject stepObject;
        if (!stepObjects.TryGetValue(step, out stepObject) || stepObject == null)
            return null;

        Transform child = stepObject.transform.Find(childName);
        return child != null ? child.GetComponent<RectTransform>() : null;
    }
}

