using System;
using System.Collections.Generic;
using UnityEngine;

public enum TutorialStep
{
    None,
    MapSelectBattle,
    BattleTurn1PlayFireFire,
    BattleTurn2RefreshOneCard,
    BattleTurn2PlayEarthEarth,
    BattleTurn3FocusTarget,
    BattleTurn3PlayAllCards,
    RewardSelectMagic,
    RewardEquipMagic,
    EventPlayRecipe,
    Completed
}

public class TutorialManagerUI : MonoBehaviour
{
    [SerializeField] private RectTransform stepsRoot;
    [SerializeField] private TutorialCutoutMaskUI cutoutMask;

    private const bool TutorialTemporarilyDisabled = true;

    private readonly Dictionary<TutorialStep, GameObject> stepObjects = new Dictionary<TutorialStep, GameObject>();
    private TutorialStep currentStep;
    private bool mainTutorialRunning;
    private bool tutorialBattleRunning;
    private int battleTurnIndex;

    public TutorialStep CurrentStep => currentStep;
    public bool MainTutorialRunning => mainTutorialRunning;
    public bool TutorialBattleRunning => tutorialBattleRunning;
    public bool IsCompleted => IsTutorialCompleted;
    private static bool IsTutorialCompleted => TutorialTemporarilyDisabled || RunSaveSystem.IsTutorialCompleted();
    private static bool IsEventTutorialShown => TutorialTemporarilyDisabled || RunSaveSystem.IsTutorialEventShown();

    public void Initialize(HandSystemUI owner)
    {
        CacheSteps();
        CacheCutoutMask();
        HideAllSteps();
        mainTutorialRunning = !IsTutorialCompleted;
    }

    public bool ShouldForceFirstNodeBattles()
    {
        return !IsTutorialCompleted;
    }

    public void OnLevelSelectShown(int nodeIndex)
    {
        if (!IsTutorialCompleted && nodeIndex == 0)
        {
            mainTutorialRunning = true;
            ShowStep(TutorialStep.MapSelectBattle);
        }
    }

    public bool ShouldUseTutorialBattle(int nodeIndex)
    {
        return mainTutorialRunning && !IsTutorialCompleted && nodeIndex == 0;
    }

    public void BeginTutorialBattle()
    {
        tutorialBattleRunning = true;
        battleTurnIndex = 0;
        HideAllSteps();
    }

    public EnemyModel CreateTutorialEnemy()
    {
        EnemyData data = new EnemyData
        {
            numericId = -1001,
            id = "tutorial_enemy",
            nameKey = "教程敌人",
            maxHealth = 15,
            baseAttack = 5,
            intentGroups = new[]
            {
                new EnemyIntentGroupData
                {
                    id = 1,
                    intents = new[]
                    {
                        new EnemyIntentData
                        {
                            intentType = EnemyIntentType.Attack,
                            actionType = EnemyActionType.Attack,
                            value = 5,
                            descriptionKey = "攻击 5"
                        }
                    }
                }
            },
            intentLoop = new[]
            {
                new EnemyIntentLoopData { groupId = 1 }
            },
            actionLoop = Array.Empty<EnemyActionData>()
        };
        return new EnemyModel(data);
    }

    public bool TryApplyFixedTurnHand(PlayerState playerState)
    {
        if (!tutorialBattleRunning || playerState == null)
            return false;

        battleTurnIndex++;
        switch (battleTurnIndex)
        {
            case 1:
                SetFixedHand(playerState, MaterialEnum.Fire, MaterialEnum.Fire, MaterialEnum.Wind, MaterialEnum.Water);
                ShowStep(TutorialStep.BattleTurn1PlayFireFire);
                return true;
            case 2:
                SetFixedHand(playerState, MaterialEnum.Wind, MaterialEnum.Fire, MaterialEnum.Water, MaterialEnum.Earth);
                ShowStep(TutorialStep.BattleTurn2RefreshOneCard);
                return true;
            case 3:
                SetFixedHand(playerState, MaterialEnum.Fire, MaterialEnum.Fire, MaterialEnum.Water, MaterialEnum.Water);
                ShowStep(TutorialStep.BattleTurn3FocusTarget);
                return true;
        }

        return false;
    }

    public bool CanMoveCardToPlay(MaterialModel card, IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning)
            return true;

