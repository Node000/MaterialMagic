using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum TutorialStep
{
    None,
    BattleInfo,
    BattleMagicBook,
    BattleEnemyInfo,
    BattlePlay,
    BattleRefresh,
    BattleRefreshConfirm,
    RewardClaim,
    RewardEquipMagic,
    RewardUndoHint,
    ShopBuyHint,
    ShopUndoHint,
    EventOptions,
    EventRefresh,
    Completed
}

public class TutorialManagerUI : MonoBehaviour
{
    public const int TutorialChapterNumericId = 100;
    public const int TutorialBattleLevelId = 1001;
    public const int TutorialEventLevelId = 1002;
    public const int TutorialShopLevelId = 1003;
    public const int TutorialRestLevelId = 1004;
    public const int TutorialBossLevelId = 1005;
    public const int TutorialEventNumericId = 1001;
    public const int TutorialDummyLEnemyId = 1001;
    public const int TutorialDummyXXLEnemyId = 1002;

    [SerializeField] private RectTransform stepsRoot;
    [SerializeField] private TutorialCutoutMaskUI cutoutMask;

    private readonly Dictionary<TutorialStep, GameObject> stepObjects = new Dictionary<TutorialStep, GameObject>();
    private TutorialStep currentStep;
    private bool mainTutorialRunning;
    private bool tutorialBattleRunning;
    private bool waitingForStepClick;
    private int battleTurnIndex;

    public TutorialStep CurrentStep => currentStep;
    public bool MainTutorialRunning => mainTutorialRunning;
    public bool TutorialBattleRunning => tutorialBattleRunning;
    public bool ShouldKillTutorialEnemyAfterPlayerTurn => tutorialBattleRunning && battleTurnIndex >= 2;
    public bool IsCompleted => RunSaveSystem.IsTutorialCompleted();
    public bool IsTutorialRun { get; private set; }

    public void Initialize(HandSystemUI owner)
    {
        CacheSteps();
        CacheCutoutMask();
        LocalizeSteps();
        HideAllSteps();
        IsTutorialRun = owner != null && owner.ActiveChapterNumericId == TutorialChapterNumericId;
        mainTutorialRunning = IsTutorialRun && !RunSaveSystem.IsTutorialCompleted();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            AdvanceStepByClick();
    }

    public bool ShouldForceFirstNodeBattles()
    {
        return false;
    }

    public void OnLevelSelectShown(int nodeIndex)
    {
    }

    public bool ShouldUseTutorialBattle(LevelData level)
    {
        return IsTutorialRun && level != null && level.numericId == TutorialBattleLevelId;
    }

    public void BeginTutorialBattle()
    {
        tutorialBattleRunning = true;
        battleTurnIndex = 0;
        HideAllSteps();
    }

    public bool TryApplyFixedTurnHand(PlayerState playerState)
    {
        if (!tutorialBattleRunning || playerState == null)
            return false;

        battleTurnIndex++;
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
        if (!tutorialBattleRunning)
            return true;
        if (waitingForStepClick)
            return false;

        switch (currentStep)
        {
            case TutorialStep.BattlePlay:
                return card != null && card.CanActAs(MaterialEnum.Fire) && CountMaterial(playZone, MaterialEnum.Fire) < 2;
            default:
                return false;
        }
    }

    public bool CanMovePlayCardToHand(MaterialModel card)
    {
        if (!tutorialBattleRunning)
            return true;
        if (waitingForStepClick)
            return false;

        return currentStep == TutorialStep.BattlePlay && card != null && card.CanActAs(MaterialEnum.Fire);
    }

    public bool CanRefreshSelected(IReadOnlyList<MaterialModel> selectedCards)
    {
        return tutorialBattleRunning && currentStep == TutorialStep.BattleRefreshConfirm && selectedCards != null && selectedCards.Count == 3;
    }

