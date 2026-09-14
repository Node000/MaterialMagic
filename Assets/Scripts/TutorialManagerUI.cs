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
    BattleCombo,
    BattlePlayLimit,
    BattleEnemyInfo,
    BattleCancel,
    BattlePlay,
    BattleRefresh,
    MapPanel,
    MapMovement,
    MapStepLimit,
    RewardClaim,
    RewardUndoHint,
    ShopBuyHint,
    ShopOrderHint,
    ShopUndoHint,
    EventOptions,
    EventRefresh,
    RestOptions,
    Completed
}

public class TutorialManagerUI : MonoBehaviour
{
    public const int TutorialChapterNumericId = 100;
    public const int TutorialBattleLevelId = 1001;
    public const int TutorialEventLevelId = 1002;
    public const int TutorialShopLevelId = 1003;
    public const int TutorialRestLevelId = 201;
    public const int TutorialBossLevelId = 1005;
    public const int TutorialEventNumericId = 1001;
    public const int TutorialDummyLEnemyId = 1001;
    public const int TutorialDummyXXLEnemyId = 1002;

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
    private static readonly string[] CutoutChildNames = { "Cutout", "Cutout2", "Cutout3" };
    private bool mapTutorialShown;
    private bool shopUndoHintShown;
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

    /// <summary>当前步骤是否允许“右键/点击出牌区出牌”。</summary>
    public bool AllowsPlayShortcut => StepAllowsShortcut(TutorialStep.BattlePlay);

    /// <summary>当前步骤是否允许“撤回”快捷键（R / Backspace）。</summary>
    public bool AllowsUndoShortcut => StepAllowsShortcut(TutorialStep.RewardUndoHint) || StepAllowsShortcut(TutorialStep.ShopUndoHint);

    /// <summary>无活动步骤、教学已解锁输入、或正处于允许该操作的页面时为 true。</summary>
    private bool StepAllowsShortcut(TutorialStep step)
    {
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

    public bool ShouldForceFirstNodeBattles()
    {
        return false;
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
                return card != null && card.CanActAs(MaterialEnum.Earth) && CountMaterial(playZone, MaterialEnum.Earth) < 3;
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

        if (currentStep == TutorialStep.BattlePlay)
            return card != null;

        return currentStep == TutorialStep.BattleRefresh && card != null && card.CanActAs(MaterialEnum.Earth);
    }

    public bool CanReplacePlayZone(int playZoneCount)
    {
        if (!tutorialBattleRunning || tutorialBattleInputUnlocked)
            return true;
        if (waitingForStepClick)
            return false;

        return currentStep == TutorialStep.BattleRefresh && playZoneCount == 3;
    }

    public bool TryGetForcedRefreshMaterialsForPlayZone(int playZoneCount, List<MaterialEnum> materials)
    {
        if (materials == null)
            return false;

        materials.Clear();
        if (tutorialBattleRunning && currentStep == TutorialStep.BattleRefresh && playZoneCount == 3)
        {
            materials.Add(MaterialEnum.Fire);
            materials.Add(MaterialEnum.Fire);
            materials.Add(MaterialEnum.Water);
            return true;
        }

        if (IsTutorialRun && currentStep == TutorialStep.EventRefresh && playZoneCount > 0)
        {
            for (int i = 0; i < playZoneCount; i++)
                materials.Add(MaterialEnum.Earth);
            return true;
        }

        return false;
    }

    public bool CanRefreshSelected(IReadOnlyList<MaterialModel> selectedCards)
    {
        return tutorialBattleRunning && (tutorialBattleInputUnlocked || currentStep == TutorialStep.BattleRefresh && selectedCards != null && selectedCards.Count == 3);
    }

    public bool TryGetForcedRefreshMaterials(int selectedCount, List<MaterialEnum> materials)
    {
        if (materials == null)
            return false;

        materials.Clear();
        if (tutorialBattleRunning && currentStep == TutorialStep.BattleRefresh && selectedCount == 3)
        {
            materials.Add(MaterialEnum.Fire);
            materials.Add(MaterialEnum.Fire);
            materials.Add(MaterialEnum.Water);
            return true;
        }

        if (IsTutorialRun && currentStep == TutorialStep.EventRefresh && selectedCount > 0)
        {
            for (int i = 0; i < selectedCount; i++)
                materials.Add(MaterialEnum.Earth);
            return true;
        }

        return false;
    }

    public void OnRefreshCompleted(PlayerState playerState)
    {
        if (tutorialBattleRunning && currentStep == TutorialStep.BattleRefresh)
        {
            tutorialBattleInputUnlocked = true;
            HideAllSteps();
            return;
        }

        if (IsTutorialRun && currentStep == TutorialStep.EventRefresh)
            HideAllSteps();
    }

    public bool CanEndTurn(IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning || tutorialBattleInputUnlocked)
            return true;
        if (waitingForStepClick)
            return false;

        return currentStep == TutorialStep.BattleCancel || currentStep == TutorialStep.BattlePlay;
    }