        switch (currentStep)
        {
            case TutorialStep.BattleTurn1PlayFireFire:
                return card != null && card.CanActAs(MaterialEnum.Fire) && CountMaterial(playZone, MaterialEnum.Fire) < 2;
            case TutorialStep.BattleTurn2PlayEarthEarth:
                return card != null && card.CanActAs(MaterialEnum.Earth) && CountMaterial(playZone, MaterialEnum.Earth) < 2;
            case TutorialStep.BattleTurn3PlayAllCards:
                return card != null;
            default:
                return false;
        }
    }

    public bool CanRefreshSelected(IReadOnlyList<MaterialModel> selectedCards)
    {
        return tutorialBattleRunning && currentStep == TutorialStep.BattleTurn2RefreshOneCard && selectedCards != null && selectedCards.Count == 1;
    }

    public bool ShouldForceRefreshEarth(out MaterialEnum material)
    {
        material = MaterialEnum.Earth;
        return tutorialBattleRunning && currentStep == TutorialStep.BattleTurn2RefreshOneCard;
    }

    public void OnRefreshCompleted(PlayerState playerState)
    {
        if (tutorialBattleRunning && currentStep == TutorialStep.BattleTurn2RefreshOneCard)
            ShowStep(TutorialStep.BattleTurn2PlayEarthEarth);
    }

    public bool CanEndTurn(IReadOnlyList<MaterialModel> playZone)
    {
        if (!tutorialBattleRunning)
            return true;

        switch (currentStep)
        {
            case TutorialStep.BattleTurn1PlayFireFire:
                return CountMaterial(playZone, MaterialEnum.Fire) == 2 && playZone.Count == 2;
            case TutorialStep.BattleTurn2PlayEarthEarth:
                return CountMaterial(playZone, MaterialEnum.Earth) == 2 && playZone.Count == 2;
            case TutorialStep.BattleTurn3PlayAllCards:
                return playZone != null && playZone.Count >= 4;
            default:
                return false;
        }
    }

    public bool CanToggleFocus(EnemyModel enemy)
    {
        if (!tutorialBattleRunning)
            return true;

        return currentStep == TutorialStep.BattleTurn3FocusTarget && enemy != null && !enemy.IsDead;
    }

    public void OnFocusTargetChanged(EnemyModel enemy)
    {
        if (tutorialBattleRunning && currentStep == TutorialStep.BattleTurn3FocusTarget && enemy != null)
            ShowStep(TutorialStep.BattleTurn3PlayAllCards);
    }

    public void OnRewardPanelShown()
    {
        if (mainTutorialRunning && !IsTutorialCompleted)
            ShowStep(TutorialStep.RewardSelectMagic);
    }

    public void OnMagicRewardChoicesShown()
    {
        if (mainTutorialRunning && !IsTutorialCompleted)
            ShowStep(TutorialStep.RewardSelectMagic);
    }

    public void OnRewardMagicSelected()
    {
        if (mainTutorialRunning && !IsTutorialCompleted)
            ShowStep(TutorialStep.RewardEquipMagic);
    }

    public void OnRewardMagicEquipped(PlayerState playerState, IReadOnlyList<RunMapNodeModel> mapNodes, int currentMapNodeIndex, ChapterData chapter, LevelData currentLevel)
    {
        if (!mainTutorialRunning)
            return;

        RunSaveSystem.SetTutorialCompleted(true);
        RunSaveSystem.SaveCurrentRun(playerState, mapNodes, currentMapNodeIndex, chapter, currentLevel);
        mainTutorialRunning = false;
        tutorialBattleRunning = false;
        ShowStep(TutorialStep.Completed);
        HideAllSteps();
    }

    public void OnEventOptionsShown()
    {
        if (IsEventTutorialShown)
            return;

        ShowStep(TutorialStep.EventPlayRecipe);
    }

    public void OnEventOptionResolved()
    {
        if (currentStep != TutorialStep.EventPlayRecipe)
            return;

        RunSaveSystem.SetTutorialEventShown(true);
        HideAllSteps();
    }

    private void SetFixedHand(PlayerState playerState, params MaterialEnum[] materials)
    {
        playerState.Hand.Clear();
        playerState.PlayZone.Clear();
        playerState.DrawPile.Clear();
        playerState.DiscardPile.Clear();
        for (int i = 0; i < materials.Length; i++)
            playerState.Hand.Add(new MaterialModel(Guid.NewGuid().ToString("N"), materials[i]));
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
            root = (RectTransform)transform;

        AddStep(root, TutorialStep.MapSelectBattle, "Map_SelectBattle");
        AddStep(root, TutorialStep.BattleTurn1PlayFireFire, "Battle_Turn1_PlayFireFire");
        AddStep(root, TutorialStep.BattleTurn2RefreshOneCard, "Battle_Turn2_RefreshOneCard");
        AddStep(root, TutorialStep.BattleTurn2PlayEarthEarth, "Battle_Turn2_PlayEarthEarth");
        AddStep(root, TutorialStep.BattleTurn3FocusTarget, "Battle_Turn3_FocusTarget");
        AddStep(root, TutorialStep.BattleTurn3PlayAllCards, "Battle_Turn3_PlayAllCards");
        AddStep(root, TutorialStep.RewardSelectMagic, "Reward_SelectMagic");
        AddStep(root, TutorialStep.RewardEquipMagic, "Reward_EquipMagic");
        AddStep(root, TutorialStep.EventPlayRecipe, "Event_PlayRecipe");
    }

    private void AddStep(RectTransform root, TutorialStep step, string objectName)
    {
        Transform child = root.Find(objectName);
        if (child != null)
            stepObjects[step] = child.gameObject;
    }

    private void ShowStep(TutorialStep step)
    {
        currentStep = step;
        foreach (KeyValuePair<TutorialStep, GameObject> pair in stepObjects)
            pair.Value.SetActive(pair.Key == step);
        UpdateCutoutTarget(step);
    }

    private void HideAllSteps()
    {
        currentStep = TutorialStep.None;
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

        cutoutMask.gameObject.SetActive(true);
        string targetName = GetCutoutTargetName(step);
        if (string.IsNullOrEmpty(targetName))
            cutoutMask.SetTarget(null);
        else
            cutoutMask.SetTargetByName(transform.root, targetName);
        cutoutMask.transform.SetAsFirstSibling();
    }

    private string GetCutoutTargetName(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.MapSelectBattle:
                return "LevelSelectPanel";
            case TutorialStep.BattleTurn1PlayFireFire:
            case TutorialStep.BattleTurn2PlayEarthEarth:
            case TutorialStep.BattleTurn3PlayAllCards:
                return "HandArea";
            case TutorialStep.BattleTurn2RefreshOneCard:
                return "RefreshButton";
            case TutorialStep.BattleTurn3FocusTarget:
                return "EnemyArea";
            case TutorialStep.RewardSelectMagic:
                return "RewardPanel";
            case TutorialStep.RewardEquipMagic:
                return "MagicBookArea";
            case TutorialStep.EventPlayRecipe:
                return "EventPanel";
            default:
                return string.Empty;
        }
    }
}