    public bool TryGetForcedRefreshMaterials(int selectedCount, List<MaterialEnum> materials)
    {
        if (materials == null)
            return false;

        materials.Clear();
        if (tutorialBattleRunning && currentStep == TutorialStep.BattleRefreshConfirm && selectedCount == 3)
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
        if (tutorialBattleRunning && currentStep == TutorialStep.BattleRefreshConfirm)
        {
            SetFixedHand(playerState, MaterialEnum.Fire, MaterialEnum.Fire, MaterialEnum.Water);
            HideAllSteps();
            return;
        }

        if (IsTutorialRun && currentStep == TutorialStep.EventRefresh)
            HideAllSteps();
    }

    public bool CanEndTurn(IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning)
            return true;
        if (waitingForStepClick)
            return false;

        return currentStep == TutorialStep.BattlePlay && CountMaterial(playZone, MaterialEnum.Fire) == 2 && playZone.Count == 2;
    }

    public void OnBattleCardsSelected(IReadOnlyList<MaterialModel> selectedCards)
    {
        if (!tutorialBattleRunning || selectedCards == null)
            return;

        if (currentStep == TutorialStep.BattleRefresh && CountMaterial(selectedCards, MaterialEnum.Earth) == 3 && selectedCards.Count == 3)
            ShowStep(TutorialStep.BattleRefreshConfirm, false);
    }

    public void OnBattleCardsPlayed(IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning)
            return;

        if (currentStep == TutorialStep.BattlePlay && CountMaterial(playZone, MaterialEnum.Fire) == 2 && playZone.Count == 2)
            HideCurrentStepVisual();
    }

    public void OnBattleCardCanceled(IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning || currentStep != TutorialStep.BattlePlay)
            return;

        HideCurrentStepVisual();
    }

    public void OnBattleReadyToEndTurn(IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning || currentStep != TutorialStep.BattlePlay)
            return;

        if (CountMaterial(playZone, MaterialEnum.Fire) == 2 && playZone.Count == 2)
            HideCurrentStepVisual();
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
        if (mainTutorialRunning && !RunSaveSystem.IsTutorialCompleted())
            ShowStep(TutorialStep.RewardClaim, true);
    }

    public void OnMagicRewardChoicesShown()
    {
        if (mainTutorialRunning && !RunSaveSystem.IsTutorialCompleted())
            ShowStep(TutorialStep.RewardClaim, true);
    }

    public void OnRewardMagicSelected()
    {
        if (mainTutorialRunning && !RunSaveSystem.IsTutorialCompleted())
            ShowStep(TutorialStep.RewardEquipMagic, true);
    }

    public void OnRewardMagicEquipped(PlayerState playerState, IReadOnlyList<RunMapNodeModel> mapNodes, int currentMapNodeIndex, ChapterData chapter, LevelData currentLevel)
    {
        if (mainTutorialRunning && !RunSaveSystem.IsTutorialCompleted() && ShouldShowKeyboardUndoHint())
            ShowStep(TutorialStep.RewardUndoHint, true);
    }

    public void CompleteTutorial(PlayerState playerState, IReadOnlyList<RunMapNodeModel> mapNodes, int currentMapNodeIndex, ChapterData chapter, LevelData currentLevel)
    {
        if (!IsTutorialRun)
            return;

        RunSaveSystem.SetTutorialCompleted(true);
        RunSaveSystem.SaveCurrentRun(playerState, mapNodes, currentMapNodeIndex, chapter, currentLevel);
        mainTutorialRunning = false;
        tutorialBattleRunning = false;
        ShowStep(TutorialStep.Completed, false);
        HideAllSteps();
    }

    public void OnEventOptionsShown()
    {
        if (RunSaveSystem.IsTutorialEventShown())
            return;

        if (IsTutorialRun && !RunSaveSystem.IsTutorialEventShown())
            ShowStep(TutorialStep.EventOptions, true);
    }

    public void OnEventOptionResolved()
    {
        if (currentStep != TutorialStep.EventOptions && currentStep != TutorialStep.EventRefresh)
            return;

        RunSaveSystem.SetTutorialEventShown(true);
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

    public void OnShopPurchaseCompleted()
    {
        if (IsTutorialRun && ShouldShowKeyboardUndoHint())
            ShowStep(TutorialStep.ShopUndoHint, true);
    }

    private void AdvanceStepByClick()
    {
        if (!waitingForStepClick)
            return;

        switch (currentStep)
        {
            case TutorialStep.BattleInfo:
                ShowStep(TutorialStep.BattleMagicBook, true);
                break;
            case TutorialStep.BattleMagicBook:
                ShowStep(TutorialStep.BattleEnemyInfo, true);
                break;
            case TutorialStep.BattleEnemyInfo:
                ShowStep(TutorialStep.BattlePlay, false);
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

        playerState.Hand.Clear();
        playerState.PlayZone.Clear();
        playerState.DrawPile.Clear();
        playerState.DiscardPile.Clear();
        playerState.ConsumedPile.Clear();
        for (int i = 0; i < materials.Length; i++)
            playerState.Hand.Add(new MaterialModel(System.Guid.NewGuid().ToString("N"), materials[i]));
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
        AddStep(root, TutorialStep.BattleMagicBook, "Battle_MagicBook");
        AddStep(root, TutorialStep.BattleEnemyInfo, "Battle_EnemyInfo");
        AddStep(root, TutorialStep.BattlePlay, "Battle_Play");
        AddStep(root, TutorialStep.BattleRefresh, "Battle_Refresh");
        AddStep(root, TutorialStep.BattleRefreshConfirm, "Battle_RefreshConfirm");
        AddStep(root, TutorialStep.RewardClaim, "Reward_Claim");
        AddStep(root, TutorialStep.RewardEquipMagic, "Reward_EquipMagic");
        AddStep(root, TutorialStep.RewardUndoHint, "Reward_UndoHint");
        AddStep(root, TutorialStep.ShopBuyHint, "Shop_BuyHint");
        AddStep(root, TutorialStep.ShopUndoHint, "Shop_UndoHint");
        AddStep(root, TutorialStep.EventOptions, "Event_Options");
        AddStep(root, TutorialStep.EventRefresh, "Event_Refresh");
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

        Transform child = stepObject.transform.Find(childName);
        TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
        if (text != null)
            text.text = LocalizationSystem.GetText(key, text.text);
    }

    private string GetStepTitleKey(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.BattleInfo: return "tutorial.battle.info.title";
            case TutorialStep.BattleMagicBook: return "tutorial.battle.magic_book.title";
            case TutorialStep.BattleEnemyInfo: return "tutorial.battle.enemy_info.title";
            case TutorialStep.BattlePlay: return "tutorial.battle.play.title";
            case TutorialStep.BattleRefresh: return "tutorial.battle.refresh.title";
            case TutorialStep.BattleRefreshConfirm: return "tutorial.battle.refresh_confirm.title";
            case TutorialStep.RewardClaim: return "tutorial.reward.claim.title";
            case TutorialStep.RewardEquipMagic: return "tutorial.reward.equip_magic.title";
            case TutorialStep.RewardUndoHint: return "tutorial.reward.undo_hint.title";
            case TutorialStep.ShopBuyHint: return "tutorial.shop.buy_hint.title";
            case TutorialStep.ShopUndoHint: return "tutorial.shop.undo_hint.title";
            case TutorialStep.EventOptions: return "tutorial.event.options.title";
            case TutorialStep.EventRefresh: return "tutorial.event.refresh.title";
            default: return string.Empty;
        }
    }

    private string GetStepBodyKey(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.BattleInfo: return "tutorial.battle.info.body";
            case TutorialStep.BattleMagicBook: return "tutorial.battle.magic_book.body";
            case TutorialStep.BattleEnemyInfo: return "tutorial.battle.enemy_info.body";
            case TutorialStep.BattlePlay: return "tutorial.battle.play.body";
            case TutorialStep.BattleRefresh: return "tutorial.battle.refresh.body";
            case TutorialStep.BattleRefreshConfirm: return "tutorial.battle.refresh_confirm.body";
            case TutorialStep.RewardClaim: return "tutorial.reward.claim.body";
            case TutorialStep.RewardEquipMagic: return "tutorial.reward.equip_magic.body";
            case TutorialStep.RewardUndoHint: return "tutorial.reward.undo_hint.body";
            case TutorialStep.ShopBuyHint: return "tutorial.shop.buy_hint.body";
            case TutorialStep.ShopUndoHint: return "tutorial.shop.undo_hint.body";
            case TutorialStep.EventOptions: return "tutorial.event.options.body";
            case TutorialStep.EventRefresh: return "tutorial.event.refresh.body";
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
            pair.Value.SetActive(pair.Key == step);
        UpdateCutoutTarget(step);
    }

    private void HideCurrentStepVisual()
    {
        waitingForStepClick = false;
        foreach (KeyValuePair<TutorialStep, GameObject> pair in stepObjects)
            pair.Value.SetActive(false);
        if (cutoutMask != null)
            cutoutMask.gameObject.SetActive(false);
    }

    private void HideAllSteps()
    {
        currentStep = TutorialStep.None;
        waitingForStepClick = false;
        foreach (KeyValuePair<TutorialStep, GameObject> pair in stepObjects)
            pair.Value.SetActive(false);
        if (cutoutMask != null)
            cutoutMask.gameObject.SetActive(false);
    }

    private void CacheCutoutMask()
    {
        if (cutoutMask == null)
            cutoutMask = GetComponentInChildren<TutorialCutoutMaskUI>(true);
    }

    private void UpdateCutoutTarget(TutorialStep step)
    {
        if (cutoutMask == null)
            return;

        if (step == TutorialStep.BattlePlay)
        {
            cutoutMask.gameObject.SetActive(false);
            return;
        }

        cutoutMask.gameObject.SetActive(true);
        RectTransform stepCutout = GetStepCutoutTarget(step);
        if (stepCutout != null)
        {
            cutoutMask.SetTarget(stepCutout);
        }
        else
        {
            string targetName = GetCutoutTargetName(step);
            if (string.IsNullOrEmpty(targetName))
                cutoutMask.SetTarget(null);
            else
                cutoutMask.SetTargetByName(transform.root, targetName);
        }
        cutoutMask.transform.SetAsFirstSibling();
    }

    private RectTransform GetStepCutoutTarget(TutorialStep step)
    {
        GameObject stepObject;
        if (!stepObjects.TryGetValue(step, out stepObject) || stepObject == null)
            return null;

        Transform child = stepObject.transform.Find("Cutout");
        return child != null ? child.GetComponent<RectTransform>() : null;
    }

    private string GetCutoutTargetName(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.BattleInfo:
                return "TopBar";
            case TutorialStep.BattleEnemyInfo:
                return "EnemyArea";
            case TutorialStep.BattleRefresh:
            case TutorialStep.BattleRefreshConfirm:
            case TutorialStep.EventRefresh:
                return "HandArea";
            case TutorialStep.BattleMagicBook:
            case TutorialStep.RewardEquipMagic:
                return "MagicBookArea";
            case TutorialStep.RewardClaim:
            case TutorialStep.RewardUndoHint:
                return "RewardPanel";
            case TutorialStep.EventOptions:
                return "EventPanel";
            case TutorialStep.ShopBuyHint:
            case TutorialStep.ShopUndoHint:
                return "ShopPanel";
            default:
                return string.Empty;
        }
    }

    private bool ShouldShowKeyboardUndoHint()
    {
#if UNITY_IOS || UNITY_ANDROID
        return false;
#else
        return true;
#endif
    }
}