    public void OnBattleCardsSelected(IReadOnlyList<MaterialModel> selectedCards)
    {
        if (!tutorialBattleRunning || selectedCards == null)
            return;
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

    public void OnMagicRewardChoicesShown()
    {
    }

    /// <summary>
    /// 选完道具奖励后，道具会自动进入道具栏第一位空槽，因此不再有“装备到槽位”教学页。
    /// 保留此钩子仅作语义显式（HandSystemUI 在选中奖励道具时调用）。
    /// </summary>
    public void OnRewardMagicSelected()
    {
    }

    public void OnRewardMagicEquipped(PlayerState playerState, IReadOnlyList<RunMapNodeModel> mapNodes, int currentMapNodeIndex, ChapterData chapter, LevelData currentLevel)
    {
        if (mainTutorialRunning && !tutorialCompleted && ShouldShowKeyboardUndoHint())
            ShowStep(TutorialStep.RewardUndoHint, true);
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
        if (currentStep != TutorialStep.EventOptions && currentStep != TutorialStep.EventRefresh)
            return;

        tutorialEventShown = true;
        HideAllSteps();
    }

    public bool ShouldUseTutorialEventFixedDraw(EventData eventData)
    {
        return IsTutorialRun && eventData != null && eventData.numericId == TutorialEventNumericId;
    }

    public void OnShopPanelShown()
    {
        if (IsTutorialRun)
            ShowStep(TutorialStep.ShopBuyHint, true);
    }

    /// <summary>休息关进入时调用（HandSystemUI.StartRestLevel）。</summary>
    public void OnRestPanelShown()
    {
        if (IsTutorialRun)
            ShowStep(TutorialStep.RestOptions, true);
    }

    public void OnShopPurchaseCompleted()
    {
        if (IsTutorialRun && !shopUndoHintShown && ShouldShowKeyboardUndoHint())
        {
            shopUndoHintShown = true;
            ShowStep(TutorialStep.ShopUndoHint, true);
        }
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

        switch (currentStep)
        {
            case TutorialStep.BattleInfo:
                ShowStep(TutorialStep.BattleArrowBase, true);
                break;
            case TutorialStep.BattleArrowBase:
                ShowStep(TutorialStep.BattleMagicBook, true);
                break;
            case TutorialStep.BattleMagicBook:
                ShowStep(TutorialStep.BattleCombo, true);
                break;
            case TutorialStep.BattleCombo:
                ShowStep(TutorialStep.BattlePlayLimit, true);
                break;
            case TutorialStep.BattlePlayLimit:
                ShowStep(TutorialStep.BattleEnemyInfo, true);
                break;
            case TutorialStep.BattleEnemyInfo:
                ShowStep(TutorialStep.BattleCancel, true);
                break;
            case TutorialStep.BattleCancel:
                ShowStep(TutorialStep.BattlePlay, false);
                break;
            case TutorialStep.ShopBuyHint:
                ShowStep(TutorialStep.ShopOrderHint, true);
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
            case TutorialStep.EventOptions:
                ShowStep(TutorialStep.EventRefresh, false);
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

    private int CountMaterial(IReadOnlyList<MaterialModel> cards, MaterialEnum material)
    {
        if (cards == null)
            return 0;

        int count = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && cards[i].CanActAs(material))
                count++;
        }
        return count;
    }

    private void CacheSteps()
    {
        stepObjects.Clear();
        RectTransform root = stepsRoot != null ? stepsRoot : transform.Find("Steps") as RectTransform;
        if (root == null)
            root = transform as RectTransform;

        AddStep(root, TutorialStep.BattleInfo, "Battle_Info");
        AddStep(root, TutorialStep.BattleArrowBase, "Battle_ArrowBase");
        AddStep(root, TutorialStep.BattleMagicBook, "Battle_MagicBook");
        AddStep(root, TutorialStep.BattleCombo, "Battle_Combo");
        AddStep(root, TutorialStep.BattlePlayLimit, "Battle_PlayLimit");
        AddStep(root, TutorialStep.BattleEnemyInfo, "Battle_EnemyInfo");
        AddStep(root, TutorialStep.BattleCancel, "Battle_Cancel");
        AddStep(root, TutorialStep.BattlePlay, "Battle_Play");
        AddStep(root, TutorialStep.BattleRefresh, "Battle_Refresh");
        AddStep(root, TutorialStep.MapPanel, "Map_Panel");
        AddStep(root, TutorialStep.MapMovement, "Map_Movement");
        AddStep(root, TutorialStep.MapStepLimit, "Map_StepLimit");
        AddStep(root, TutorialStep.RewardClaim, "Reward_Claim");
        AddStep(root, TutorialStep.RewardUndoHint, "Reward_UndoHint");
        AddStep(root, TutorialStep.ShopBuyHint, "Shop_BuyHint");
        AddStep(root, TutorialStep.ShopOrderHint, "Shop_OrderHint");
        AddStep(root, TutorialStep.ShopUndoHint, "Shop_UndoHint");
        AddStep(root, TutorialStep.EventOptions, "Event_Options");
        AddStep(root, TutorialStep.EventRefresh, "Event_Refresh");
        AddStep(root, TutorialStep.RestOptions, "Rest_Options");
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
        }
    }

    private void SetStepText(GameObject stepObject, string childName, string key)
    {
        if (stepObject == null || string.IsNullOrEmpty(key))
            return;

        TMP_Text text = FindStepText(stepObject.transform, childName);
        if (text != null)
            text.text = LocalizationSystem.GetText(key, text.text);
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
            case TutorialStep.BattleCombo: return "tutorial.battle.magic_combo.title";
            case TutorialStep.BattlePlayLimit: return "tutorial.battle.play_limit.title";
            case TutorialStep.BattleEnemyInfo: return "tutorial.battle.enemy_info.title";
            case TutorialStep.BattleCancel: return "tutorial.battle.cancel.title";
            case TutorialStep.BattlePlay: return "tutorial.battle.play.title";
            case TutorialStep.BattleRefresh: return "tutorial.battle.refresh.title";
            case TutorialStep.MapPanel: return "tutorial.map.panel.title";
            case TutorialStep.MapMovement: return "tutorial.map.movement.title";
            case TutorialStep.MapStepLimit: return "tutorial.map.step_limit.title";
            case TutorialStep.RewardClaim: return "tutorial.reward.claim.title";
            case TutorialStep.RewardUndoHint: return "tutorial.reward.undo_hint.title";
            case TutorialStep.ShopBuyHint: return "tutorial.shop.buy_hint.title";
            case TutorialStep.ShopOrderHint: return "tutorial.shop.order.title";
            case TutorialStep.ShopUndoHint: return "tutorial.shop.undo_hint.title";
            case TutorialStep.EventOptions: return "tutorial.event.options.title";
            case TutorialStep.EventRefresh: return "tutorial.event.refresh.title";
            case TutorialStep.RestOptions: return "tutorial.rest.title";
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
            case TutorialStep.BattleCombo: return "tutorial.battle.magic_combo.body";
            case TutorialStep.BattlePlayLimit: return "tutorial.battle.play_limit.body";
            case TutorialStep.BattleEnemyInfo: return "tutorial.battle.enemy_info.body";
            case TutorialStep.BattleCancel: return "tutorial.battle.cancel.body";
            case TutorialStep.BattlePlay: return "tutorial.battle.play.body";
            case TutorialStep.BattleRefresh: return "tutorial.battle.refresh.body";
            case TutorialStep.MapPanel: return "tutorial.map.panel.body";
            case TutorialStep.MapMovement: return "tutorial.map.movement.body";
            case TutorialStep.MapStepLimit: return "tutorial.map.step_limit.body";
            case TutorialStep.RewardClaim: return "tutorial.reward.claim.body";
            case TutorialStep.RewardUndoHint: return "tutorial.reward.undo_hint.body";
            case TutorialStep.ShopBuyHint: return "tutorial.shop.buy_hint.body";
            case TutorialStep.ShopOrderHint: return "tutorial.shop.order.body";
            case TutorialStep.ShopUndoHint: return "tutorial.shop.undo_hint.body";
            case TutorialStep.EventOptions: return "tutorial.event.options.body";
            case TutorialStep.EventRefresh: return "tutorial.event.refresh.body";
            case TutorialStep.RestOptions: return "tutorial.rest.body";
            default: return string.Empty;
        }
    }

    private void ShowStep(TutorialStep step, bool waitForClick)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        currentStep = step;
        waitingForStepClick = waitForClick;
        foreach (KeyValuePair<TutorialStep, GameObject> pair in stepObjects)
        {
            bool active = pair.Key == step;
            pair.Value.SetActive(active);
            SetStepRaycastTarget(pair.Value, active && waitForClick);
        }
        UpdateCutoutTarget(step);
        UpdateInputBlocker(waitForClick);
        SetMapTutorialInputLocked(IsMapTutorialBlockingInput);
    }

    /// <summary>
    /// 输入拦截：阅读页（点击推进）阻挡全屏，行为页（如出牌/换牌）只阻挡高亮框以外，
    /// 保证所有操作都发生在高亮框内；教学战斗解锁输入后完全不拦。
    /// </summary>
    private void UpdateInputBlocker(bool waitForClick)
    {
        if (inputBlocker == null)
            return;

        bool hasHoles = cutoutTargets.Count > 0;
        bool active = !tutorialBattleInputUnlocked && (waitForClick || hasHoles);
        inputBlocker.BlockWholeScreen = waitForClick || !hasHoles;
        inputBlocker.SetTargetList(cutoutTargets);
        inputBlocker.RefreshFromConfig();
        SetInputBlockerActive(active);
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
        foreach (KeyValuePair<TutorialStep, GameObject> pair in stepObjects)
            pair.Value.SetActive(false);
        if (cutoutMask != null)
            cutoutMask.gameObject.SetActive(false);
        SetInputBlockerActive(false);
        if (wasBlockingMapInput)
            SetMapTutorialInputLocked(false);
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

    private void UpdateCutoutTarget(TutorialStep step)
    {
        cutoutTargets.Clear();
        for (int i = 0; i < CutoutChildNames.Length; i++)
        {
            RectTransform cutout = GetStepCutoutTarget(step, CutoutChildNames[i]);
            if (cutout != null)
                cutoutTargets.Add(cutout);
        }

        if (cutoutMask == null)
            return;

        cutoutMask.gameObject.SetActive(true);
        cutoutMask.SetTargetList(cutoutTargets);
        cutoutMask.transform.SetAsFirstSibling();
    }

    private RectTransform GetStepCutoutTarget(TutorialStep step, string childName)
    {
        GameObject stepObject;
        if (!stepObjects.TryGetValue(step, out stepObject) || stepObject == null)
            return null;

        Transform child = stepObject.transform.Find(childName);
        return child != null ? child.GetComponent<RectTransform>() : null;
    }

    private bool ShouldShowKeyboardUndoHint()
    {
#if UNITY_IOS || UNITY_ANDROID
        return false;
#else
        return !Application.isMobilePlatform;
#endif
    }
}

