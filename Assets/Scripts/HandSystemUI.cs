using System;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public class HandSystemUI : MonoBehaviour
{
	private class EnemyViewState
	{
		public EnemyModel model;

		public RectTransform viewRect;

		public Image healthFill;

		public Image intentIcon;

		public Image bodyImage;

		public Color bodyBaseColor;

		public Image healthBufferFill;

		public Image shieldFill;

		public TMP_Text healthText;

		public RectTransform buffRoot;

		public Image focusMarker;

		public readonly List<EnemyIntentView> intentViews = new List<EnemyIntentView>();

		public int displayedHealth;

		public Tween healthNumberTween;
	}

	[SerializeField]
	private RectTransform handArea;

	[SerializeField]
	private RectTransform playArea;

	[SerializeField]
	private RectTransform deckPileArea;

	[SerializeField]
	private RectTransform magicBookArea;

	[SerializeField]
	private RectTransform enemyArea;

	[SerializeField]
	private RectTransform cardPrefab;

	[SerializeField]
	private RectTransform magicViewPrefab;

	[SerializeField]
	private RectTransform enemyViewPrefab;

	[SerializeField]
	private RectTransform floatingTextPrefab;

	[SerializeField]
	private RectTransform buffSlotPrefab;

	[SerializeField]
	private Button refreshButton;

	[SerializeField]
	private Button endTurnButton;

	[SerializeField]
	private TMP_Text deckCountText;

	[Header("Buff栏参数")]
	[SerializeField]
	private float buffSlotSize = 42f;

	[SerializeField]
	private float buffSlotSpacing = 6f;

	[SerializeField]
	private float cardSpacing = 118f;

	[SerializeField]
	private float playCardSpacing = 118f;

	[SerializeField]
	private float layoutDuration = 0.3f;

	[SerializeField]
	private Ease layoutEase = (Ease)9;

	[SerializeField]
	private float enemyIntentRippleDuration = 0.36f;

	[SerializeField]
	private float enemyIntentPrePerformDelay = 0.02f;

	[SerializeField]
	private float enemyIntentBetweenDelay = 0.12f;

	[SerializeField]
	private float enemyIntentFadeOutDuration = 0.18f;

	[SerializeField]
	private float enemyBetweenDelay = 0.16f;

	[SerializeField]
	private float enemyTurnEndDelay = 0.5f;

	[SerializeField]
	private float enemyAttackJumpDistance = 26f;

	[SerializeField]
	private float enemyAttackJumpDuration = 0.18f;

	[SerializeField]
	private float enemyDefendIconDuration = 0.38f;

	[SerializeField]
	private float enemyDefendIconSize = 62f;

	[Header("战斗UI动画参数")]
	[SerializeField]
	private float materialCardPunchDuration = 0.22f;

	[SerializeField]
	private float materialCardPunchStrength = 12f;

	[SerializeField]
	private int materialCardPunchVibrato = 6;

	[SerializeField]
	private float materialCardPunchElasticity = 0.6f;

	[SerializeField]
	private float postMagicResolveDelay = 0.11f;

    [Header("玩家施法音效")]
    [SerializeField]
    private float playerCastSwingPitchBase = 1f;

    [SerializeField]
    private float playerCastSwingPitchIncrease = 0.08f;

    [SerializeField]
    private float playerCastSwingPitchMax = 1.45f;

	[SerializeField]
	private float enemyDeathScaleDuration = 0.35f;

	[SerializeField]
	private Ease enemyDeathScaleEase = Ease.InBack;

	[SerializeField]
	[FormerlySerializedAs("enemyDissolveDuration")]
	private float enemyDeathExplosionDuration = 0.7f;

	[SerializeField]
	private float enemyHitShakeDuration = 0.28f;

	[SerializeField]
	private Vector2 enemyHitShakeStrength = new Vector2(16f, 8f);

	[SerializeField]
	private int enemyHitShakeVibrato = 18;

	[SerializeField]
	private float enemyHitColorRecoverDuration = 0.22f;

	[SerializeField]
	private Ease enemyHitColorRecoverEase = Ease.OutBack;

	[SerializeField]
	private float floatingTextDuration = 0.75f;

	[SerializeField]
	private float floatingTextYOffset = 42f;

	[SerializeField]
	private int floatingTextFontSize = 24;

	[SerializeField]
	private Vector2 floatingTextStartOffset = new Vector2(0f, 28f);

	[SerializeField]
	private Vector2 floatingTextSize = new Vector2(80f, 32f);

	[SerializeField]
	private Ease floatingTextMoveEase = Ease.OutBack;

	[SerializeField]
	private Ease floatingTextFadeEase = Ease.OutQuad;

	[Header("敌人血条动画参数")]
	[SerializeField]
	private float enemyHealthFillDuration = 0.35f;

	[SerializeField]
	private float enemyHealthBufferDecreaseDuration = 0.55f;

	[SerializeField]
	private float enemyHealthBufferIncreaseDuration = 0.35f;

	[SerializeField]
	private float enemyHealthTextDuration = 0.35f;

	[SerializeField]
	private float enemyHealthTextFontSize = 18f;

	[SerializeField]
	private Ease enemyHealthEase = Ease.OutQuad;

	[SerializeField]
	private float levelSelectAfterMapHideExtraDelay = 0.06f;

	[SerializeField]
	private float levelSelectAfterMapHideFallbackDelay = 0.28f;

	[SerializeField]
	private UIManager uiManager;

	private readonly List<HandCardView> cardViews = new List<HandCardView>();

	private readonly List<MagicItemView> magicViews = new List<MagicItemView>();

	private readonly List<MaterialModel> selectedCards = new List<MaterialModel>();

	private readonly HashSet<HandCardView> newCardViews = new HashSet<HandCardView>();

	private readonly HashSet<MaterialModel> consumedBattleDeckCards = new HashSet<MaterialModel>();

	private readonly List<EnemyModel> enemyModels = new List<EnemyModel>();

	private readonly List<EnemyViewState> enemyViewStates = new List<EnemyViewState>();

	private BattleManager battleManager;

	private readonly List<RunMapNodeModel> mapNodes = new List<RunMapNodeModel>();

	private readonly List<CombatantModel> magicEnemyTargets = new List<CombatantModel>();

	private EnemyModel enemyModel;

	private PlayerState playerState;

	private LevelData currentLevel;

	private Material enemyDeathExplosionMaterialTemplate;

	private Material temporaryCardDissolveMaterialTemplate;

	private RectTransform playerBuffRoot;

	private Image enemyHealthFill;

	private Image enemyIntentIcon;

	private RectTransform enemyViewRect;

	private Image enemyBodyImage;

	private Color enemyBodyBaseColor;

	private ISpellCastEffect spellCastEffect;

	private PlayerCastAnimatorUI playerCastAnimator;

	private RectTransform spellParticleEmitter;

	private MagicModel pendingCastParticleMagic;

	private RectTransform pendingCastParticleTarget;

    private readonly List<RectTransform> pendingCastParticleTargets = new List<RectTransform>();

    private readonly List<RectTransform> castParticleTargetBuffer = new List<RectTransform>();

	private Action pendingCastParticleImpactHandler;

	private int pendingCastShakeCount;

	private Image enemyHealthBufferFill;

	private Image enemyShieldFill;

	private TMP_Text enemyHealthText;

	private int displayedEnemyHealth;

	private Tween enemyHealthNumberTween;


	private ChapterData activeChapter;

	private MagicData pendingRewardMagic;

    private MagicData pendingShopMagic;

    private Action<int> pendingShopMagicSlotChosen;

	private MagicModifierData pendingMagicModifier;

	private EventModel currentEvent;

	private EventPanelUI eventPanel;

	private bool refreshUsedThisTurn;

	private bool suppressEnemyIntentRefresh;

	private int currentMapNodeIndex;

	private bool busy;

	private bool choosingEventCard;

	private EventOptionData pendingChoiceOption;

	private int pendingChoiceCount;

	private readonly List<MaterialModel> pendingChoiceCards = new List<MaterialModel>();

	private bool runEnded;

    private float loadedRunPlaySeconds;

    private float runStartRealtime;

	private TutorialManagerUI TutorialManager => GetUIManager().TutorialManager;

	private const int RestStudyResultId = 301;

	private const int RestDeepStudyResultId = 302;

	private const float RestDefaultHealRatio = 0.3f;

    private const float AcquireMagicAnimationDuration = 0.42f;

    private const float AcquireMaterialAnimationDuration = 0.42f;

    private const int BuffRootColumnCount = 5;

    private const int BuffRootRowCount = 2;

    private const float EnemyHealthTextWidth = 56f;

    private const int EnemyDeathShardColumns = 5;

    private const int EnemyDeathShardRows = 4;

    private const float EnemyDeathExplosionDistance = 120f;

    private const string EnemyDeathExplosionMaterialPath = "Materials/Style/Sprite/M_Sprite_FragmentExplosion_Default";

	public PlayerState PlayerState => playerState;

    public void DebugDealDamageToTarget(int damage)
    {
        if (damage <= 0 || battleManager == null)
            return;

        EnemyModel target = battleManager.GetTargetEnemy();
        if (target == null)
            return;

        target.TakeDamage(damage, playerState != null ? new CombatantModel(playerState) : null);
        RefreshEnemyUI((RectTransform)null, false);
        StartCoroutine(DebugHandleBattleResult());
    }

    public void DebugHealPlayer(int amount)
    {
        if (amount <= 0 || playerState == null)
            return;

        playerState.Heal(amount);
        UpdatePlayerHealthUI(false);
    }

    public void DebugGainPlayerShield(int amount)
    {
        if (amount <= 0 || playerState == null)
            return;

        playerState.GainShield(amount);
        UpdatePlayerHealthUI(false);
    }

    private IEnumerator DebugHandleBattleResult()
    {
        yield return PlayPendingEnemyDeaths();
        if (AllEnemiesDead())
            yield return FinishBattleRoutine();
    }

	public bool IsDeckCardConsumedInCurrentBattle(MaterialModel materialModel)
	{
		return materialModel != null && playerState != null && playerState.Deck.Contains(materialModel) &&
			(consumedBattleDeckCards.Contains(materialModel) || !playerState.DrawPile.Contains(materialModel));
	}

	public IReadOnlyList<RunMapNodeModel> MapNodes => mapNodes;

	public int CurrentMapNodeIndex => currentMapNodeIndex;

	private const int RunNodeCount = 21;

	private const int DebugRewardLevelNumericId = 301;

	private const int FirstFixedEventNodeIndex = 7;

	private const int SecondFixedEventNodeIndex = 15;

	private void ShowLevelSelect()
	{
		busy = true;
		SetButtonsInteractable(interactable: false);
		HideLevelSelectPanel();
		if (currentMapNodeIndex < mapNodes.Count)
		{
			CreateLevelSelectPanel();
			return;
		}
		ShowVictoryPanel();
	}

	private void CreateLevelSelectPanel()
	{
		GetUIManager().ShowLevelSelect(mapNodes, currentMapNodeIndex);
		RunMapNodeModel currentNode = currentMapNodeIndex >= 0 && currentMapNodeIndex < mapNodes.Count ? mapNodes[currentMapNodeIndex] : null;
		bool hasDebugRewardOption = currentNode != null &&
			((currentNode.leftLevel != null && currentNode.leftLevel.numericId == DebugRewardLevelNumericId) ||
			(currentNode.rightLevel != null && currentNode.rightLevel.numericId == DebugRewardLevelNumericId));
		if (!hasDebugRewardOption)
			TutorialManager?.OnLevelSelectShown(currentMapNodeIndex);
	}

	private void HideLevelSelectPanel()
	{
		GetUIManager().HideLevelSelect();
	}

	private void HideLevelSelectPanelAnimated(Action onComplete = null)
	{
		GetUIManager().HideLevelSelectAnimated(onComplete);
	}

	private List<LevelData> GetLevels(LevelType type)
	{
		List<LevelData> list = new List<LevelData>();
		foreach (LevelData value in GameDataDatabase.LevelData.Values)
		{
			if (value.levelType == type)
			{
				list.Add(value);
			}
		}
		return list;
	}

	private List<LevelData> GetBattleLevels()
	{
		return GetLevels(LevelType.Battle);
	}

	private List<LevelData> GetEventLevels()
	{
		return GetLevels(LevelType.Event);
	}

	private List<LevelData> GetRestLevels()
	{
		return GetLevels(LevelType.Rest);
	}

	public UIManager GetUIManager()
	{
		if ((Object)(object)uiManager == (Object)null)
		{
			uiManager = ((Component)this).GetComponent<UIManager>();
			if ((Object)(object)uiManager == (Object)null)
				uiManager = ((Component)this).gameObject.AddComponent<UIManager>();
			uiManager.Initialize(this, ((Component)this).transform);
		}
		return uiManager;
	}

	public void StartLevel(LevelData level)
	{
		GameLog.Data($"Start level node={currentMapNodeIndex + 1}/{mapNodes.Count} id={level.id} type={level.levelType}");
		currentLevel = level;
		if (currentMapNodeIndex >= 0 && currentMapNodeIndex < mapNodes.Count)
		{
			mapNodes[currentMapNodeIndex].selectedLevel = level;
			GetUIManager().MapPanel?.RefreshNodeVisual(currentMapNodeIndex);
			GetUIManager().MapPanel?.SetPlayerMarkerNodeIndex(currentMapNodeIndex);
		}
        SaveRunProgress();
        busy = true;
        SetButtonsInteractable(interactable: false);
        HideMapPanel();
        HideLevelSelectPanelAnimated(() => StartLevelAfterSelectClosed(level));
	}

    private void StartLevelAfterSelectClosed(LevelData level)
    {
		if (level.levelType == LevelType.Event)
		{
			StartEventLevel(level);
		}
		else if (level.levelType == LevelType.Shop)
		{
			StartShopLevel(level);
		}
		else if (level.levelType == LevelType.Rest)
		{
			StartRestLevel(level);
		}
		else if (level.levelType == LevelType.Reward)
		{
			StartRewardLevel(level);
		}
		else
		{
			StartBattleLevel(level);
		}
	}

	private void StartBattleLevel(LevelData level)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
		int configuredEnemyCount = level.enemies != null && level.enemies.Length > 0 ? level.enemies.Length : (level.enemyIds != null ? level.enemyIds.Length : 0);
		GameLog.Data("Start battle level=" + level.id + " enemies=" + configuredEnemyCount);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBattleMusic();
		currentLevel = level;
		currentEvent = null;
		refreshUsedThisTurn = false;
		ResetBattleDeckState();
		HideMapPanel();
		enemyModels.Clear();
		battleManager.ClearEnemies();
        ClearEnemyViews();
		bool tutorialBattle = TutorialManager != null && TutorialManager.ShouldUseTutorialBattle(currentMapNodeIndex);
		if (tutorialBattle)
		{
			battleManager.SpawnEnemy(TutorialManager.CreateTutorialEnemy());
			TutorialManager.BeginTutorialBattle();
		}
		else if (level.enemies != null && level.enemies.Length > 0)
		{
			for (int i = 0; i < level.enemies.Length; i++)
			{
				battleManager.SpawnEnemy(level.enemies[i]);
			}
		}
		else if (level.enemyIds != null)
		{
			for (int i = 0; i < level.enemyIds.Length; i++)
			{
				battleManager.SpawnEnemy(level.enemyIds[i]);
			}
		}
		if (battleManager.Enemies.Count != 0)
		{
            TriggerMagicBattleStart();
			BeginPlayerTurn(playerState.DrawCount);
			ResetMagicHighlights();
			busy = false;
			SetButtonsInteractable(interactable: true);
		}
	}

	private void BeginPlayerTurn(int drawCount)
	{
		GameLog.Data($"Begin player turn drawCount={drawCount} extra={playerState.GetBuffStack(BuffEnum.ExtraDraw)}");
		GetUIManager().TurnBanner?.Show("你的回合");
		CombatantModel opponent = new CombatantModel(GetFirstAliveEnemy());
		ResetContinuousCastCounterUI();
		suppressEnemyIntentRefresh = false;
		RefreshEnemyUI();
        playerState.ClearShield();
		playerState.TriggerOnTurnStart(opponent);
		playerState.TriggerAfterTurnStart(opponent);
        TriggerMagicTurnStart();
        int extraDraw = playerState.GetBuffStack(BuffEnum.ExtraDraw);
        bool fixedTutorialHand = TutorialManager != null && TutorialManager.TryApplyFixedTurnHand(playerState);
        if (!fixedTutorialHand)
        {
            playerState.DrawCards(drawCount + extraDraw);
            if (extraDraw > 0)
                playerState.ConsumeBuff(BuffEnum.ExtraDraw, extraDraw);
            playerState.ConsumeTemporaryMaterialsNextTurn();
        }
        if (playerState.GetBuffStack(BuffEnum.Sturdy) > 0)
            playerState.ApplySturdyToHand();
		GetUIManager().PlayerFeedback?.UpdateVignetteRange(playerState);
		RefreshStaticUI();
		RefreshMaterialListPanel();
		RebuildCards(animateFromCurrent: true);
	}

	private void StartShopLevel(LevelData level)
	{
		GameLog.Data("Start shop level=" + level.id);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameplayMusic();
		ResetBattleDeckState();
		currentLevel = level;
		currentEvent = null;
		pendingRewardMagic = null;
        pendingShopMagic = null;
        pendingShopMagicSlotChosen = null;
        pendingMagicModifier = null;
		HideMapPanel();
        GetUIManager().HideRewardPanel();
        GetUIManager().RewardGridPanel?.Hide();
        GetUIManager().HideSlotSelect();
        GetUIManager().MagicModifierSelectionPanel?.Hide();
		enemyModels.Clear();
		battleManager.ClearEnemies();
		enemyViewStates.Clear();
		if ((Object)enemyArea != (Object)null)
		{
			for (int num = ((Transform)enemyArea).childCount - 1; num >= 0; num--)
				Object.Destroy((Object)((Component)((Transform)enemyArea).GetChild(num)).gameObject);
		}
		if ((Object)eventPanel != (Object)null)
		{
			eventPanel.Close();
			eventPanel = null;
		}
		RebuildCards(animateFromCurrent: true);
        RefreshStaticUI();
		RefreshMaterialListPanel();
		GetUIManager().ShowShopPanel(level);
		refreshUsedThisTurn = false;
		busy = true;
		SetButtonsInteractable(interactable: false);
	}

	private EventPanelUI GetOrCreateEventPanel()
	{
		EventPanelUI panel = GetComponent<EventPanelUI>();
		if (panel == null)
			panel = gameObject.AddComponent<EventPanelUI>();
		return panel;
	}

	private void StartRestLevel(LevelData level)
	{
		GameLog.Data("Start rest level=" + level.id);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameplayMusic();
		ResetBattleDeckState();
		currentLevel = level;
		HideMapPanel();
		enemyModels.Clear();
		battleManager.ClearEnemies();
		enemyViewStates.Clear();
		if ((Object)enemyArea != (Object)null)
		{
			for (int num = ((Transform)enemyArea).childCount - 1; num >= 0; num--)
				Object.Destroy((Object)((Component)((Transform)enemyArea).GetChild(num)).gameObject);
		}

		if ((Object)eventPanel != (Object)null)
			eventPanel.Close();
		eventPanel = GetOrCreateEventPanel();
		Transform transform = ((Component)this).transform;
		eventPanel.Initialize((RectTransform)((transform is RectTransform) ? transform : null), GetDefaultFont(), DrawRestOptionsHand);
		currentEvent = CreateRestEventModel(level);
		eventPanel.Bind(currentEvent);
		refreshUsedThisTurn = false;
		busy = false;
		SetButtonsInteractable(interactable: true);
	}

	private void StartRewardLevel(LevelData level)
	{
		GameLog.Data("Start reward level=" + level.id);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameplayMusic();
		ResetBattleDeckState();
		currentLevel = level;
		currentEvent = null;
		HideMapPanel();
		enemyModels.Clear();
		battleManager.ClearEnemies();
		enemyViewStates.Clear();
		if ((Object)enemyArea != (Object)null)
		{
			for (int num = ((Transform)enemyArea).childCount - 1; num >= 0; num--)
				Object.Destroy((Object)((Component)((Transform)enemyArea).GetChild(num)).gameObject);
		}
		if ((Object)eventPanel != (Object)null)
		{
			eventPanel.Close();
			eventPanel = null;
		}

		BonusLevelData bonusData = GetBonusLevelData(level);
		GetUIManager().RewardGridPanel?.ShowNewGrid(bonusData, playerState);
		refreshUsedThisTurn = false;
		playerState.DrawCards(bonusData != null && bonusData.drawCount > 0 ? bonusData.drawCount : 5);
		RefreshStaticUI();
		RefreshMaterialListPanel();
		RebuildCards(animateFromCurrent: true);
		busy = false;
		SetButtonsInteractable(interactable: true);
	}

	private BonusLevelData GetBonusLevelData(LevelData level)
	{
		if (level != null && level.bonusLevelId > 0 && GameDataDatabase.TryGetBonusLevelData(level.bonusLevelId, out BonusLevelData bonusData))
			return bonusData;
		if (level != null && GameDataDatabase.TryGetBonusLevelData(level.numericId, out bonusData))
			return bonusData;
		return null;
	}

	private void StartEventLevel(LevelData level)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected O, but got Unknown
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Expected O, but got Unknown
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Expected O, but got Unknown
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Expected O, but got Unknown
		GameLog.Data("Start event level=" + level.id);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameplayMusic();
		ResetBattleDeckState();
		currentLevel = level;
		HideMapPanel();
		EventData eventData = null;
		if (level.eventPoolId > 0)
			GameDataDatabase.TryGetEventData(level.eventPoolId, out eventData);
		if (eventData == null)
		{
			using (IEnumerator<EventData> enumerator = GameDataDatabase.EventData.Values.GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					eventData = enumerator.Current;
				}
			}
		}
		if (eventData == null)
		{
			return;
		}
		currentEvent = new EventModel(eventData);
		if ((Object)eventPanel != (Object)null)
		{
			eventPanel.Close();
		}
		eventPanel = GetOrCreateEventPanel();
		EventPanelUI eventPanelUI = eventPanel;
		Transform transform = ((Component)this).transform;
		eventPanelUI.Initialize((RectTransform)((transform is RectTransform) ? transform : null), GetDefaultFont(), DrawEventOptionsHand);
		eventPanel.Bind(currentEvent);
		refreshUsedThisTurn = false;
		enemyModels.Clear();
		battleManager.ClearEnemies();
		enemyViewStates.Clear();
		if ((Object)enemyArea != (Object)null)
		{
			for (int num = ((Transform)enemyArea).childCount - 1; num >= 0; num--)
			{
				Object.Destroy((Object)((Component)((Transform)enemyArea).GetChild(num)).gameObject);
			}
		}
		busy = false;
		SetButtonsInteractable(interactable: true);
	}

	private void DrawEventOptionsHand()
	{
		if (currentEvent == null || playerState == null)
		{
			return;
		}

		int drawCount = currentEvent.Data.drawCount > 0 ? currentEvent.Data.drawCount : playerState.DrawCount;
		GameLog.Data($"Draw event options hand count={drawCount}");
		refreshUsedThisTurn = false;
		playerState.DrawCards(drawCount);
		RefreshStaticUI();
		RefreshMaterialListPanel();
		RebuildCards(animateFromCurrent: true);
	}

    private EventModel CreateRestEventModel(LevelData level)
    {
        EventOptionData study = new EventOptionData
        {
            id = "study_magic",
            titleKey = "rest.option.study",
            recipe = CreateRandomRecipe(1),
            resultId = RestStudyResultId
        };
        EventOptionData deepStudy = new EventOptionData
        {
            id = "deep_study_magic",
            titleKey = "rest.option.deep_study",
            recipe = CreateRandomRecipe(2),
            resultId = RestDeepStudyResultId
        };
        EventData data = new EventData
        {
            id = level != null ? level.id : "rest",
            titleKey = level != null ? level.titleKey : "level.rest_001.title",
            startNodeId = "start",
            drawCount = playerState != null ? playerState.DrawCount : 5,
            nodes = new[]
            {
                new EventNodeData
                {
                    id = "start",
                    textKeys = level != null && level.restTextKeys != null ? level.restTextKeys : Array.Empty<string>(),
                    options = new[] { study, deepStudy }
                }
            }
        };
        return new EventModel(data);
    }

    private string CreateRandomRecipe(int length)
    {
        char[] recipe = new char[Mathf.Max(1, length)];
        for (int i = 0; i < recipe.Length; i++)
            recipe[i] = (char)('0' + Random.Range(1, 5));
        return new string(recipe);
    }

    private void DrawRestOptionsHand()
    {
        if (playerState == null)
            return;

        GameLog.Data($"Draw rest options hand count={playerState.DrawCount}");
        refreshUsedThisTurn = false;
        playerState.DrawCards(playerState.DrawCount);
        RefreshStaticUI();
        RefreshMaterialListPanel();
        RebuildCards(animateFromCurrent: true);
    }

	public void OnEnemyLeftClicked(EnemyModel enemy)
	{
		if (enemy != null && !enemy.IsDead && battleManager != null)
		{
			if (TutorialManager != null && !TutorialManager.CanToggleFocus(enemy))
				return;

			if (battleManager.FocusTarget == enemy)
			{
				battleManager.ClearFocusTarget();
			}
			else
			{
				battleManager.SetFocusTarget(enemy);
			}
			RefreshEnemyUI();
			TutorialManager?.OnFocusTargetChanged(battleManager.FocusTarget);
		}
	}

	public void ShowBuffTooltip(BuffSlotView slot, BuffModel buff)
	{
		GetUIManager().ShowBuffTooltip(slot, buff);
	}

	public void HideBuffTooltip(BuffSlotView slot)
	{
		GetUIManager().HideBuffTooltip(slot);
	}

    public void ShowModifierTooltip(HandCardView cardView, MaterialModel materialModel)
    {
        if (cardView != null)
            GetUIManager().MaterialListPanel?.ShowModifierTooltip(cardView.RectTransform, materialModel);
    }

    public void HideModifierTooltip(HandCardView cardView)
    {
        if (cardView != null)
            GetUIManager().MaterialListPanel?.HideModifierTooltip(cardView.RectTransform);
    }

	private Image EnsureFocusMarker(RectTransform enemyView)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Expected O, but got Unknown
		Transform val = ((Transform)enemyView).Find("FocusMarker");
		Image image = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<Image>() : null);
		if ((Object)image == (Object)null)
		{
			image = new GameObject("FocusMarker", new Type[3]
			{
				typeof(RectTransform),
				typeof(CanvasRenderer),
				typeof(Image)
			}).GetComponent<Image>();
			((Component)image).transform.SetParent((Transform)enemyView, false);
		}
		image.color = new Color(1f, 0.08f, 0.06f, 0.92f);
		image.raycastTarget = false;
		RectTransform component = ((Component)image).GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.5f, 0.5f);
		component.anchorMax = new Vector2(0.5f, 0.5f);
		component.pivot = new Vector2(0.5f, 0.5f);
		component.anchoredPosition = new Vector2(0f, 40f);
		component.sizeDelta = new Vector2(34f, 34f);
		((Transform)component).localEulerAngles = new Vector3(0f, 0f, 45f);
		((Component)image).gameObject.SetActive(false);
		return image;
	}

	private RectTransform EnsureBuffRoot(RectTransform parent, Vector2 anchoredPosition)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		Transform val = ((Transform)parent).Find("BuffRoot");
		RectTransform val2 = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<RectTransform>() : null);
		if ((Object)val2 == (Object)null)
		{
			val2 = new GameObject("BuffRoot", new Type[2]
			{
				typeof(RectTransform),
				typeof(GridLayoutGroup)
			}).GetComponent<RectTransform>();
			((Transform)val2).SetParent((Transform)parent, false);
		}
		val2.anchorMin = new Vector2(0.5f, 0.5f);
		val2.anchorMax = new Vector2(0.5f, 0.5f);
		val2.pivot = new Vector2(0.5f, 0.5f);
		val2.anchoredPosition = anchoredPosition;
		ConfigureBuffRootLayout(val2);
		return val2;
	}

	private void ConfigureBuffRootLayout(RectTransform root)
	{
		if ((Object)root == (Object)null)
			return;

		float slotSize = BuffSlotSize;
		float slotSpacing = BuffSlotSpacing;
		root.sizeDelta = new Vector2(slotSize * BuffRootColumnCount + slotSpacing * (BuffRootColumnCount - 1), slotSize * BuffRootRowCount + slotSpacing * (BuffRootRowCount - 1));
		GridLayoutGroup grid = ((Component)root).GetComponent<GridLayoutGroup>();
		if ((Object)grid == (Object)null)
			return;

		grid.cellSize = new Vector2(slotSize, slotSize);
		grid.spacing = new Vector2(slotSpacing, slotSpacing);
		grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		grid.constraintCount = BuffRootColumnCount;
	}

	private float BuffSlotSize => Mathf.Max(1f, buffSlotSize);

	private float BuffSlotSpacing => Mathf.Max(0f, buffSlotSpacing);

	private void RefreshBuffRoot(RectTransform root, IReadOnlyDictionary<BuffEnum, BuffModel> buffs, CombatantModel owner)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		if ((Object)root == (Object)null)
		{
			return;
		}
		ConfigureBuffRootLayout(root);
		int num = 0;
		foreach (BuffModel value in buffs.Values)
		{
			if (value.stack > 0)
			{
				BuffSlotView slot = FindBuffSlot(root, value.buffType, num);
				if ((Object)slot == (Object)null)
					slot = GetOrCreateBuffSlot(root, num);

				((Transform)slot.RectTransform).SetSiblingIndex(num);
				((Component)slot).gameObject.SetActive(true);
				slot.SetLayoutSize(BuffSlotSize);
				slot.Bind(value, this);
				num++;
			}
		}
		for (int i = ((Transform)root).childCount - 1; i >= num; i--)
		{
			BuffSlotView slot = ((Component)((Transform)root).GetChild(i)).GetComponent<BuffSlotView>();
			if ((Object)slot != (Object)null && slot.BuffType != BuffEnum.None)
			{
				slot.PlayRemoveMotion(() =>
				{
					if ((Object)slot != (Object)null)
						((Component)slot).gameObject.SetActive(false);
				});
			}
			else
			{
				((Component)((Transform)root).GetChild(i)).gameObject.SetActive(false);
			}
		}
	}

	private BuffSlotView FindBuffSlot(RectTransform root, BuffEnum buffType, int startIndex)
	{
		for (int i = startIndex; i < ((Transform)root).childCount; i++)
		{
			BuffSlotView slot = ((Component)((Transform)root).GetChild(i)).GetComponent<BuffSlotView>();
			if ((Object)slot != (Object)null && slot.BuffType == buffType)
				return slot;
		}
		return null;
	}

	private BuffSlotView GetOrCreateBuffSlot(RectTransform root, int index)
	{
		while (((Transform)root).childCount <= index)
		{
			CreateBuffSlot(root);
		}
		return ((Component)((Transform)root).GetChild(index)).GetComponent<BuffSlotView>();
	}

	private BuffSlotView CreateBuffSlot(RectTransform parent)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		RectTransform val = null;
		if ((Object)buffSlotPrefab != (Object)null)
		{
			val = Object.Instantiate<RectTransform>(buffSlotPrefab, (Transform)parent);
		}
		if ((Object)val == (Object)null)
		{
			val = CreateRuntimeBuffSlot(parent);
		}
		BuffSlotView slot = ((Component)val).GetComponent<BuffSlotView>();
		if ((Object)slot != (Object)null)
			slot.SetLayoutSize(BuffSlotSize);
		return slot;
	}

	private RectTransform CreateRuntimeBuffSlot(RectTransform parent)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Expected O, but got Unknown
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Expected O, but got Unknown
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		Image component = new GameObject("BuffSlot", new Type[4]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(Image),
			typeof(BuffSlotView)
		}).GetComponent<Image>();
		((Component)component).transform.SetParent((Transform)parent, false);
		component.color = new Color(0.08f, 0.08f, 0.1f, 0.86f);
		RectTransform component2 = ((Component)component).GetComponent<RectTransform>();
		component2.sizeDelta = new Vector2(BuffSlotSize, BuffSlotSize);
		Image component3 = new GameObject("Icon", new Type[3]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(Image)
		}).GetComponent<Image>();
		((Component)component3).transform.SetParent((Transform)component2, false);
		component3.raycastTarget = false;
		RectTransform component4 = ((Component)component3).GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = Vector2.one;
		float iconPadding = Mathf.Max(2f, BuffSlotSize * 0.08f);
		component4.offsetMin = new Vector2(iconPadding, iconPadding);
		component4.offsetMax = new Vector2(-iconPadding, -iconPadding);
		TMP_Text component5 = new GameObject("StackText", new Type[3]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(TextMeshProUGUI)
		}).GetComponent<TMP_Text>();
		((Component)component5).transform.SetParent((Transform)component2, false);
		component5.font = GetDefaultFont();
		component5.fontSize = Mathf.Max(12f, BuffSlotSize * 0.5f);
		component5.fontStyle = FontStyles.Bold;
		component5.color = Color.white;
		component5.alignment = TextAlignmentOptions.BottomRight;
		component5.raycastTarget = false;
		RectTransform component6 = ((Component)component5).GetComponent<RectTransform>();
		component6.anchorMin = Vector2.zero;
		component6.anchorMax = Vector2.one;
		component6.offsetMin = Vector2.zero;
		component6.offsetMax = new Vector2(-2f, -1f);
		((Component)component).GetComponent<BuffSlotView>().Initialize(component3, component5);
		return component2;
	}

	private TMP_FontAsset GetDefaultFont()
	{
		TMP_Text playerHealthText = GetUIManager().PlayerStatus != null ? UIManager.FindChildComponent<TMP_Text>(GetUIManager().PlayerStatus.transform, "HealthText") : null;
		return playerHealthText != null && playerHealthText.font != null ? playerHealthText.font : UIManager.GetDefaultTMPFont();
	}

	private RectTransform FindChildRect(Transform root, string name)
	{
		Transform obj = (((Object)(object)root != (Object)null) ? root.Find(name) : null);
		return (RectTransform)(object)((obj is RectTransform) ? obj : null);
	}

	private T FindChildComponent<T>(Transform root, string name) where T : Component
	{
		Transform val = (((Object)(object)root != (Object)null) ? root.Find(name) : null);
		if (!((Object)(object)val != (Object)null))
		{
			return default(T);
		}
		return ((Component)val).GetComponent<T>();
	}

	private void BuildDebugMap()
	{
		mapNodes.Clear();
		ChapterData chapter = GetActiveChapter();
		List<LevelData> eventLevels = GetEventLevelsForChapter(chapter);
		int chapterLength = GetActiveChapterLength();
		for (int i = 0; i < chapterLength; i++)
		{
			List<LevelData> candidateLevels = GetLevelsForProgress(chapter, i + 1);
			if (candidateLevels.Count == 0)
				candidateLevels = GetBattleLevels();
			if (candidateLevels.Count == 0)
				return;

			RunMapNodeModel mapNodeModel = new RunMapNodeModel();
			if (i == 0 && GameDataDatabase.TryGetLevelData(DebugRewardLevelNumericId, out LevelData debugRewardLevel))
			{
				mapNodeModel.leftLevel = ChooseRandomMapLevel(candidateLevels);
				mapNodeModel.rightLevel = debugRewardLevel;
				mapNodes.Add(mapNodeModel);
				continue;
			}
			if (i == 0 && TutorialManager != null && TutorialManager.ShouldForceFirstNodeBattles())
			{
				List<LevelData> battleLevels = GetBattleLevels();
				if (battleLevels.Count == 0)
					return;
				mapNodeModel.leftLevel = battleLevels[Random.Range(0, battleLevels.Count)];
				mapNodeModel.rightLevel = battleLevels[Random.Range(0, battleLevels.Count)];
				mapNodes.Add(mapNodeModel);
				continue;
			}
			LevelData fixedLevel = GetFixedLevelForProgress(chapter, i + 1);
			if (fixedLevel != null)
			{
				mapNodeModel.leftLevel = fixedLevel;
				mapNodeModel.rightLevel = fixedLevel;
				mapNodeModel.fixedSingleChoice = true;
			}
			else
			{
				mapNodeModel.leftLevel = ChooseRandomMapLevel(candidateLevels);
				mapNodeModel.rightLevel = ChooseRandomMapLevel(candidateLevels);
			}
			mapNodes.Add(mapNodeModel);
		}
		currentMapNodeIndex = 0;
		RefreshChapterProgressUI();
		GetUIManager().MapPanel?.SetPlayerMarkerNodeIndex(currentMapNodeIndex);
		GameLog.Data($"Build map nodes={mapNodes.Count} randomizedEvents={eventLevels.Count}");
	}

	private LevelData ChooseRandomMapLevel(List<LevelData> levels)
	{
		if (levels == null || levels.Count == 0)
			return null;

		EconomyConfigData economy = GameDataDatabase.GetDefaultEconomyConfig();
		int totalWeight = 0;
		for (int i = 0; i < levels.Count; i++)
			totalWeight += GetMapLevelWeight(levels[i], economy);

		if (totalWeight <= 0)
			return levels[Random.Range(0, levels.Count)];

		int roll = Random.Range(0, totalWeight);
		for (int i = 0; i < levels.Count; i++)
		{
			int weight = GetMapLevelWeight(levels[i], economy);
			if (weight <= 0)
				continue;
			if (roll < weight)
				return levels[i];
			roll -= weight;
		}
		return levels[levels.Count - 1];
	}

	private int GetMapLevelWeight(LevelData level, EconomyConfigData economy)
	{
		if (level == null)
			return 0;

		int weight = level.levelType == LevelType.Shop ? economy?.shopMapLevelWeight ?? 1 : economy?.defaultMapLevelWeight ?? 1;
		return Mathf.Max(0, weight);
	}

	private LevelData GetFixedLevelForProgress(ChapterData chapter, int progress)
	{
		if (chapter == null || chapter.fixed_level == null)
			return null;

		for (int i = 0; i < chapter.fixed_level.Length; i++)
		{
			ChapterFixedLevelData fixedLevel = chapter.fixed_level[i];
			if (fixedLevel == null || fixedLevel.levelIndex != progress)
				continue;

			List<LevelData> levels = GetLevels(fixedLevel.levelType);
			return levels.Count > 0 ? levels[Random.Range(0, levels.Count)] : null;
		}

		return null;
	}

	private ChapterData GetActiveChapter()
	{
		if (activeChapter != null)
			return activeChapter;

		foreach (ChapterData chapter in GameDataDatabase.ChapterData.Values)
		{
			if (chapter != null)
			{
				activeChapter = chapter;
				return activeChapter;
			}
		}
		return null;
	}

	private int GetActiveChapterLength()
	{
		ChapterData chapter = GetActiveChapter();
		return chapter != null && chapter.levelLength > 0 ? chapter.levelLength : RunNodeCount;
	}

	private List<LevelData> GetLevelsForProgress(ChapterData chapter, int progress)
	{
		int[] poolIds = GetLevelPoolIdsForProgress(chapter, progress);
		if (poolIds == null || poolIds.Length == 0)
			return GetBattleLevels();

		List<LevelData> levels = new List<LevelData>();
		for (int i = 0; i < poolIds.Length; i++)
		{
			if (GameDataDatabase.TryGetLevelData(poolIds[i], out LevelData level) && (level.levelType == LevelType.Battle || level.levelType == LevelType.Event || level.levelType == LevelType.Shop || level.levelType == LevelType.Rest || level.levelType == LevelType.Reward))
				levels.Add(level);
		}
		return levels;
	}

	private int[] GetLevelPoolIdsForProgress(ChapterData chapter, int progress)
	{
		if (chapter == null)
			return null;

		if (chapter.levelPoolRanges != null)
		{
			for (int i = 0; i < chapter.levelPoolRanges.Length; i++)
			{
				ChapterLevelPoolRangeData range = chapter.levelPoolRanges[i];
				if (range != null && progress >= range.startProgress && progress <= range.endProgress)
					return range.levelPoolIds;
			}
		}

		return chapter.levelPoolIds;
	}

	private List<LevelData> GetEventLevelsForChapter(ChapterData chapter)
	{
		List<LevelData> eventLevels = new List<LevelData>();
		if (chapter != null && chapter.eventPoolIds != null && chapter.eventPoolIds.Length > 0)
		{
			for (int i = 0; i < chapter.eventPoolIds.Length; i++)
			{
				if (GameDataDatabase.TryGetLevelData(chapter.eventPoolIds[i], out LevelData level) && level.levelType == LevelType.Event)
					eventLevels.Add(level);
			}
		}

		if (eventLevels.Count == 0)
			eventLevels.AddRange(GetEventLevels());
		return eventLevels;
	}

	private void RefreshChapterProgressUI()
	{
		GetUIManager().ChapterProgress?.SetProgress(Mathf.Min(currentMapNodeIndex + 1, mapNodes.Count), mapNodes.Count);
	}

	private void ToggleMapPanel()
	{
		GetUIManager().ToggleMapPanel();
	}

	private void ShowMapPanel(bool focusCurrentNode, TweenCallback onComplete, bool animateMarker)
	{
		GetUIManager().ShowMapPanel(focusCurrentNode, onComplete, animateMarker);
	}

	private void HideMapPanel()
	{
		GetUIManager().HideMapPanel();
	}

	private void CreateTopBar()
	{
		GetUIManager();
	}

	private static void SetTopBarChildAnchors(RectTransform rect, Vector2 anchor)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)rect == (Object)null))
		{
			rect.anchorMin = anchor;
			rect.anchorMax = anchor;
		}
	}

	private void ToggleSettingsPanel()
	{
		GetUIManager().ToggleSettingsPanel();
	}

	private void Awake()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected O, but got Unknown
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Expected O, but got Unknown
        RunSaveData saveData = null;
        bool continueSavedRun = PlayerState.ContinueSavedRun && !RunSaveSystem.ConsumeForceNewRun();
        if (continueSavedRun)
            saveData = RunSaveSystem.LoadCurrentRun();
        PlayerState.ContinueSavedRun = false;
        loadedRunPlaySeconds = saveData != null ? Mathf.Max(0f, saveData.totalPlaySeconds) : 0f;
        runStartRealtime = Time.realtimeSinceStartup;

		playerState = saveData != null ? RunSaveSystem.CreatePlayer(saveData) : PlayerState.CreateDefault();
		battleManager = BattleManager.Create(playerState);
        battleManager.EnemyAdded += OnBattleEnemyAdded;
		((UnityEvent)refreshButton.onClick).AddListener(new UnityAction(RefreshSelectedCards));
		((UnityEvent)endTurnButton.onClick).AddListener(new UnityAction(EndTurn));
		GetUIManager();
		EnsureMaterialListButton();
			CreateTopBar();
        if (saveData != null)
        {
            activeChapter = null;
            if (saveData.chapterNumericId > 0)
                GameDataDatabase.TryGetChapterData(saveData.chapterNumericId, out activeChapter);
            RunSaveSystem.RestoreMapNodes(saveData, mapNodes);
            currentMapNodeIndex = Mathf.Clamp(saveData.currentMapNodeIndex, 0, Mathf.Max(0, mapNodes.Count - 1));
            RefreshChapterProgressUI();
            GetUIManager().MapPanel?.SetPlayerMarkerNodeIndex(currentMapNodeIndex);
        }
        else
        {
		    BuildDebugMap();
        }
		CreateMagicViews();
		CreateParticleCaster();
		CreatePlayerCastAnimator();
		InitializePlayerUiComponents();
		RebuildCards(animateFromCurrent: true);
        SaveRunProgress();
        if (RunSaveSystem.ShouldAutoStartSavedNode(saveData))
        {
            LevelData savedLevel = RunSaveSystem.GetSavedCurrentLevel(saveData);
            if (savedLevel != null)
                StartLevel(savedLevel);
            else
                ShowLevelSelect();
        }
        else
        {
		    ShowLevelSelect();
        }
	}

	private void InitializePlayerUiComponents()
	{
		UIManager manager = GetUIManager();
        manager.PlayerStatus?.Setup(playerState, true);
        playerBuffRoot = manager.PlayerStatus?.BuffRoot;
        manager.GoldDisplay?.SetGold(playerState.Gold, true);
        manager.PlayArea?.UpdateContinuousCastCounter(0, true);
	}

	private void Update()
	{
		if (Input.GetMouseButtonDown(1))
		{
			PlaySelectedCardsByInput();
		}

		if (currentLevel != null && currentLevel.levelType == LevelType.Rest && eventPanel != null && eventPanel.WaitingForFinalClick && Input.GetMouseButtonDown(0))
		{
			FinishRestLevel();
		}
		else if (currentEvent != null && eventPanel != null && eventPanel.WaitingForFinalClick && Input.GetMouseButtonDown(0))
		{
			FinishEventLevel();
		}
	}

	private void OnDestroy()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected O, but got Unknown
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Expected O, but got Unknown
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Expected O, but got Unknown
        SaveRunProgress();
        if (battleManager != null)
        {
            battleManager.EnemyAdded -= OnBattleEnemyAdded;
            BattleManager.ClearInstance(battleManager);
        }
		((UnityEvent)refreshButton.onClick).RemoveListener(new UnityAction(RefreshSelectedCards));
		((UnityEvent)endTurnButton.onClick).RemoveListener(new UnityAction(EndTurn));
		if ((Object)(object)deckPileArea != (Object)null)
		{
			Button component = ((Component)deckPileArea).GetComponent<Button>();
			if ((Object)(object)component != (Object)null)
			{
				((UnityEvent)component.onClick).RemoveListener(new UnityAction(ToggleMaterialListPanel));
			}
		}
		DOTween.Kill((object)this, false);
		for (int i = 0; i < enemyViewStates.Count; i++)
		{
			Tween healthNumberTween = enemyViewStates[i].healthNumberTween;
			if (healthNumberTween != null)
			{
				TweenExtensions.Kill(healthNumberTween, false);
			}
		}
	}

	public void SetSpellCastEffect(ISpellCastEffect effect)
	{
		spellCastEffect = effect;
	}

	public void OnCardLeftClicked(HandCardView cardView)
	{
		if (busy && !choosingEventCard)
		{
			return;
		}
		if (choosingEventCard)
		{
			HandleEventCardChoice(cardView);
			return;
		}
		if (cardView.InPlayZone)
		{
			if (playerState.TryMovePlayCardToHand(cardView.Card))
			{
				RebuildCards(animateFromCurrent: true);
			}
			return;
		}
		bool flag = !cardView.Selected;
		cardView.SetSelected(flag, instant: false);
		if (flag)
		{
			if (!selectedCards.Contains(cardView.Card))
			{
				selectedCards.Add(cardView.Card);
			}
		}
		else
		{
			selectedCards.Remove(cardView.Card);
		}
	}

	public void OnCardPlayRequested(HandCardView cardView)
	{
		if (!busy && !cardView.InPlayZone)
		{
			PlayCard(cardView.Card);
		}
	}

	private void HandleEventCardChoice(HandCardView cardView)
	{
		if (cardView == null || cardView.InPlayZone || !playerState.Hand.Contains(cardView.Card))
			return;

		if (pendingChoiceCards.Contains(cardView.Card))
		{
			pendingChoiceCards.Remove(cardView.Card);
			cardView.SetSelected(false, instant: false);
			return;
		}

		pendingChoiceCards.Add(cardView.Card);
		cardView.SetSelected(true, instant: false);
		if (pendingChoiceCards.Count < pendingChoiceCount)
			return;

		ResolveEventCardChoice();
	}

	private void ResolveEventCardChoice()
	{
		EventOptionData option = pendingChoiceOption;
		List<MaterialModel> choiceCards = new List<MaterialModel>(pendingChoiceCards);
		choosingEventCard = false;
		pendingChoiceOption = null;
		pendingChoiceCards.Clear();
		pendingChoiceCount = 0;

		if (option == null)
		{
			busy = false;
			SetButtonsInteractable(interactable: true);
			return;
		}

		if (option.resultId == 100)
		{
			for (int i = 0; i < choiceCards.Count; i++)
				playerState.RemoveCardEverywhere(choiceCards[i]);
		}
		else
		{
			for (int i = 0; i < choiceCards.Count; i++)
			{
				MaterialModifierModel modifier = EventModel.CreateModifierForResult(option.resultId);
				if (modifier != null)
					choiceCards[i].AddModifier(modifier);
			}
		}

		selectedCards.Clear();
		RefreshStaticUI();
		RefreshMaterialListPanel();
		((MonoBehaviour)this).StartCoroutine(FinishEventCardChoiceRoutine(option));
	}

	private IEnumerator FinishEventCardChoiceRoutine(EventOptionData option)
	{
		List<HandCardView> views = new List<HandCardView>();
		for (int i = 0; i < cardViews.Count; i++)
			views.Add(cardViews[i]);

		List<MaterialModel> removedTemporaryCards = new List<MaterialModel>();
		playerState.EndTurn(removedTemporaryCards);
		RefreshStaticUI();
		bool returnDone = false;
		AnimateReturningViews(views, removedTemporaryCards, deckPileArea, (TweenCallback)delegate
		{
			returnDone = true;
		});
		while (!returnDone)
			yield return null;

		RebuildCards(animateFromCurrent: true);
		refreshUsedThisTurn = false;
		CompleteEventChoiceOption(option);
	}

	private void CompleteEventChoiceOption(EventOptionData option)
	{
		if (currentEvent != null && currentEvent.AdvanceToNextNode(option))
		{
			if ((Object)eventPanel != (Object)null)
				((MonoBehaviour)this).StartCoroutine(CompleteEventChoiceNodeRoutine());
			else
			{
				busy = false;
				SetButtonsInteractable(interactable: true);
			}
		}
		else
		{
			FinishEventLevel();
		}
	}

	private IEnumerator CompleteEventChoiceNodeRoutine()
	{
		yield return eventPanel.ShowCurrentNodeRoutine();
		busy = false;
		SetButtonsInteractable(interactable: true);
	}

	public void PlaySelectedCardsByInput()
	{
		if (!busy)
		{
			PlaySelectedCards();
		}
	}

	private void PlaySelectedCards()
	{
		if (selectedCards.Count == 0)
		{
			return;
		}
		bool flag = false;
		for (int i = 0; i < playerState.Hand.Count; i++)
		{
			MaterialModel materialModel = playerState.Hand[i];
			if (selectedCards.Contains(materialModel) && (TutorialManager == null || TutorialManager.CanMoveCardToPlay(materialModel, playerState.PlayZone)))
			{
				flag |= playerState.TryMoveHandCardToPlay(materialModel);
				i--;
			}
		}
		selectedCards.Clear();
		if (flag)
		{
			RebuildCards(animateFromCurrent: true);
		}
	}

	private void PlayCard(MaterialModel card)
	{
		if (TutorialManager != null && !TutorialManager.CanMoveCardToPlay(card, playerState.PlayZone))
			return;

		if (playerState.TryMoveHandCardToPlay(card))
		{
			selectedCards.Remove(card);
			RebuildCards(animateFromCurrent: true);
		}
	}

	private void RefreshSelectedCards()
	{
		RefreshSelectedCards(ignoreOncePerTurn: false);
	}

	private void RefreshSelectedCards(bool ignoreOncePerTurn)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Expected O, but got Unknown
		if (TutorialManager != null && TutorialManager.TutorialBattleRunning && !TutorialManager.CanRefreshSelected(selectedCards))
		{
			return;
		}
		if (busy || (refreshUsedThisTurn && !ignoreOncePerTurn && playerState.GetBuffStack(BuffEnum.ExtraRefresh) <= 0) || selectedCards.Count == 0)
		{
			return;
		}
		List<HandCardView> list = new List<HandCardView>();
		for (int i = 0; i < selectedCards.Count; i++)
		{
			HandCardView handCardView = FindView(selectedCards[i]);
			if ((Object)handCardView != (Object)null)
			{
				handCardView.SetSelected(value: false, instant: false);
				list.Add(handCardView);
			}
		}
		List<MaterialModel> list2 = new List<MaterialModel>();
		PlayerState.RefreshHandResult refreshResult;
		if (TutorialManager != null && TutorialManager.ShouldForceRefreshEarth(out MaterialEnum forcedMaterial))
		{
			playerState.ReturnHandCardsToDrawPile(selectedCards, list2);
			playerState.Hand.Add(new MaterialModel(System.Guid.NewGuid().ToString("N"), forcedMaterial));
			refreshResult = new PlayerState.RefreshHandResult(1, 1);
		}
		else
		{
			refreshResult = playerState.RefreshHandCards(selectedCards, list2, battleManager);
		}
		selectedCards.Clear();
		if (refreshResult.DrawnCount == 0 && refreshResult.ReturnedCount == 0 && list2.Count == 0)
		{
			return;
		}
		if (!ignoreOncePerTurn)
		{
			if (refreshUsedThisTurn)
			{
				playerState.UseExtraRefreshChance();
			}
			else
			{
				refreshUsedThisTurn = true;
			}
		}
		busy = true;
		SetButtonsInteractable(interactable: false);
		RefreshStaticUI();
		UpdateLayout();
		AnimateReturningViews(list, list2, deckPileArea, (TweenCallback)delegate
		{
			RefreshStaticUI();
			RefreshMaterialListPanel();
			RebuildCards(animateFromCurrent: true);
			TutorialManager?.OnRefreshCompleted(playerState);
			busy = false;
			SetButtonsInteractable(interactable: true);
		});
	}

	private void EndTurn()
	{
		if (runEnded)
			return;

		if (!busy)
		{
			if (TutorialManager != null && !TutorialManager.CanEndTurn(playerState.PlayZone))
				return;

			GameLog.Data((currentEvent != null) ? "Click end turn in event" : "Click end turn in battle");
			selectedCards.Clear();
			if (currentLevel != null && currentLevel.levelType == LevelType.Rest)
			{
				((MonoBehaviour)this).StartCoroutine(ResolveRestEndTurnRoutine());
			}
			else if (currentLevel != null && currentLevel.levelType == LevelType.Reward)
			{
				((MonoBehaviour)this).StartCoroutine(ResolveRewardEndTurnRoutine());
			}
			else if (currentEvent != null)
			{
				((MonoBehaviour)this).StartCoroutine(ResolveEventEndTurnRoutine());
			}
			else
			{
				((MonoBehaviour)this).StartCoroutine(ResolveEndTurnRoutine());
			}
		}
	}

	private IEnumerator ResolveEndTurnRoutine()
	{
		busy = true;
		SetButtonsInteractable(interactable: false);
		ResetMagicHighlights();
		MaterialModifierModel.CurrentContext = new MaterialModifierContext { PlayerState = playerState, BattleManager = battleManager };
		playerState.TriggerMaterialBegin();
		List<MaterialModel> list = new List<MaterialModel>(playerState.PlayZone);
		if (list.Count > 0)
		{
			yield return PlayResolveAnimation(list);
			GetUIManager().PlayArea?.HideResolveIndicator();
			ResetContinuousCastCounterUI();
			if (AllEnemiesDead())
			{
				yield return FinishBattleRoutine();
				yield break;
			}
		}
		List<HandCardView> list2 = new List<HandCardView>();
		for (int i = 0; i < cardViews.Count; i++)
		{
			list2.Add(cardViews[i]);
		}
		int playerHealthBefore = playerState.CurrentHealth;
		int playerShieldBefore = playerState.Shield;
		List<MaterialModel> removedTemporaryCards = new List<MaterialModel>();
		playerState.TriggerOnTurnEnd(new CombatantModel(GetFirstAliveEnemy()));
        TriggerMagicTurnEnd();
		playerState.EndTurn(removedTemporaryCards);
		playerState.TriggerAfterTurnEnd(new CombatantModel(GetFirstAliveEnemy()));
		playerState.TriggerMaterialEnd();
		MaterialModifierModel.CurrentContext = null;
		RefreshStaticUI();
		bool discardDone = false;
		AnimateReturningViews(list2, removedTemporaryCards, GetDiscardPileArea(), (TweenCallback)delegate
		{
			discardDone = true;
		});
		while (!discardDone)
		{
			yield return null;
		}
		bool enemyTurnBannerShown = false;
		int enemyTurnCount = enemyModels.Count;
		for (int num = 0; num < enemyTurnCount; num++)
		{
			EnemyModel enemy = enemyModels[num];
			if (enemy.IsDead)
				continue;

			if (!enemyTurnBannerShown)
			{
				enemyTurnBannerShown = true;
				GetUIManager().TurnBanner?.Show("敌方回合");
			}

			yield return ResolveEnemyIntentsRoutine(enemy);
			if (HasAliveEnemyAfter(num))
				yield return new WaitForSeconds(enemyBetweenDelay);
		}
		RefreshStaticUI();
		if (CheckPlayerDefeated())
			yield break;
		GetUIManager().PlayerFeedback?.UpdateVignetteRange(playerState);
		RefreshStaticUI();
		RefreshEnemyUI();
		if (AllEnemiesDead())
		{
			yield return FinishBattleRoutine();
			yield break;
		}
		if (enemyTurnBannerShown && enemyTurnEndDelay > 0f)
			yield return new WaitForSeconds(enemyTurnEndDelay);
		ResetMagicHighlights();
		GetUIManager().PlayArea?.HideResolveIndicator();
		BeginPlayerTurn(playerState.DrawCount);
		refreshUsedThisTurn = false;
		busy = false;
		SetButtonsInteractable(interactable: true);
	}

	private IEnumerator ResolveRestEndTurnRoutine()
    {
        busy = true;
        SetButtonsInteractable(interactable: false);
        List<MaterialModel> playSnapshot = new List<MaterialModel>(playerState.PlayZone);
        EventOptionData matchedOption = null;
        bool matched = currentEvent != null && currentEvent.TryGetMatchedOption(playSnapshot, out matchedOption);
        GameLog.Data(string.Format("Resolve rest end turn matched={0} option={1}", matched, (matchedOption != null) ? matchedOption.id : "none"));
        if (matched && (Object)eventPanel != (Object)null)
        {
            yield return eventPanel.PlayOptionChosen(matchedOption);
            RectTransform matchedOptionRect = eventPanel.MatchedOptionRect;
            for (int i = 0; i < playSnapshot.Count; i++)
            {
                HandCardView handCardView = FindView(playSnapshot[i]);
                if ((Object)handCardView != (Object)null && (Object)matchedOptionRect != (Object)null)
                    PlayMaterialFillParticle(handCardView, matchedOptionRect, playSnapshot[i].material);
            }
            yield return (object)new WaitForSeconds(GetParticleArrivalWait());
        }

        List<HandCardView> views = new List<HandCardView>();
        for (int i = 0; i < cardViews.Count; i++)
            views.Add(cardViews[i]);
        List<MaterialModel> removedTemporaryCards = new List<MaterialModel>();
        playerState.EndTurn(removedTemporaryCards);
        RefreshStaticUI();
        bool returnDone = false;
        AnimateReturningViews(views, removedTemporaryCards, deckPileArea, (TweenCallback)delegate
        {
            returnDone = true;
        });
        while (!returnDone)
            yield return null;

        RebuildCards(animateFromCurrent: true);
        refreshUsedThisTurn = false;
        if (matched && matchedOption != null && (matchedOption.resultId == RestStudyResultId || matchedOption.resultId == RestDeepStudyResultId))
        {
            ShowMagicModifierSelection(matchedOption.resultId == RestDeepStudyResultId ? 3 : 2);
            yield break;
        }

        ApplyRestDefaultHeal();
        FinishRestLevel();
    }

    private IEnumerator ResolveRewardEndTurnRoutine()
    {
        busy = true;
        SetButtonsInteractable(interactable: false);
        List<MaterialModel> playSnapshot = new List<MaterialModel>(playerState.PlayZone);
        RewardGridPanelUI panel = GetUIManager().RewardGridPanel;
        if (panel != null)
            yield return panel.ResolvePathRoutine(playSnapshot, playerState);

        List<HandCardView> views = new List<HandCardView>();
        for (int i = 0; i < cardViews.Count; i++)
            views.Add(cardViews[i]);

        List<MaterialModel> removedTemporaryCards = new List<MaterialModel>();
        playerState.EndTurn(removedTemporaryCards);
        RefreshStaticUI();
        bool returnDone = false;
        AnimateReturningViews(views, removedTemporaryCards, deckPileArea, (TweenCallback)delegate
        {
            returnDone = true;
        });
        while (!returnDone)
            yield return null;

        RebuildCards(animateFromCurrent: true);
        refreshUsedThisTurn = false;
        if (CheckPlayerDefeated())
            yield break;

        FinishRewardLevel();
    }

	private IEnumerator ResolveEventEndTurnRoutine()
	{
		busy = true;
		SetButtonsInteractable(interactable: false);
		List<MaterialModel> playSnapshot = new List<MaterialModel>(playerState.PlayZone);
		EventOptionData matchedOption = null;
		bool matched = currentEvent != null && currentEvent.TryGetMatchedOption(playSnapshot, out matchedOption);
		if (matched)
			TutorialManager?.OnEventOptionResolved();
		if (!matched && currentEvent != null)
			matched = currentEvent.TryGetExitOption(out matchedOption);
		GameLog.Data(string.Format("Resolve event end turn matched={0} option={1}", matched, (matchedOption != null) ? matchedOption.id : "none"));
		if (matched && (Object)eventPanel != (Object)null)
		{
			yield return eventPanel.PlayOptionChosen(matchedOption);
			RectTransform matchedOptionRect = eventPanel.MatchedOptionRect;
			for (int i = 0; i < playSnapshot.Count; i++)
			{
				HandCardView handCardView = FindView(playSnapshot[i]);
				if ((Object)handCardView != (Object)null && (Object)matchedOptionRect != (Object)null)
				{
					PlayMaterialFillParticle(handCardView, matchedOptionRect, playSnapshot[i].material);
				}
			}
			yield return (object)new WaitForSeconds(GetParticleArrivalWait());
			currentEvent.ResolveResult(matchedOption.resultId, playerState);
			GameLog.Data($"Event option selected id={matchedOption.id} result={matchedOption.resultId}");
			RefreshStaticUI();
		}
        if (matched && IsCardChoiceEventResult(matchedOption.resultId))
        {
            StartEventCardChoice(matchedOption);
            yield break;
        }
		List<HandCardView> list = new List<HandCardView>();
		for (int j = 0; j < cardViews.Count; j++)
		{
			list.Add(cardViews[j]);
		}
		List<MaterialModel> removedTemporaryCards = new List<MaterialModel>();
		playerState.EndTurn(removedTemporaryCards);
		RefreshStaticUI();
		bool returnDone = false;
		AnimateReturningViews(list, removedTemporaryCards, deckPileArea, (TweenCallback)delegate
		{
			returnDone = true;
		});
		while (!returnDone)
		{
			yield return null;
		}
		RebuildCards(animateFromCurrent: true);
		refreshUsedThisTurn = false;
		if (!matched)
		{
			if ((Object)eventPanel != (Object)null && eventPanel.WaitingForFinalClick)
			{
				FinishEventLevel();
			}
			else
			{
				busy = false;
				SetButtonsInteractable(interactable: true);
			}
		}
		else if (currentEvent.AdvanceToNextNode(matchedOption))
		{
			if ((Object)eventPanel != (Object)null)
			{
				yield return eventPanel.ShowCurrentNodeRoutine();
			}
			busy = false;
			SetButtonsInteractable(interactable: true);
		}
		else
		{
			FinishEventLevel();
		}
	}

	private bool IsCardChoiceEventResult(int resultId)
	{
		return resultId == 100 || EventModel.CreateModifierForResult(resultId) != null;
	}

	private void StartEventCardChoice(EventOptionData option)
	{
		pendingChoiceOption = option;
		pendingChoiceCount = option != null && option.choiceCount > 0 ? option.choiceCount : 1;
		pendingChoiceCards.Clear();
		selectedCards.Clear();
		choosingEventCard = true;
		MaterialListPanelUI panel = GetUIManager().MaterialListPanel;
		if ((Object)panel != (Object)null)
		{
			panel.BeginSelection(pendingChoiceCount, IsEventChoiceSelectable, OnEventMaterialListSelectionCompleted);
		}
		else
		{
			RefreshMaterialListPanel();
			RebuildCards(animateFromCurrent: true);
		}
		SetButtonsInteractable(interactable: false);
	}

	private bool IsEventChoiceSelectable(MaterialModel materialModel)
	{
        if (materialModel == null || playerState == null)
            return false;

        if (pendingChoiceOption != null && pendingChoiceOption.resultId == 100)
            return playerState.Deck.Contains(materialModel);

		return playerState.Hand.Contains(materialModel);
	}

	private void OnEventMaterialListSelectionCompleted(IReadOnlyList<MaterialModel> materials)
	{
		pendingChoiceCards.Clear();
		for (int i = 0; materials != null && i < materials.Count; i++)
		{
			if (IsEventChoiceSelectable(materials[i]))
				pendingChoiceCards.Add(materials[i]);
		}
		if (pendingChoiceCards.Count >= pendingChoiceCount)
			ResolveEventCardChoice();
	}

	private void FinishRewardLevel()
	{
		StartCoroutine(FinishRewardLevelRoutine());
	}

    private IEnumerator FinishRewardLevelRoutine()
    {
        RewardGridPanelUI rewardGridPanel = GetUIManager().RewardGridPanel;
        if (rewardGridPanel != null)
            yield return rewardGridPanel.HideRoutine();
		GameLog.Data("Finish reward grid level");
		FinishReward();
    }

	private void FinishEventLevel()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		if ((Object)eventPanel != (Object)null)
		{
			eventPanel.Close();
			eventPanel = null;
		}
		currentEvent = null;
		GameLog.Data("Finish event level");
		FinishReward();
	}

	private void FinishRestLevel()
	{
		if ((Object)eventPanel != (Object)null)
		{
			eventPanel.Close();
			eventPanel = null;
		}
		GameLog.Data("Finish rest level");
		FinishReward();
	}

	private IEnumerator PlayResolveAnimation(List<MaterialModel> cards)
	{
		if (GetUIManager().PlayArea == null || GetUIManager().PlayArea.ResolveIndicator == null)
		{
			yield break;
		}
		GetUIManager().PlayArea.ShowResolveIndicator();
		for (int roundStart = 0; roundStart < cards.Count; roundStart++)
		{
			ResetMagicHighlights();
			MagicItemView matchedMagicView = FindCastableMagicFromLeft(cards, roundStart);
			if ((Object)matchedMagicView == (Object)null)
			{
				continue;
			}
			int matchLength = GetRecipeLength(matchedMagicView.Magic);
			MoveIndicatorToCardRange(cards, roundStart, matchLength, roundStart == 0);
			yield return (object)new WaitForSeconds(layoutDuration * 0.65f);
			for (int i = 0; i < matchLength; i++)
			{
				HandCardView handCardView = FindView(cards[roundStart + i]);
				if ((Object)handCardView != (Object)null)
				{
					TweenSettingsExtensions.SetTarget<Tweener>(ShortcutExtensions.DOPunchPosition((Transform)handCardView.RectTransform, Vector3.up * materialCardPunchStrength, materialCardPunchDuration, materialCardPunchVibrato, materialCardPunchElasticity, false), (object)this);
					PlayMaterialFillParticle(handCardView, matchedMagicView, cards[roundStart + i].material);
				}
			}
			yield return (object)new WaitForSeconds(GetParticleArrivalWait());
            for (int j = 0; j < matchLength; j++)
            {
                cards[roundStart + j].TriggerOnInvoke();
                matchedMagicView.HighlightRecipeSlot(j);
            }
            EnemyModel targetEnemy = battleManager.BeginCastTarget();
            bool castImpactReached = false;
            float castVisualFallbackWait = PlayMagicCastParticle(matchedMagicView, targetEnemy, () => castImpactReached = true);
            float castVisualWaitTime = 0f;
            while (!castImpactReached && castVisualWaitTime < castVisualFallbackWait)
            {
                castVisualWaitTime += Time.deltaTime;
                yield return null;
            }
			matchedMagicView.PulseCast();
			GameLog.Data($"Resolve magic from play zone magic={matchedMagicView.Magic.Id} start={roundStart} length={matchLength}");
			CastMagic(matchedMagicView.Magic);
			yield return PlayPendingEnemyDeaths();
			if (AllEnemiesDead())
			{
				yield break;
			}
			yield return (object)new WaitForSeconds(postMagicResolveDelay);
			ResetMagicHighlights();
		}
	}

	private MagicItemView FindCastableMagicFromLeft(List<MaterialModel> cards, int startIndex)
	{
		for (int i = 0; i < magicViews.Count; i++)
		{
			MagicItemView magicItemView = magicViews[i];
			if (magicItemView.Magic != null && magicItemView.Magic.IsMatch(cards, startIndex))
			{
				return magicItemView;
			}
		}
		return null;
	}

	private static int GetRecipeLength(MagicModel magic)
	{
		if (magic.Data.matchRule == MagicMatchRule.AnyTwoDifferentElements)
		{
			return 2;
		}
		if (magic.Data.recipe == null)
		{
			return 0;
		}
		return magic.Data.recipe.Length;
	}

	private float GetParticleArrivalWait()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		ArcParticleImpactTester arcParticleImpactTester = spellCastEffect as ArcParticleImpactTester;
		if (!((Object)arcParticleImpactTester != (Object)null))
		{
			return 0.43f;
		}
		return arcParticleImpactTester.BurstDuration;
	}

	private float GetCastParticleImpactStartWait()
	{
		ArcParticleImpactTester arcParticleImpactTester = spellCastEffect as ArcParticleImpactTester;
		if (!((Object)arcParticleImpactTester != (Object)null))
		{
			return 0.43f;
		}
		return arcParticleImpactTester.SingleProjectileImpactDelay;
	}

	private void RebuildCards(bool animateFromCurrent)
	{
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Expected O, but got Unknown
		for (int num = cardViews.Count - 1; num >= 0; num--)
		{
			HandCardView view = cardViews[num];
			if (!playerState.Hand.Contains(view.Card) && !playerState.PlayZone.Contains(view.Card))
			{
				cardViews.RemoveAt(num);
				AnimateCardToArea(view, GetDiscardPileArea(), GetAreaCenterWorldPosition(GetDiscardPileArea()), 90f, (TweenCallback)delegate
				{
					//IL_000b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0015: Expected O, but got Unknown
					Object.Destroy((Object)((Component)view).gameObject);
				});
			}
		}
		for (int num2 = 0; num2 < playerState.Hand.Count; num2++)
		{
			EnsureCardView(playerState.Hand[num2], inPlayZone: false, animateFromCurrent);
		}
		for (int num3 = 0; num3 < playerState.PlayZone.Count; num3++)
		{
			EnsureCardView(playerState.PlayZone[num3], inPlayZone: true, animateFromCurrent);
		}
		RefreshStaticUI();
		RefreshEnemyUI();
		RefreshMaterialListPanel();
		UpdateLayout(!animateFromCurrent);
	}

	private void EnsureCardView(MaterialModel card, bool inPlayZone, bool animateFromCurrent)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		HandCardView handCardView = FindView(card);
		if ((Object)handCardView != (Object)null)
		{
			handCardView.SetInPlayZone(inPlayZone);
		}
		else
		{
			CreateCardView(card, inPlayZone, animateFromCurrent);
		}
	}

	private void CreateCardView(MaterialModel card, bool inPlayZone, bool animateFromDeck)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		RectTransform val = (inPlayZone ? playArea : handArea);
		RectTransform val2 = Object.Instantiate<RectTransform>(cardPrefab, (Transform)val);
		((Component)val2).gameObject.SetActive(true);
		HandCardView component = ((Component)val2).GetComponent<HandCardView>();
		component.Initialize(this);
		component.Bind(card, inPlayZone);
		if (animateFromDeck)
		{
			((Transform)val2).position = GetAreaCenterWorldPosition(deckPileArea);
			((Transform)val2).localEulerAngles = new Vector3(0f, 0f, 90f);
			newCardViews.Add(component);
		}
		cardViews.Add(component);
	}

	private void UpdateLayout(bool instant = false)
	{
		LayoutArea(playerState.Hand, handArea, playZone: false, instant);
		LayoutArea(playerState.PlayZone, playArea, playZone: true, instant);
	}

	private void LayoutArea(List<MaterialModel> cards, RectTransform area, bool playZone, bool instant)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		//IL_0085: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Expected O, but got Unknown
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Expected O, but got Unknown
		//IL_0104: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Expected O, but got Unknown
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		int count = cards.Count;
		float num = (playZone ? playCardSpacing : cardSpacing);
		float num2 = ((count > 1) ? ((0f - num) * (float)(count - 1) * 0.5f) : 0f);
		Vector2 val = default(Vector2);
		for (int i = 0; i < count; i++)
		{
			HandCardView handCardView = FindView(cards[i]);
			if (!((Object)handCardView == (Object)null))
			{
				if ((Object)((Transform)handCardView.RectTransform).parent != (Object)area)
				{
					((Transform)handCardView.RectTransform).SetParent((Transform)area, true);
				}
				val = new Vector2(num2 + num * (float)i, 0f);
				handCardView.SetInPlayZone(playZone);
				if (instant)
				{
					((Transform)handCardView.RectTransform).SetParent((Transform)area, false);
					handCardView.RectTransform.anchoredPosition = val;
					handCardView.SetBaseRotation(0f, instant: true);
				}
				else
				{
					bool animateFromExistingView = (Object)((Transform)handCardView.RectTransform).parent == (Object)area && !newCardViews.Remove(handCardView);
					AnimateCardToLayoutTarget(handCardView, area, val, animateFromExistingView);
				}
			}
		}
	}

	private void AnimateCardToLayoutTarget(HandCardView view, RectTransform targetParent, Vector2 targetAnchoredPosition, bool animateFromExistingView)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Expected O, but got Unknown
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Expected O, but got Unknown
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Expected O, but got Unknown
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Expected O, but got Unknown
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Expected O, but got Unknown
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Expected O, but got Unknown
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Expected O, but got Unknown
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected O, but got Unknown
		if (!animateFromExistingView)
		{
			ShortcutExtensions.DOKill((Component)view.RectTransform, false);
			Sequence obj = DOTween.Sequence();
			TweenSettingsExtensions.Join(obj, (Tween)TweenSettingsExtensions.SetEase<TweenerCore<Vector2, Vector2, VectorOptions>>(view.RectTransform.DOAnchorPos(targetAnchoredPosition, layoutDuration), layoutEase));
			TweenSettingsExtensions.Join(obj, (Tween)TweenSettingsExtensions.SetEase<TweenerCore<Quaternion, Vector3, QuaternionOptions>>(ShortcutExtensions.DOLocalRotate((Transform)view.RectTransform, Vector3.zero, layoutDuration, (RotateMode)0), layoutEase));
			TweenSettingsExtensions.SetTarget<Sequence>(obj, (object)this);
			return;
		}
		Vector3 position = ((Transform)view.RectTransform).position;
		Quaternion rotation = ((Transform)view.RectTransform).rotation;
		ShortcutExtensions.DOKill((Component)view.RectTransform, false);
		((Transform)view.RectTransform).SetParent((Transform)targetParent, true);
		((Transform)view.RectTransform).position = position;
		((Transform)view.RectTransform).rotation = rotation;
		Vector2 anchoredPosition = view.RectTransform.anchoredPosition;
		view.RectTransform.anchoredPosition = targetAnchoredPosition;
		Vector3 position2 = ((Transform)view.RectTransform).position;
		view.RectTransform.anchoredPosition = anchoredPosition;
		((Transform)view.RectTransform).position = position;
		((Transform)view.RectTransform).rotation = rotation;
		Sequence obj2 = DOTween.Sequence();
		TweenSettingsExtensions.Join(obj2, (Tween)TweenSettingsExtensions.SetEase<TweenerCore<Vector3, Vector3, VectorOptions>>(ShortcutExtensions.DOMove((Transform)view.RectTransform, position2, layoutDuration, false), layoutEase));
		TweenSettingsExtensions.Join(obj2, (Tween)TweenSettingsExtensions.SetEase<TweenerCore<Quaternion, Vector3, QuaternionOptions>>(ShortcutExtensions.DOLocalRotate((Transform)view.RectTransform, Vector3.zero, layoutDuration, (RotateMode)0), layoutEase));
		TweenSettingsExtensions.SetTarget<Sequence>(obj2, (object)this);
		TweenSettingsExtensions.OnComplete<Sequence>(obj2, (TweenCallback)delegate
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Expected O, but got Unknown
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)view != (Object)null)
			{
				view.RectTransform.anchoredPosition = targetAnchoredPosition;
				((Transform)view.RectTransform).localEulerAngles = Vector3.zero;
			}
		});
	}

	private HandCardView FindView(MaterialModel card)
	{
		for (int i = 0; i < cardViews.Count; i++)
		{
			if (cardViews[i].Card == card)
			{
				return cardViews[i];
			}
		}
		return null;
	}

	private void RefreshStaticUI()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		GetUIManager().PlayerStatus?.Refresh(playerState, false);
        GetUIManager().GoldDisplay?.SetGold(playerState.Gold, true);
		RefreshBuffRoot(playerBuffRoot, playerState.Buffs, null);
		if ((Object)(object)deckCountText != (Object)null)
		{
			deckCountText.text = "素材列表";
		}
	}

	private void EnsureMaterialListButton()
	{
		if (deckPileArea == null)
		{
			return;
		}
		Button button = deckPileArea.GetComponent<Button>();
		if (button == null)
		{
			button = deckPileArea.gameObject.AddComponent<Button>();
		}
		button.onClick.RemoveListener(ToggleMaterialListPanel);
		button.onClick.AddListener(ToggleMaterialListPanel);
		AddJuicyMotion((Transform)(object)deckPileArea);
	}

	private void ToggleMaterialListPanel()
	{
		GetUIManager().ToggleMaterialListPanel();
	}

	private void RefreshMaterialListPanel()
	{
		GetUIManager().RefreshMaterialListPanel();
	}

	private void CreateEnemyHealthView(RectTransform enemyView)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected O, but got Unknown
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Expected O, but got Unknown
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Expected O, but got Unknown
		if ((Object)enemyView == (Object)null)
		{
			return;
		}
		Transform val = ((Transform)enemyView).Find("HealthText");
		if ((Object)val != (Object)null)
		{
			enemyHealthText = ((Component)val).GetComponent<TMP_Text>();
		}
		Transform shieldTextTransform = ((Transform)enemyView).Find("ShieldText");
		if ((Object)shieldTextTransform != (Object)null)
		{
			TMP_Text shieldText = ((Component)shieldTextTransform).GetComponent<TMP_Text>();
			if ((Object)shieldText != (Object)null)
				shieldText.text = string.Empty;
			((Component)shieldTextTransform).gameObject.SetActive(false);
		}
		SetupHealthText(enemyHealthText);
		if ((Object)enemyHealthFill == (Object)null)
		{
			return;
		}
		Transform parent = ((Transform)((Component)enemyHealthFill).GetComponent<RectTransform>()).parent;
		RectTransform val2 = (RectTransform)((parent is RectTransform) ? parent : null);
		if (!((Object)val2 == (Object)null))
		{
			val2.anchorMin = new Vector2(0.5f, 0.5f);
			val2.anchorMax = new Vector2(0.5f, 0.5f);
			val2.pivot = new Vector2(0.5f, 0.5f);
			val2.anchoredPosition = new Vector2(0f, -56f);
			val2.sizeDelta = new Vector2(150f, 14f);
			SetupFillImage(enemyHealthFill, new Color(0.82f, 0.05f, 0.04f, 1f), 1);
			enemyHealthBufferFill = CreateHealthFillLayer(val2, "HealthBufferFill", Color.white, 0);
			enemyShieldFill = CreateHealthFillLayer(val2, "ShieldFill", new Color(0.2f, 0.55f, 1f, 1f), 2);
			SetHealthLayerOrder(enemyHealthBufferFill, enemyHealthFill, enemyShieldFill);
			HealthBarUI.PositionHealthTextRightOfBar(enemyHealthText, val2, EnemyHealthTextWidth);
		}
	}

	private Image CreateHealthFillLayer(RectTransform parent, string name, Color color, int siblingIndex)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		Transform val = ((Transform)parent).Find(name);
		Image image = (((Object)val != (Object)null) ? ((Component)val).GetComponent<Image>() : null);
		bool flag = (Object)image == (Object)null;
		if (flag)
		{
			image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
			((Component)image).transform.SetParent((Transform)parent, false);
		}
		((Component)image).transform.SetParent((Transform)parent, false);
		Image parentImage = ((Component)parent).GetComponent<Image>();
		if ((Object)parentImage != (Object)null)
		{
			if (flag)
			{
				image.sprite = parentImage.sprite;
				image.type = parentImage.type;
				image.fillCenter = parentImage.fillCenter;
				image.preserveAspect = parentImage.preserveAspect;
				image.material = parentImage.material;
				image.maskable = parentImage.maskable;
				image.pixelsPerUnitMultiplier = parentImage.pixelsPerUnitMultiplier;
			}
			else if ((Object)image.sprite == (Object)null)
			{
				image.sprite = parentImage.sprite;
			}
		}
		SetupFillImage(image, color, siblingIndex);
		return image;
	}

	private static void SetupFillImage(Image image, Color color, int siblingIndex)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)image == (Object)null))
		{
			image.color = color;
			image.raycastTarget = false;
			image.fillAmount = 1f;
			((Component)image).transform.SetSiblingIndex(siblingIndex);
			SetRectHorizontalRange(((Component)image).GetComponent<RectTransform>(), 0f, 1f);
		}
	}

	private static void SetHealthLayerOrder(Image bufferFill, Image healthFillImage, Image shieldFill)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		if ((Object)bufferFill != (Object)null)
		{
			((Component)bufferFill).transform.SetSiblingIndex(0);
		}
		if ((Object)healthFillImage != (Object)null)
		{
			((Component)healthFillImage).transform.SetSiblingIndex(1);
		}
		if ((Object)shieldFill != (Object)null)
		{
			((Component)shieldFill).transform.SetSiblingIndex(2);
		}
	}

	private void SetupHealthText(TMP_Text text)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		if (!((Object)text == (Object)null))
		{
			HealthBarUI.SetupHealthText(text, enemyHealthTextFontSize);
		}
	}

	private static void PositionHealthTextLeftOfBar(TMP_Text text, RectTransform barBack, float width)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected O, but got Unknown
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)text == (Object)null) && !((Object)barBack == (Object)null))
		{
			RectTransform component = ((Component)text).GetComponent<RectTransform>();
			component.anchorMin = barBack.anchorMin;
			component.anchorMax = barBack.anchorMin;
			component.pivot = new Vector2(1f, 0.5f);
			component.sizeDelta = new Vector2(width, barBack.rect.height + 8f);
			component.anchoredPosition = barBack.anchoredPosition + new Vector2(-8f, 0f);
		}
	}

	private void UpdatePlayerHealthUI(bool instant = false)
	{
		GetUIManager().PlayerStatus?.Refresh(playerState, instant);
	}

	private void UpdateEnemyHealthUI(bool instant = false)
	{
		UpdateHealthBar(enemyHealthFill, enemyHealthBufferFill, enemyShieldFill, enemyModel.CurrentHealth, enemyModel.Data.maxHealth, enemyModel.Shield, instant);
		Tween val = enemyHealthNumberTween;
		if (val != null)
		{
			TweenExtensions.Kill(val, false);
		}
		HealthBarUI.SetHealthTextColor(enemyHealthText, enemyModel.Shield > 0);
		enemyHealthNumberTween = UpdateHealthText(enemyHealthText, displayedEnemyHealth, HealthBarUI.GetHealthTextValue(enemyModel.CurrentHealth, enemyModel.Shield), instant, delegate(int value)
		{
			displayedEnemyHealth = value;
		});
	}

	private void UpdateHealthBar(Image healthFillImage, Image bufferFillImage, Image shieldFillImage, int currentHealth, int maxHealth, int shield, bool instant)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Expected O, but got Unknown
		if (!((Object)healthFillImage == (Object)null) && maxHealth > 0)
		{
			float totalMax = (float)(maxHealth + Mathf.Max(0, shield));
			float num = Mathf.Clamp01((float)(currentHealth + Mathf.Max(0, shield)) / totalMax);
			float num2 = Mathf.Clamp01((float)Mathf.Max(0, shield) / totalMax);
			float num3 = (shield > 0) ? Mathf.Max(0f, num - num2) : num;
			AnimateHorizontalRange(((Component)healthFillImage).GetComponent<RectTransform>(), 0f, num, enemyHealthFillDuration, instant);
			SetImageAlpha(healthFillImage, (currentHealth > 0 || shield > 0) ? 1f : 0f);
			if ((Object)shieldFillImage != (Object)null)
			{
				SetRectHorizontalRange(((Component)shieldFillImage).GetComponent<RectTransform>(), num3, num);
				SetImageAlpha(shieldFillImage, (shield > 0 && num > num3) ? 1f : 0f);
			}
			if (!((Object)bufferFillImage == (Object)null))
			{
				float rectHorizontalEnd = GetRectHorizontalEnd(((Component)bufferFillImage).GetComponent<RectTransform>());
                float end = num;
				float duration = ((num < rectHorizontalEnd) ? enemyHealthBufferDecreaseDuration : enemyHealthBufferIncreaseDuration);
				AnimateHorizontalRange(((Component)bufferFillImage).GetComponent<RectTransform>(), 0f, end, duration, instant);
				SetImageAlpha(bufferFillImage, (currentHealth > 0 || shield > 0) ? 1f : 0f);
			}
		}
	}

	private static float GetRectHorizontalEnd(RectTransform rect)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)rect != (Object)null))
		{
			return 0f;
		}
		return rect.anchorMax.x;
	}

	private static void SetRectHorizontalRange(RectTransform rect, float start, float end)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)rect == (Object)null))
		{
			rect.anchorMin = new Vector2(Mathf.Clamp01(start), 0f);
			rect.anchorMax = new Vector2(Mathf.Clamp01(end), 1f);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}
	}

	private static void AnimateHorizontalRange(RectTransform rect, float start, float end, float duration, bool instant)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)rect == (Object)null))
		{
			ShortcutExtensions.DOKill((Component)rect, false);
			start = Mathf.Clamp01(start);
			end = Mathf.Clamp01(end);
			if (instant)
			{
				SetRectHorizontalRange(rect, start, end);
				return;
			}
			TweenSettingsExtensions.SetEase<TweenerCore<Vector2, Vector2, VectorOptions>>(rect.DOAnchorMin(new Vector2(start, 0f), duration), (Ease)9);
			TweenSettingsExtensions.SetEase<TweenerCore<Vector2, Vector2, VectorOptions>>(rect.DOAnchorMax(new Vector2(end, 1f), duration), (Ease)9);
		}
	}

	private Tween UpdateHealthText(TMP_Text text, int displayedHealth, int targetHealth, bool instant, Action<int> setDisplayedHealth)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Expected O, but got Unknown
		if ((Object)text == (Object)null)
		{
			return null;
		}
		if (instant)
		{
			setDisplayedHealth(targetHealth);
			text.text = targetHealth.ToString();
			return null;
		}
		return (Tween)TweenSettingsExtensions.SetTarget<Tweener>(TweenSettingsExtensions.SetEase<Tweener>(DOVirtual.Int(displayedHealth, targetHealth, enemyHealthTextDuration, (TweenCallback<int>)delegate(int value)
		{
			setDisplayedHealth(value);
			text.text = value.ToString();
		}), enemyHealthEase), (object)this);
	}

	private static void SetImageAlpha(Image image, float alpha)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		Color color = image.color;
		color.a = alpha;
		image.color = color;
	}

	private static void AddJuicyMotion(Transform target)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		if (!((Object)target == (Object)null) && !((Object)((Component)target).GetComponent<JuicyMotion>() != (Object)null))
		{
			((Component)target).gameObject.AddComponent<JuicyMotion>();
		}
	}

	private static void AddJuicyMotionToImmediateUiChildren(Transform root)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		if ((Object)root == (Object)null)
		{
			return;
		}
		for (int i = 0; i < root.childCount; i++)
		{
			Transform child = root.GetChild(i);
			Graphic component = ((Component)child).GetComponent<Graphic>();
			if ((Object)component != (Object)null && component.raycastTarget)
			{
				AddJuicyMotion(child);
			}
		}
	}

	private void CreateMagicViews()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		if (!((Object)magicBookArea == (Object)null))
		{
			magicViews.Clear();
			MagicItemView[] componentsInChildren = ((Component)magicBookArea).GetComponentsInChildren<MagicItemView>(true);
			for (int i = 0; i < componentsInChildren.Length && i < playerState.MagicBookSlotCount; i++)
			{
				((Component)componentsInChildren[i]).gameObject.SetActive(true);
				AddJuicyMotion(((Component)componentsInChildren[i]).transform);
				MagicSlotClickHandler clickHandler = ((Component)componentsInChildren[i]).GetComponent<MagicSlotClickHandler>();
				if (clickHandler == null)
					clickHandler = ((Component)componentsInChildren[i]).gameObject.AddComponent<MagicSlotClickHandler>();
				clickHandler.Bind(this, i);
				componentsInChildren[i].Bind(playerState.GetMagicAtSlot(i));
				magicViews.Add(componentsInChildren[i]);
			}
		}
	}

	private void OnBattleEnemyAdded(EnemyModel enemy)
    {
        if (enemy == null || enemyModels.Contains(enemy))
            return;

        enemyModels.Add(enemy);
        if ((Object)enemyArea == (Object)null || (Object)enemyViewPrefab == (Object)null)
            return;

        CreateEnemyView(enemy, enemyViewStates.Count, enemyModels.Count);
        LayoutEnemyViews();
        RefreshEnemyUI((RectTransform)null, true);
    }

    private void ClearEnemyViews()
    {
        for (int i = 0; i < enemyViewStates.Count; i++)
        {
            Tween healthNumberTween = enemyViewStates[i].healthNumberTween;
            if (healthNumberTween != null)
                TweenExtensions.Kill(healthNumberTween, false);
        }
        enemyViewStates.Clear();
        enemyModel = null;
        enemyViewRect = null;
        enemyHealthFill = null;
        enemyIntentIcon = null;
        enemyBodyImage = null;
        if ((Object)enemyArea != (Object)null)
        {
            for (int num = ((Transform)enemyArea).childCount - 1; num >= 0; num--)
                Object.Destroy((Object)((Component)((Transform)enemyArea).GetChild(num)).gameObject);
        }
    }

    private void LayoutEnemyViews()
    {
        int visibleCount = 0;
        int automaticPositionCount = 0;
        for (int i = 0; i < enemyViewStates.Count; i++)
        {
            EnemyViewState state = enemyViewStates[i];
            if (state == null || (Object)state.viewRect == (Object)null || !((Component)state.viewRect).gameObject.activeSelf)
                continue;

            visibleCount++;
            if (state.model == null || !state.model.HasSpawnPosition)
                automaticPositionCount++;
        }

        int automaticPositionIndex = 0;
        for (int i = 0; i < enemyViewStates.Count; i++)
        {
            EnemyViewState state = enemyViewStates[i];
            if (state == null || (Object)state.viewRect == (Object)null || !((Component)state.viewRect).gameObject.activeSelf)
                continue;

            if (state.model != null && state.model.HasSpawnPosition)
            {
                state.viewRect.anchoredPosition = new Vector2(state.model.SpawnPositionX, state.model.SpawnPositionY);
            }
            else
            {
                state.viewRect.anchoredPosition = new Vector2(((float)automaticPositionIndex - (float)(automaticPositionCount - 1) * 0.5f) * 250f, 0f);
                automaticPositionIndex++;
            }
            ((Transform)state.viewRect).localScale = visibleCount >= 3 ? Vector3.one * 0.88f : Vector3.one;
        }
    }

	private void CreateEnemyViews()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected O, but got Unknown
		if (!((Object)enemyArea == (Object)null) && !((Object)enemyViewPrefab == (Object)null))
		{
            ClearEnemyViews();
			int count = enemyModels.Count;
			for (int i = 0; i < count; i++)
			{
				CreateEnemyView(enemyModels[i], i, count);
			}
			enemyModel = GetFirstAliveEnemy();
            LayoutEnemyViews();
			RefreshEnemyUI((RectTransform)null, true);
		}
	}

	private void CreateEnemyView(EnemyModel model, int index, int count)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Expected O, but got Unknown
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Expected O, but got Unknown
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		RectTransform val = Object.Instantiate<RectTransform>(enemyViewPrefab, (Transform)enemyArea);
		((Component)val).gameObject.SetActive(true);
        AddJuicyMotion((Transform)val);
        JuicyMotion enemyMotion = ((Component)val).GetComponent<JuicyMotion>();
        if ((Object)enemyMotion != (Object)null)
            enemyMotion.SetHoverTiltAngle(0f);
		val.anchoredPosition = model.HasSpawnPosition
			? new Vector2(model.SpawnPositionX, model.SpawnPositionY)
			: new Vector2(((float)index - (float)(count - 1) * 0.5f) * 250f, 0f);
		((Transform)val).localScale = ((count >= 3) ? (Vector3.one * 0.88f) : Vector3.one);
		EnemyViewState enemyViewState = new EnemyViewState();
		enemyViewState.model = model;
		enemyViewState.viewRect = val;
		Image[] componentsInChildren = ((Component)val).GetComponentsInChildren<Image>(true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (((Object)componentsInChildren[i]).name == "HealthFill")
			{
				enemyViewState.healthFill = componentsInChildren[i];
			}
			else if (((Object)componentsInChildren[i]).name == "IntentIcon")
			{
				enemyViewState.intentIcon = componentsInChildren[i];
			}
			else if (((Object)componentsInChildren[i]).name == "Body")
			{
				enemyViewState.bodyImage = componentsInChildren[i];
			}
		}
		if ((Object)enemyViewState.bodyImage != (Object)null)
		{
			Sprite val2 = LoadEnemyIcon(model.Data.iconName);
			if ((Object)(object)val2 != (Object)null)
			{
				enemyViewState.bodyImage.sprite = val2;
				enemyViewState.bodyImage.color = Color.white;
			}
			enemyViewState.bodyBaseColor = enemyViewState.bodyImage.color;
		}
		enemyModel = model;
		enemyViewRect = val;
		enemyHealthFill = enemyViewState.healthFill;
		enemyIntentIcon = enemyViewState.intentIcon;
		enemyBodyImage = enemyViewState.bodyImage;
		enemyBodyBaseColor = enemyViewState.bodyBaseColor;
		EnsureEnemyIntentView(val);
		enemyViewState.intentIcon = enemyIntentIcon;
		CreateEnemyHealthView(val);
		enemyViewState.healthBufferFill = enemyHealthBufferFill;
		enemyViewState.shieldFill = enemyShieldFill;
        enemyViewState.healthText = enemyHealthText;
        enemyViewState.displayedHealth = HealthBarUI.GetHealthTextValue(model.CurrentHealth, model.Shield);
		EnemyViewClickHandler enemyViewClickHandler = ((Component)val).GetComponent<EnemyViewClickHandler>();
		if ((Object)enemyViewClickHandler == (Object)null)
		{
			enemyViewClickHandler = ((Component)val).gameObject.AddComponent<EnemyViewClickHandler>();
		}
		enemyViewClickHandler.Bind(this, model);
		enemyViewState.focusMarker = EnsureFocusMarker(val);
		enemyViewState.buffRoot = EnsureBuffRoot(val, new Vector2(0f, -82f));
		RebuildEnemyIntentViews(enemyViewState);
		enemyViewStates.Add(enemyViewState);
	}

	private static Sprite LoadEnemyIcon(string iconName)
	{
		if (string.IsNullOrEmpty(iconName))
		{
			return null;
		}
		return Resources.Load<Sprite>("Images/Enemies/" + iconName);
	}

	private void CreateParticleCaster()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		Transform val = ((Component)this).transform.Find("SpellParticleCaster");
		ArcParticleImpactTester arcParticleImpactTester = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<ArcParticleImpactTester>() : null);
		if (!((Object)arcParticleImpactTester == (Object)null))
		{
			spellCastEffect = arcParticleImpactTester;
			arcParticleImpactTester.SetTestPlayback(playOnStart: false, loop: false);
		}

		Transform emitter = ((Component)this).transform.Find("SpellParticleEmitter");
		spellParticleEmitter = ((Object)(object)emitter != (Object)null) ? (RectTransform)emitter : null;
	}

	private void CreatePlayerCastAnimator()
	{
		Transform target = ((Component)this).transform.Find("PlayerCastAnimator");
		playerCastAnimator = ((Object)(object)target != (Object)null) ? ((Component)target).GetComponent<PlayerCastAnimatorUI>() : null;
		if (!((Object)playerCastAnimator == (Object)null))
		{
			playerCastAnimator.Initialize();
			playerCastAnimator.SetReleaseHandler(HandleCastReleaseFrame);
		}
	}

	private bool PlayPlayerCastAnimation()
	{
		if ((Object)playerCastAnimator == (Object)null)
			CreatePlayerCastAnimator();
		if ((Object)playerCastAnimator == (Object)null)
			return false;

		playerCastAnimator.PlayCast();
		return true;
	}

    private void PlayPlayerCastSwingSfx(int continuousCastCount)
    {
        if (AudioManager.Instance == null)
            return;

        if (continuousCastCount <= 0)
            continuousCastCount = battleManager != null ? battleManager.ContinuousCastCount + 1 : 1;

        float pitch = playerCastSwingPitchBase + Mathf.Max(0, continuousCastCount - 1) * playerCastSwingPitchIncrease;
        pitch = Mathf.Min(pitch, playerCastSwingPitchMax);
        AudioManager.Instance.PlaySfx(GameSfxId.PlayerCastSwing, pitch);
    }

	private void QueueCastParticle(MagicModel magic, RectTransform target, Action onImpact)
	{
		pendingCastParticleMagic = magic;
		pendingCastParticleTarget = target;
        pendingCastParticleTargets.Clear();
		pendingCastParticleImpactHandler = onImpact;
	}

    private void QueueCastParticles(MagicModel magic, IReadOnlyList<RectTransform> targets, Action onImpact)
    {
        pendingCastParticleMagic = magic;
        pendingCastParticleTarget = null;
        pendingCastParticleTargets.Clear();
        for (int i = 0; targets != null && i < targets.Count; i++)
        {
            RectTransform target = targets[i];
            if ((Object)target != (Object)null)
                pendingCastParticleTargets.Add(target);
        }
        pendingCastParticleImpactHandler = onImpact;
    }

	private void HandleCastReleaseFrame()
	{
		int shakeCount = pendingCastShakeCount;
		pendingCastShakeCount = 0;
        PlayPlayerCastSwingSfx(shakeCount);
		PlayCastScreenShake(shakeCount);
		PlayPendingCastParticle();
	}

	private void PlayCastScreenShake(int continuousCastCount)
	{
		if (battleManager == null)
			return;

		if (continuousCastCount <= 0)
			continuousCastCount = battleManager.ContinuousCastCount;

		GetUIManager().PlayerFeedback?.PlayCastScreenShake(continuousCastCount);
	}

	private void PlayPendingCastParticle()
	{
		MagicModel magic = pendingCastParticleMagic;
		RectTransform target = pendingCastParticleTarget;
        int targetCount = pendingCastParticleTargets.Count;
		Action onImpact = pendingCastParticleImpactHandler;
		pendingCastParticleMagic = null;
		pendingCastParticleTarget = null;
		pendingCastParticleImpactHandler = null;

		if (spellCastEffect == null || magic == null || ((Object)target == (Object)null && targetCount <= 0))
        {
            pendingCastParticleTargets.Clear();
			return;
        }

		if ((Object)spellParticleEmitter == (Object)null)
		{
			Transform emitter = ((Component)this).transform.Find("SpellParticleEmitter");
			spellParticleEmitter = ((Object)(object)emitter != (Object)null) ? (RectTransform)emitter : null;
		}
		RectTransform from = (Object)spellParticleEmitter != (Object)null ? spellParticleEmitter : target;
        if ((Object)from == (Object)null && targetCount > 0)
            from = pendingCastParticleTargets[0];
        if ((Object)from == (Object)null)
        {
            pendingCastParticleTargets.Clear();
            return;
        }

        if (targetCount > 0)
        {
            ISpellCastMultiTargetImpactEffect multiTargetEffect = spellCastEffect as ISpellCastMultiTargetImpactEffect;
            if (multiTargetEffect != null)
            {
                multiTargetEffect.PlayCast(magic, from, pendingCastParticleTargets, SpellEffectTarget.Enemy, onImpact);
            }
            else
            {
                target = pendingCastParticleTargets[0];
                ISpellCastImpactEffect impactEffect = spellCastEffect as ISpellCastImpactEffect;
                if (impactEffect != null)
                    impactEffect.PlayCast(magic, from, target, SpellEffectTarget.Enemy, onImpact);
                else
                    spellCastEffect.PlayCast(magic, from, target, SpellEffectTarget.Enemy);
            }
            pendingCastParticleTargets.Clear();
            return;
        }

		ISpellCastImpactEffect singleImpactEffect = spellCastEffect as ISpellCastImpactEffect;
		if (singleImpactEffect != null)
			singleImpactEffect.PlayCast(magic, from, target, SpellEffectTarget.Enemy, onImpact);
		else
			spellCastEffect.PlayCast(magic, from, target, SpellEffectTarget.Enemy);
	}

	private float GetCastReleaseWait()
	{
		if ((Object)playerCastAnimator == (Object)null)
			CreatePlayerCastAnimator();
		return (Object)playerCastAnimator != (Object)null ? playerCastAnimator.CastReleaseDelay : 0f;
	}

	private void PlayMaterialFillParticle(HandCardView cardView, RectTransform target, MaterialEnum material)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		if (spellCastEffect != null && !((Object)cardView == (Object)null) && !((Object)target == (Object)null))
		{
			spellCastEffect.PlayMaterialFill(cardView.RectTransform, target, material);
		}
	}

	private void PlayMaterialFillParticle(HandCardView cardView, MagicItemView magicView, MaterialEnum material)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		if (!((Object)magicView == (Object)null))
		{
			PlayMaterialFillParticle(cardView, (RectTransform)((Component)magicView).transform, material);
		}
	}

	private float PlayMagicCastParticle(MagicItemView magicView, EnemyModel targetEnemy, Action onImpact)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		if ((Object)magicView == (Object)null || magicView.Magic == null)
		{
			return 0f;
		}

		pendingCastParticleMagic = null;
		pendingCastParticleTarget = null;
        pendingCastParticleTargets.Clear();
        castParticleTargetBuffer.Clear();
		pendingCastParticleImpactHandler = null;
		pendingCastShakeCount = battleManager != null ? battleManager.ContinuousCastCount + 1 : 0;
		if (!magicView.Magic.Data.playPlayerCastAnimation)
		{
			pendingCastShakeCount = 0;
			return 0f;
		}

		bool animationStarted = PlayPlayerCastAnimation();
		float releaseWait = GetCastReleaseWait();
		if (!magicView.Magic.CastParticleTargetsAllEnemies && GetMagicEffectTargetType(magicView.Magic) != SpellEffectTarget.Enemy)
		{
			return releaseWait;
		}

		if (spellCastEffect == null)
		{
			return releaseWait;
		}

        if (magicView.Magic.CastParticleTargetsAllEnemies)
        {
            CollectAliveEnemyTargetRects(castParticleTargetBuffer);
            if (castParticleTargetBuffer.Count == 0)
            {
                return releaseWait;
            }

            QueueCastParticles(magicView.Magic, castParticleTargetBuffer, onImpact);
        }
        else
        {
			RectTransform magicEffectTarget = GetMagicEffectTarget(magicView.Magic, targetEnemy);
			if ((Object)magicEffectTarget == (Object)null)
			{
				return releaseWait;
			}

			QueueCastParticle(magicView.Magic, magicEffectTarget, onImpact);
        }
		if (!animationStarted)
		{
			HandleCastReleaseFrame();
			return GetCastParticleImpactStartWait();
		}
		return releaseWait + GetCastParticleImpactStartWait();
	}

	private IEnumerator FinishBattleRoutine()
	{
		HideContinuousCastCounterUI();
		yield return PlayPendingEnemyDeaths();
        TriggerMagicBattleEnd();
        if (battleManager != null)
            battleManager.ResetContinuousCastCount();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameplayMusic();
		ResetMagicHighlights();
		GetUIManager().PlayArea?.HideResolveIndicator();
		List<HandCardView> views = new List<HandCardView>(cardViews);
		List<MaterialModel> removedTemporaryCards = new List<MaterialModel>();
		playerState.EndTurn(removedTemporaryCards);
        playerState.ClearCombatState();
		bool returnDone = false;
		AnimateReturningViews(views, removedTemporaryCards, deckPileArea, (TweenCallback)delegate
		{
			returnDone = true;
		});
		while (!returnDone)
			yield return null;

		RestoreBattleDeckState();
		RebuildCards(animateFromCurrent: true);
		refreshUsedThisTurn = false;
		busy = true;
		SetButtonsInteractable(interactable: false);
		ShowRewardPanel();
	}

	private void ShowRewardPanel()
	{
		busy = true;
		currentEvent = null;
		SetButtonsInteractable(interactable: false);
        SaveRunProgress();
		GetUIManager().ShowRewardPanel();
	}

    private void SaveRunProgress()
    {
        if (!runEnded)
            RunSaveSystem.SaveCurrentRun(playerState, mapNodes, currentMapNodeIndex, activeChapter ?? GetActiveChapter(), currentLevel, GetCurrentRunPlaySeconds());
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveRunProgress();
    }

    private void OnApplicationQuit()
    {
        SaveRunProgress();
    }

    private void ShowMagicModifierSelection(int choiceCount)
    {
        busy = true;
        SetButtonsInteractable(interactable: false);
        pendingMagicModifier = null;
        GetUIManager().MagicModifierSelectionPanel?.Show(GetMagicModifierChoices(choiceCount), FinishRestLevel);
    }

    private List<MagicModifierData> GetMagicModifierChoices(int count)
    {
        List<MagicModifierData> pool = new List<MagicModifierData>();
        foreach (MagicModifierData data in GameDataDatabase.MagicModifierData.Values)
        {
            if (data != null && data.weight != 0 && CanAnyMagicAcceptModifier(data))
                pool.Add(data);
        }

        List<MagicModifierData> result = new List<MagicModifierData>();
        int attempts = 0;
        while (result.Count < count && pool.Count > 0 && attempts < 40)
        {
            attempts++;
            MagicModifierData data = pool[Random.Range(0, pool.Count)];
            if (!result.Contains(data))
                result.Add(data);
        }
        return result;
    }

    private bool CanAnyMagicAcceptModifier(MagicModifierData data)
    {
        MagicModifierModel modifier = MagicModifierFactory.Create(data);
        if (modifier == null || playerState == null)
            return false;

        for (int i = 0; i < playerState.MagicBook.Count; i++)
        {
            MagicModel magic = playerState.MagicBook[i];
            if (magic != null && magic.CanAddModifier(modifier))
                return true;
        }
        return false;
    }

    private void ApplyRestDefaultHeal()
    {
        int healAmount = Mathf.Max(1, Mathf.CeilToInt(playerState.MaxHealth * RestDefaultHealRatio));
        int healthBefore = playerState.CurrentHealth;
        playerState.Heal(healAmount);
        int healed = playerState.CurrentHealth - healthBefore;
        if (healed > 0)
        {
            PlayPlayerCornerFeedback(new Color(0.1f, 0.95f, 0.25f, 0.48f));
            ShowPlayerFloatingText("+" + healed, FloatingTextType.Heal);
        }
        RefreshStaticUI();
        GameLog.Data($"Rest default heal amount={healed}");
    }

    private void TriggerMagicBattleStart()
    {
        for (int i = 0; i < playerState.MagicBook.Count; i++)
            playerState.MagicBook[i]?.TriggerMagicBattleStart(playerState, battleManager);
    }

    private void TriggerMagicBattleEnd()
    {
        for (int i = 0; i < playerState.MagicBook.Count; i++)
            playerState.MagicBook[i]?.TriggerMagicBattleEnd(playerState, battleManager);
    }

    private void TriggerMagicTurnStart()
    {
        for (int i = 0; i < playerState.MagicBook.Count; i++)
            playerState.MagicBook[i]?.TriggerMagicTurnStart(playerState, battleManager);
    }

    private void TriggerMagicTurnEnd()
    {
        for (int i = 0; i < playerState.MagicBook.Count; i++)
            playerState.MagicBook[i]?.TriggerMagicTurnEnd(playerState, battleManager);
    }

	private void ResetBattleDeckState()
	{
		consumedBattleDeckCards.Clear();
		playerState.Hand.Clear();
		playerState.PlayZone.Clear();
		playerState.DrawPile.Clear();
		playerState.DrawPile.AddRange(playerState.Deck);
		RefreshMaterialListPanel();
	}

	private void RestoreBattleDeckState()
	{
		consumedBattleDeckCards.Clear();
		playerState.Hand.Clear();
		playerState.PlayZone.Clear();
		playerState.DrawPile.Clear();
		playerState.DrawPile.AddRange(playerState.Deck);
		RefreshMaterialListPanel();
	}

	private void MarkBattleDeckCardConsumed(MaterialModel card)
	{
		if (card != null && playerState.Deck.Contains(card))
		{
			consumedBattleDeckCards.Add(card);
			RefreshMaterialListPanel();
		}
	}

	public List<MagicData> GetRewardMagicChoices()
	{
		if (TutorialManager != null && TutorialManager.MainTutorialRunning)
			return GetTutorialRewardMagicChoices();

		List<MagicData> list = new List<MagicData>();
		RewardPoolData rewardPool = null;
		if (currentLevel != null && currentLevel.rewardPoolId > 0)
			GameDataDatabase.TryGetRewardPoolData(currentLevel.rewardPoolId, out rewardPool);

		if (rewardPool != null && rewardPool.magicIds != null)
		{
			for (int i = 0; i < rewardPool.magicIds.Length; i++)
			{
				if (GameDataDatabase.TryGetMagicData(rewardPool.magicIds[i], out var data))
				{
					list.Add(data);
				}
			}
		}
		if (list.Count == 0)
		{
			list.AddRange(GameDataDatabase.MagicData.Values);
		}
		List<MagicData> list2 = new List<MagicData>();
		int num = 0;
		while (list2.Count < 3 && list.Count > 0 && num < 30)
		{
			num++;
			MagicData item = list[Random.Range(0, list.Count)];
			if (!list2.Contains(item))
			{
				list2.Add(item);
			}
		}
		return list2;
	}

	private List<MagicData> GetTutorialRewardMagicChoices()
	{
		List<MagicData> choices = new List<MagicData>();
		foreach (MagicData data in GameDataDatabase.MagicData.Values)
		{
			if (data != null)
			{
				choices.Add(data);
				if (choices.Count >= 3)
					break;
			}
		}
		return choices;
	}

	public void SelectPendingRewardMagic(MagicData rewardMagic)
	{
		pendingRewardMagic = rewardMagic;
        if (rewardMagic != null)
            ClearPendingShopMagic();
		if (rewardMagic != null)
			TutorialManager?.OnRewardMagicSelected();
	}

    public void SelectPendingShopMagic(MagicData magicData, Action<int> onSlotChosen)
    {
        pendingShopMagic = magicData;
        pendingShopMagicSlotChosen = onSlotChosen;
        if (magicData != null)
            pendingRewardMagic = null;
    }

    public void ClearPendingShopMagic()
    {
        pendingShopMagic = null;
        pendingShopMagicSlotChosen = null;
    }

    public bool TryPlacePendingShopMagic(int slotIndex)
    {
        if (pendingShopMagic == null)
            return false;

        Action<int> slotChosen = pendingShopMagicSlotChosen;
        ClearPendingShopMagic();
        slotChosen?.Invoke(slotIndex);
        return true;
    }

    public void SelectPendingMagicModifier(MagicModifierData modifierData)
    {
        pendingMagicModifier = modifierData;
    }

	public bool HasPendingRewardMagic => pendingRewardMagic != null;

    public bool HasPendingShopMagic => pendingShopMagic != null;

    public bool HasPendingMagicModifier => pendingMagicModifier != null;

	public bool TryPlacePendingRewardMagic(int slotIndex)
	{
		if (pendingRewardMagic == null)
			return false;

        MagicData rewardMagic = pendingRewardMagic;
        RectTransform sourceRect = GetUIManager().RewardPanel != null ? GetUIManager().RewardPanel.SelectedMagicRect : null;
        pendingRewardMagic = null;
        StartCoroutine(SetRewardMagicAtSlotAnimatedRoutine(rewardMagic, slotIndex, sourceRect));
		return true;
	}

    public bool TryApplyPendingMagicModifier(int slotIndex)
    {
        if (pendingMagicModifier == null)
            return false;

        MagicModel magic = playerState.GetMagicAtSlot(slotIndex);
        MagicModifierSelectionPanelUI panel = GetUIManager().MagicModifierSelectionPanel;
        if (magic == null)
        {
            panel?.ShowPopup(LocalizationSystem.GetText("ui.magic_modifier.empty_slot", "这个法术槽是空的！"));
            return false;
        }
        if (magic.HasModifier)
        {
            panel?.ShowPopup(LocalizationSystem.GetText("ui.magic_modifier.already_has_modifier", "这个法术已经有强化了！"));
            return false;
        }

        MagicModifierModel modifier = MagicModifierFactory.Create(pendingMagicModifier);
        if (modifier == null || !magic.AddModifier(modifier))
        {
            panel?.ShowPopup(LocalizationSystem.GetText("ui.magic_modifier.not_applicable", "这个强化不能用于该法术！"));
            return false;
        }

        pendingMagicModifier = null;
        CreateMagicViews();
        panel?.CompleteSelection();
        return true;
    }

	public void ShowSlotSelect(MagicData rewardMagic)
	{
		SelectPendingRewardMagic(rewardMagic);
	}

	public void SetRewardMagicAtSlot(MagicData rewardMagic, int slotIndex)
	{
		if (rewardMagic == null)
			return;

		playerState.SetMagicAtSlot(MagicFactory.Create(rewardMagic, slotIndex), slotIndex);
		CreateMagicViews();
		GetUIManager().RewardPanel?.CompleteMagicRewardSelection();
		TutorialManager?.OnRewardMagicEquipped(playerState, mapNodes, currentMapNodeIndex, activeChapter ?? GetActiveChapter(), currentLevel);
	}

    public IEnumerator GainGoldAnimated(int amount, RectTransform sourceRect)
    {
        if (amount <= 0 || playerState == null)
            yield break;

        GoldDisplayUI goldDisplay = GetUIManager().GoldDisplay;
        if (goldDisplay != null)
            yield return goldDisplay.PlayGain(amount, sourceRect, () => playerState.AddGold(1), () => playerState.Gold);
        else
            playerState.AddGold(amount);

        RefreshStaticUI();
        SaveRunProgress();
    }

    public bool TrySpendShopGold(int amount)
    {
        if (playerState == null || amount < 0 || playerState.Gold < amount)
            return false;

        playerState.AddGold(-amount);
        RefreshStaticUI();
        SaveRunProgress();
        return true;
    }

    public void SetShopMagicAtSlot(MagicData magicData, int slotIndex)
    {
        if (magicData == null)
            return;

        playerState.SetMagicAtSlot(MagicFactory.Create(magicData, slotIndex), slotIndex);
        CreateMagicViews();
        RefreshStaticUI();
        SaveRunProgress();
    }

    public void SetShopMagicAtSlotAnimated(MagicData magicData, int slotIndex, RectTransform sourceRect, Action onComplete)
    {
        StartCoroutine(SetShopMagicAtSlotAnimatedRoutine(magicData, slotIndex, sourceRect, onComplete));
    }

    private IEnumerator SetShopMagicAtSlotAnimatedRoutine(MagicData magicData, int slotIndex, RectTransform sourceRect, Action onComplete)
    {
        yield return PlayMagicAcquireAnimation(magicData, slotIndex, sourceRect);
        SetShopMagicAtSlot(magicData, slotIndex);
        onComplete?.Invoke();
    }

    public void AddShopMaterialAnimated(MaterialEnum material, RectTransform sourceRect, Action onComplete)
    {
        StartCoroutine(AddShopMaterialAnimatedRoutine(material, sourceRect, onComplete));
    }

    private IEnumerator AddShopMaterialAnimatedRoutine(MaterialEnum material, RectTransform sourceRect, Action onComplete)
    {
        yield return PlayMaterialAcquireAnimation(material, sourceRect);
        AddShopMaterial(material);
        onComplete?.Invoke();
    }

    private IEnumerator SetRewardMagicAtSlotAnimatedRoutine(MagicData rewardMagic, int slotIndex, RectTransform sourceRect)
    {
        yield return PlayMagicAcquireAnimation(rewardMagic, slotIndex, sourceRect);
        SetRewardMagicAtSlot(rewardMagic, slotIndex);
    }

    public void AddShopMaterial(MaterialEnum material)
    {
        if (material == MaterialEnum.None)
            return;

        playerState.AddDeckMaterial(material);
        RefreshMaterialListPanel();
        RefreshStaticUI();
        SaveRunProgress();
    }

    public bool RemoveShopMaterial(MaterialModel material)
    {
        if (material == null || playerState == null)
            return false;

        bool removed = playerState.RemoveCardEverywhere(material);
        if (removed)
        {
            RefreshMaterialListPanel();
            RebuildCards(animateFromCurrent: true);
            RefreshStaticUI();
            SaveRunProgress();
        }
        return removed;
    }

	public void FinishReward()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		pendingRewardMagic = null;
        pendingShopMagic = null;
        pendingShopMagicSlotChosen = null;
        pendingMagicModifier = null;
        playerState.ClearCombatState();
		GetUIManager().HideRewardPanel();
        GetUIManager().HideShopPanel();
		GetUIManager().RewardGridPanel?.Hide();
		GetUIManager().HideSlotSelect();
        GetUIManager().MagicModifierSelectionPanel?.Hide();
		enemyModels.Clear();
		battleManager.ClearEnemies();
		enemyViewStates.Clear();
		currentEvent = null;
		currentLevel = null;
		GameLog.Data($"Finish reward node={currentMapNodeIndex + 1}/{mapNodes.Count}");
		if (mapNodes.Count != 0)
		{
			int num = currentMapNodeIndex;
			currentMapNodeIndex++;
			GameLog.Data($"Advance map node index={currentMapNodeIndex + 1}");
			RefreshChapterProgressUI();
			if (currentMapNodeIndex >= mapNodes.Count)
			{
				ShowVictoryPanel();
				return;
			}
            SaveRunProgress();
			ShowLevelSelectAfterMapAdvance(num != currentMapNodeIndex);
		}
	}

	private void ShowLevelSelectAfterMapAdvance(bool animateMarker)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		busy = true;
		SetButtonsInteractable(interactable: false);
		HideLevelSelectPanel();
		ShowMapPanel(focusCurrentNode: true, (TweenCallback)delegate
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Expected O, but got Unknown
			HideMapPanel();
			DOVirtual.DelayedCall(GetUIManager().MapPanel != null ? GetUIManager().MapPanel.HideDuration + levelSelectAfterMapHideExtraDelay : levelSelectAfterMapHideFallbackDelay, (TweenCallback)delegate
			{
				GetUIManager().SyncMapPanelToCurrentNode();
				CreateLevelSelectPanel();
			}, true).SetTarget(this);
		}, animateMarker);
	}

	private IEnumerator ResolveEnemyIntentsRoutine(EnemyModel enemy)
	{
		EnemyViewState state = FindEnemyViewState(enemy);
		suppressEnemyIntentRefresh = true;
		CombatantModel opponent = new CombatantModel(playerState);
        enemy.ClearShield();
		enemy.TriggerOnTurnStart(opponent);
		enemy.TriggerAfterTurnStart(opponent);
		enemy.BeginResolveIntents(playerState);

		for (int i = 0; i < enemy.CurrentIntents.Count; i++)
		{
			EnemyIntentView intentView = state != null && i < state.intentViews.Count ? state.intentViews[i] : null;
			if (intentView != null)
			{
				Tween ripple = intentView.PlayRipple(enemyIntentRippleDuration);
				if (ripple != null)
					yield return ripple.WaitForCompletion();
				else
					yield return new WaitForSeconds(enemyIntentRippleDuration);
			}
			else
			{
				yield return new WaitForSeconds(enemyIntentRippleDuration);
			}

			yield return new WaitForSeconds(enemyIntentPrePerformDelay);
			yield return PlayEnemyIntentPerformance(state, enemy.CurrentIntents[i]);
			int playerHealthBefore = playerState.CurrentHealth;
			int playerShieldBefore = playerState.Shield;
			int enemyShieldBefore = enemy.Shield;
			enemy.ResolveCurrentIntentAt(i, playerState);
			PlayPlayerDamageFeedbackIfNeeded(playerHealthBefore, playerShieldBefore);
			PlayEnemyShieldFeedbackIfNeeded(state, enemy, enemyShieldBefore);
			RefreshStaticUI();
			RefreshEnemyUI(state, false);
			Tween fadeOut = intentView != null ? intentView.PlayFadeOut(enemyIntentFadeOutDuration) : null;
			if (fadeOut != null)
				yield return fadeOut.WaitForCompletion();
			yield return new WaitForSeconds(enemyIntentBetweenDelay);
		}

		enemy.EndResolveIntents(playerState);
        enemy.TriggerOnTurnEnd(opponent);
        enemy.TriggerAfterTurnEnd(opponent);
		RefreshEnemyUI(state, false);
		yield return PlayPendingEnemyDeaths();
	}

	private void PlayPlayerDamageFeedbackIfNeeded(int healthBefore, int shieldBefore)
	{
		int healthDelta = playerState.CurrentHealth - healthBefore;
		int shieldDelta = playerState.Shield - shieldBefore;
		if (healthDelta < 0)
		{
			GetUIManager().PlayerFeedback?.PlayDamageFeedback(new Color(0.95f, 0.05f, 0.02f, 0.72f), playerState);
			ShowPlayerFloatingText(healthDelta.ToString(), FloatingTextType.Damage);
		}
		else if (shieldDelta < 0)
		{
			PlayPlayerCornerFeedback(new Color(0.1f, 0.45f, 1f, 0.58f));
			ShowPlayerFloatingText("BLOCK", FloatingTextType.Damage, true);
		}
		GetUIManager().PlayerFeedback?.UpdateVignetteRange(playerState);
	}

	private void PlayEnemyShieldFeedbackIfNeeded(EnemyViewState state, EnemyModel enemy, int shieldBefore)
	{
		if (state == null || enemy == null)
		{
			return;
		}

		int shieldDelta = enemy.Shield - shieldBefore;
		if (shieldDelta > 0)
		{
			ShowFloatingText(state.viewRect, "+" + shieldDelta, FloatingTextType.Shield);
		}
	}

	private IEnumerator PlayEnemyIntentPerformance(EnemyViewState state, EnemyIntentData intent)
	{
		if (state == null || intent == null)
			yield break;

		if (intent.intentType == EnemyIntentType.Attack || intent.actionType == EnemyActionType.AttackAll)
		{
			yield return PlayEnemyAttackPerformance(state);
		}
		else if (intent.intentType == EnemyIntentType.Defend || intent.actionType == EnemyActionType.GainShield)
		{
			yield return PlayEnemyDefendPerformance(state);
		}
	}

	private IEnumerator PlayEnemyAttackPerformance(EnemyViewState state)
	{
		if (state.bodyImage == null)
			yield break;

		RectTransform body = state.bodyImage.rectTransform;
		body.DOKill(false);
		Vector2 origin = body.anchoredPosition;
		Sequence sequence = DOTween.Sequence().SetTarget(this);
		sequence.Append(body.DOAnchorPos(origin + Vector2.down * enemyAttackJumpDistance, enemyAttackJumpDuration).SetEase(Ease.OutQuad));
		sequence.Append(body.DOAnchorPos(origin, enemyAttackJumpDuration).SetEase(Ease.OutBack));
		yield return sequence.WaitForCompletion();
	}

	private IEnumerator PlayEnemyDefendPerformance(EnemyViewState state)
	{
		if (state.viewRect == null)
			yield break;

		Image icon = new GameObject("DefendBurst", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
		icon.transform.SetParent(state.viewRect, false);
		icon.sprite = Resources.Load<Sprite>("Images/UI/defend");
		icon.color = new Color(0.35f, 0.65f, 1f, 0.9f);
		icon.raycastTarget = false;
		RectTransform rect = icon.rectTransform;
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = Vector2.zero;
		rect.sizeDelta = new Vector2(enemyDefendIconSize, enemyDefendIconSize);
		rect.localScale = Vector3.zero;
		Sequence sequence = DOTween.Sequence().SetTarget(this);
		sequence.Join(rect.DOScale(Vector3.one * 1.35f, enemyDefendIconDuration).SetEase(Ease.OutCubic));
		sequence.Join(icon.DOFade(0f, enemyDefendIconDuration).SetEase(Ease.InQuad));
		sequence.OnComplete(() => Destroy(icon.gameObject));
		yield return sequence.WaitForCompletion();
	}

	private EnemyModel GetFirstAliveEnemy()
	{
        return battleManager != null ? battleManager.GetFirstAliveEnemy() : null;
	}

	private RectTransform GetAliveEnemyTargetRect()
	{
		EnemyModel firstAliveEnemy = GetFirstAliveEnemy();
		return FindEnemyViewState(firstAliveEnemy)?.viewRect;
	}

    private void CollectAliveEnemyTargetRects(List<RectTransform> results)
    {
        if (results == null)
            return;

        results.Clear();
        for (int i = 0; i < enemyViewStates.Count; i++)
        {
            EnemyViewState state = enemyViewStates[i];
            if (state != null && state.model != null && !state.model.IsDead && (Object)state.viewRect != (Object)null)
                results.Add(state.viewRect);
        }
    }

	private EnemyViewState FindEnemyViewState(EnemyModel model)
	{
		if (model == null)
		{
			return null;
		}
		for (int i = 0; i < enemyViewStates.Count; i++)
		{
			if (enemyViewStates[i].model == model)
			{
				return enemyViewStates[i];
			}
		}
		return null;
	}

	private bool CheckPlayerDefeated()
	{
		if (playerState == null || playerState.CurrentHealth > 0)
			return false;

		ShowDefeatPanel();
		return true;
	}

	private void ShowVictoryPanel()
	{
		ShowRunResultPanel(true);
	}

	private void ShowDefeatPanel()
	{
		ShowRunResultPanel(false);
	}

	private void ShowRunResultPanel(bool victory)
	{
		if (runEnded)
			return;

        float playSeconds = victory ? GetCurrentRunPlaySeconds() : 0f;
        List<string> magicNames = victory ? GetVictoryMagicNames() : null;
        if (victory)
            RunSaveSystem.RecordVictoryAndClearCurrentRun(playSeconds);
        else
            RunSaveSystem.ClearCurrentRun();
		runEnded = true;
		busy = true;
		SetButtonsInteractable(interactable: false);
		ResetMagicHighlights();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameplayMusic();
		GetUIManager().HideLevelSelect();
		GetUIManager().HideMapPanel();
		GetUIManager().HideRewardPanel();
		GetUIManager().RewardGridPanel?.Hide();
		GetUIManager().HideSlotSelect();
		ResetContinuousCastCounterUI();
		if (victory)
			GetUIManager().ShowVictoryPanel(playSeconds, magicNames);
		else
			GetUIManager().ShowDefeatPanel(playerState?.LastDamageSourceEnemy?.Name);
	}

    private float GetCurrentRunPlaySeconds()
    {
        return loadedRunPlaySeconds + Mathf.Max(0f, Time.realtimeSinceStartup - runStartRealtime);
    }

    private List<string> GetVictoryMagicNames()
    {
        List<string> names = new List<string>();
        if (playerState == null)
            return names;

        int slotCount = Mathf.Min(6, playerState.MagicBookSlotCount);
        for (int i = 0; i < slotCount; i++)
        {
            MagicModel magic = playerState.GetMagicAtSlot(i);
            if (magic != null && !string.IsNullOrEmpty(magic.Name))
                names.Add(magic.Name);
        }
        return names;
    }

	private bool AllEnemiesDead()
	{
        return battleManager != null && battleManager.AllEnemiesDead();
	}

	private bool HasAliveEnemyAfter(int index)
	{
        return battleManager != null && battleManager.HasAliveEnemyAfter(index);
	}

	private IEnumerator PlayPendingEnemyDeaths()
	{
		for (int i = 0; i < enemyModels.Count; i++)
		{
			EnemyModel enemyModel = enemyModels[i];
			if (enemyModel.IsDead && !enemyModel.DeathHandled)
			{
				enemyModel.Die();
				EnemyViewState enemyViewState = FindEnemyViewState(enemyModel);
				if (enemyViewState != null)
				{
					yield return PlayEnemyExplosion(enemyViewState);
				}
			}
		}
	}

	private IEnumerator PlayEnemyExplosion(EnemyViewState state)
	{
		if ((Object)state.viewRect == (Object)null || (Object)state.bodyImage == (Object)null)
		{
			yield break;
		}

		EnsureEnemyDeathExplosionMaterial();
		if ((Object)enemyDeathExplosionMaterialTemplate == (Object)null)
		{
			TweenSettingsExtensions.SetTarget<TweenerCore<Vector3, Vector3, VectorOptions>>(TweenSettingsExtensions.SetEase<TweenerCore<Vector3, Vector3, VectorOptions>>(ShortcutExtensions.DOScale((Transform)state.viewRect, 0f, enemyDeathScaleDuration), enemyDeathScaleEase), (object)this);
			yield return (object)new WaitForSeconds(enemyDeathScaleDuration);
			((Component)state.viewRect).gameObject.SetActive(false);
            LayoutEnemyViews();
			yield break;
		}

		Image sourceImage = state.bodyImage;
		RectTransform sourceRect = sourceImage.rectTransform;
		Transform shardParent = ((Transform)sourceRect).parent;
		Sprite sourceSprite = sourceImage.sprite;
		Vector4 spriteUv = new Vector4(0f, 0f, 1f, 1f);
		if ((Object)sourceSprite != (Object)null)
		{
			Vector4 outerUv = UnityEngine.Sprites.DataUtility.GetOuterUV(sourceSprite);
			spriteUv = new Vector4(outerUv.x, outerUv.y, outerUv.z - outerUv.x, outerUv.w - outerUv.y);
		}

		Vector2 bodySize = sourceRect.rect.size;
		if (bodySize.x <= 0f || bodySize.y <= 0f)
		{
			bodySize = sourceRect.sizeDelta;
		}
		bodySize.x = Mathf.Max(1f, Mathf.Abs(bodySize.x));
		bodySize.y = Mathf.Max(1f, Mathf.Abs(bodySize.y));

		int shardCount = EnemyDeathShardColumns * EnemyDeathShardRows;
		RectTransform[] shardRects = new RectTransform[shardCount];
		Material[] shardMaterials = new Material[shardCount];
		Vector2[] startPositions = new Vector2[shardCount];
		Vector2[] targetPositions = new Vector2[shardCount];
		float[] startRotations = new float[shardCount];
		float[] targetRotations = new float[shardCount];
		Vector3[] startScales = new Vector3[shardCount];
		Vector3[] targetScales = new Vector3[shardCount];
		Vector2 shardSize = new Vector2(bodySize.x / EnemyDeathShardColumns, bodySize.y / EnemyDeathShardRows);
		Vector2 sourceAnchoredPosition = sourceRect.anchoredPosition;
		Vector3 sourceScale = ((Transform)sourceRect).localScale;
		float sourceRotation = ((Transform)sourceRect).localEulerAngles.z;
		int sourceSiblingIndex = ((Transform)sourceRect).GetSiblingIndex();
		int index = 0;

		for (int row = 0; row < EnemyDeathShardRows; row++)
		{
			for (int column = 0; column < EnemyDeathShardColumns; column++)
			{
				GameObject shardObject = new GameObject("BodyShard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
				RectTransform shardRect = (RectTransform)shardObject.transform;
				shardRect.SetParent(shardParent, false);
				shardRect.SetSiblingIndex(sourceSiblingIndex + 1 + index);
				shardRect.anchorMin = sourceRect.anchorMin;
				shardRect.anchorMax = sourceRect.anchorMax;
				shardRect.pivot = new Vector2(0.5f, 0.5f);
				shardRect.sizeDelta = shardSize;
				((Transform)shardRect).localScale = sourceScale;
				((Transform)shardRect).localRotation = ((Transform)sourceRect).localRotation;

				float normalizedX = (column + 0.5f) / EnemyDeathShardColumns;
				float normalizedY = (row + 0.5f) / EnemyDeathShardRows;
				Vector2 offset = new Vector2(normalizedX * bodySize.x - sourceRect.pivot.x * bodySize.x, normalizedY * bodySize.y - sourceRect.pivot.y * bodySize.y);
				Vector2 startPosition = sourceAnchoredPosition + offset;
				shardRect.anchoredPosition = startPosition;

				Material shardMaterial = new Material(enemyDeathExplosionMaterialTemplate);
				shardMaterial.SetFloat("_Explosion", 0f);
				shardMaterial.SetFloat("_ShardIndex", index);
				shardMaterial.SetVector("_ShardRect", new Vector4((float)column / EnemyDeathShardColumns, (float)row / EnemyDeathShardRows, 1f / EnemyDeathShardColumns, 1f / EnemyDeathShardRows));
				shardMaterial.SetVector("_SpriteUV", spriteUv);

				Image shardImage = shardObject.GetComponent<Image>();
				shardImage.sprite = sourceSprite;
				shardImage.color = sourceImage.color;
				shardImage.type = Image.Type.Simple;
				shardImage.preserveAspect = false;
				shardImage.raycastTarget = false;
				shardImage.maskable = sourceImage.maskable;
				shardImage.material = shardMaterial;

				Vector2 direction = offset;
				if (direction.sqrMagnitude < 0.01f)
				{
					direction = Random.insideUnitCircle;
				}
				if (direction.sqrMagnitude < 0.01f)
				{
					direction = Vector2.up;
				}
				direction.Normalize();
				Vector2 scatter = direction * Random.Range(EnemyDeathExplosionDistance * 0.62f, EnemyDeathExplosionDistance);
				scatter += Random.insideUnitCircle * 26f;

				shardRects[index] = shardRect;
				shardMaterials[index] = shardMaterial;
				startPositions[index] = startPosition;
				targetPositions[index] = startPosition + scatter;
				startRotations[index] = sourceRotation;
				targetRotations[index] = sourceRotation + Random.Range(-170f, 170f);
				startScales[index] = sourceScale;
				targetScales[index] = sourceScale * Random.Range(0.82f, 1.12f);
				index++;
			}
		}

		sourceImage.enabled = false;
		float duration = Mathf.Max(0.01f, enemyDeathExplosionDuration);
		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			float progress = Mathf.Clamp01(t / duration);
			float scatterProgress = 1f - Mathf.Pow(1f - progress, 3f);
			for (int i = 0; i < shardCount; i++)
			{
				RectTransform shardRect = shardRects[i];
				if ((Object)shardRect == (Object)null)
				{
					continue;
				}
				shardRect.anchoredPosition = Vector2.LerpUnclamped(startPositions[i], targetPositions[i], scatterProgress);
				((Transform)shardRect).localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(startRotations[i], targetRotations[i], scatterProgress));
				((Transform)shardRect).localScale = Vector3.LerpUnclamped(startScales[i], targetScales[i], scatterProgress);
				shardMaterials[i].SetFloat("_Explosion", progress);
			}
			yield return null;
		}

		for (int i = 0; i < shardCount; i++)
		{
			if ((Object)shardRects[i] != (Object)null)
			{
				Object.Destroy((Object)((Component)shardRects[i]).gameObject);
			}
			if ((Object)shardMaterials[i] != (Object)null)
			{
				Object.Destroy((Object)shardMaterials[i]);
			}
		}

		((Component)state.viewRect).gameObject.SetActive(false);
        LayoutEnemyViews();
	}

	private void EnsureEnemyDeathExplosionMaterial()
	{
		if (!((Object)enemyDeathExplosionMaterialTemplate != (Object)null))
		{
			enemyDeathExplosionMaterialTemplate = Resources.Load<Material>(EnemyDeathExplosionMaterialPath);
			if (!((Object)enemyDeathExplosionMaterialTemplate != (Object)null))
			{
				Shader shader = Shader.Find("Style/Sprite/FragmentExplosion");
				if ((Object)shader != (Object)null)
				{
					enemyDeathExplosionMaterialTemplate = new Material(shader);
				}
			}
		}
	}

	private RectTransform GetMagicEffectTarget(MagicModel magic, EnemyModel targetEnemy)
	{
		if (GetMagicEffectTargetType(magic) != SpellEffectTarget.Player)
		{
			return FindEnemyViewState(targetEnemy)?.viewRect ?? GetAliveEnemyTargetRect();
		}
		return GetUIManager().PlayerFeedback?.PlayerVirtualTarget;
	}

	private SpellEffectTarget GetMagicEffectTargetType(MagicModel magic)
	{
		MagicEffectType effectType = magic.Data.effectType;
		if ((uint)(effectType - 2) <= 1u || effectType == MagicEffectType.DrawNextTurn)
		{
			return SpellEffectTarget.Player;
		}
		return SpellEffectTarget.Enemy;
	}

	private void CastMagic(MagicModel magic)
	{
		if (magic != null)
		{
			GameLog.Data("Request cast magic=" + magic.Id + " name=" + magic.Name);
			EnemyModel firstAliveEnemy = GetFirstAliveEnemy();
			if (firstAliveEnemy != null || GetMagicEffectTargetType(magic) != SpellEffectTarget.Enemy)
			{
				enemyModel = firstAliveEnemy;
				magicEnemyTargets.Clear();
				EnemyModel focusTarget = battleManager.FocusTarget;
				battleManager.SetFocusTarget(focusTarget);
				battleManager.BeginCastTarget();
				int count = battleManager.RegisterMagicCast();
				UpdateContinuousCastCounter(count, instant: false);
				MagicCastResult result = magic.Cast(playerState, battleManager);
				PlayMagicCastFeedback(result);
			}
		}
	}

	private void PlayMagicCastFeedback(MagicCastResult result)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		if (result != null)
		{
			for (int i = 0; i < result.enemyDamageHits.Count; i++)
			{
				PlayEnemyDamageFeedback(result.enemyDamageHits[i]);
			}
			if (result.playerHeal > 0)
			{
				PlayPlayerCornerFeedback(new Color(0.1f, 0.95f, 0.25f, 0.48f));
				ShowPlayerFloatingText("+" + result.playerHeal, FloatingTextType.Heal);
				RefreshStaticUI();
			}
			if (result.playerShield > 0)
			{
				PlayPlayerCornerFeedback(new Color(0.1f, 0.45f, 1f, 0.5f));
				ShowPlayerFloatingText("+" + result.playerShield, FloatingTextType.Shield);
				RefreshStaticUI();
			}
			if (result.enemyBuffApplied)
			{
				RefreshEnemyUI();
			}
		}
	}

	private void PlayEnemyDamageFeedback(MagicDamageHitResult hit)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		if (hit == null || hit.target == null)
		{
			return;
		}

		EnemyViewState enemyViewState = FindEnemyViewState(hit.target);
		if (enemyViewState == null)
		{
			return;
		}

		if (hit.healthDamage > 0)
		{
			PlayEnemyHitFeedback(enemyViewState);
			ShowFloatingText(enemyViewState.viewRect, "-" + hit.healthDamage, FloatingTextType.Damage);
			RefreshEnemyUI();
		}
		else if (hit.FullyBlocked)
		{
			ShowFloatingText(enemyViewState.viewRect, "BLOCK", FloatingTextType.Damage, true);
			RefreshEnemyUI();
		}
	}

	private void PlayEnemyHitFeedback(EnemyViewState state)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Expected O, but got Unknown
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Expected O, but got Unknown
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)state.viewRect != (Object)null)
		{
			ShortcutExtensions.DOKill((Component)state.viewRect, false);
			TweenSettingsExtensions.SetTarget<Tweener>(state.viewRect.DOShakeAnchorPos(enemyHitShakeDuration, enemyHitShakeStrength, enemyHitShakeVibrato, 90f, snapping: false, fadeOut: true, (ShakeRandomnessMode)0), (object)this);
		}
		if (!((Object)state.bodyImage == (Object)null))
		{
			ShortcutExtensions.DOKill((Component)state.bodyImage, false);
			state.bodyImage.color = new Color(1f, 0.08f, 0.04f, state.bodyBaseColor.a);
			TweenSettingsExtensions.SetTarget<TweenerCore<Color, Color, ColorOptions>>(TweenSettingsExtensions.SetEase<TweenerCore<Color, Color, ColorOptions>>(state.bodyImage.DOColor(state.bodyBaseColor, enemyHitColorRecoverDuration), enemyHitColorRecoverEase), (object)this);
		}
	}

	private void ShowFloatingText(RectTransform anchor, string text, FloatingTextType type, bool blocked = false)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		if ((Object)anchor == (Object)null || (Object)floatingTextPrefab == (Object)null)
		{
			return;
		}
		Transform parent = ((Transform)anchor).parent;
		RectTransform val = (RectTransform)((parent is RectTransform) ? parent : null);
		if ((Object)val == (Object)null)
		{
			return;
		}

		RectTransform component = Object.Instantiate(floatingTextPrefab, (Transform)val, false);
		component.anchorMin = anchor.anchorMin;
		component.anchorMax = anchor.anchorMax;
		component.pivot = new Vector2(0.5f, 0.5f);
		component.sizeDelta = floatingTextSize * 3f;
		component.anchoredPosition = anchor.anchoredPosition + floatingTextStartOffset;
		((Transform)component).localScale = Vector3.one;

		FloatingTextUI floatingText = ((Component)component).GetComponent<FloatingTextUI>();
		if (floatingText == null)
		{
			Object.Destroy((Object)((Component)component).gameObject);
			return;
		}

		TMP_Text targetText = floatingText.Text;
		if (targetText != null)
		{
			if (targetText.font == null)
				targetText.font = GetDefaultFont();
			targetText.fontSize = floatingTextFontSize * 3;
			targetText.fontStyle = FontStyles.Bold;
			targetText.alignment = TextAlignmentOptions.Center;
			targetText.raycastTarget = false;
		}
		floatingText.Play(text, type, blocked, floatingTextYOffset, floatingTextDuration, floatingTextMoveEase, floatingTextFadeEase);
	}

	private void ShowPlayerFloatingText(string text, FloatingTextType type, bool blocked = false)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		ShowFloatingText(GetUIManager().PlayerFeedback?.PlayerFloatingTextTarget, text, type, blocked);
	}

	private void PlayPlayerCornerFeedback(Color color)
	{
		GetUIManager().PlayerFeedback?.PlayCornerFeedback(color);
	}

	private void RefreshEnemyUI(RectTransform enemyView = null, bool instant = false)
	{
		for (int i = 0; i < enemyViewStates.Count; i++)
		{
			RefreshEnemyUI(enemyViewStates[i], instant);
		}
	}

	private void RefreshEnemyUI(EnemyViewState state, bool instant = false)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Expected O, but got Unknown
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Expected O, but got Unknown
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		if (state == null || (Object)state.viewRect == (Object)null || state.model == null)
		{
			return;
		}
		TMP_Text[] componentsInChildren = ((Component)state.viewRect).GetComponentsInChildren<TMP_Text>(true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (((Object)componentsInChildren[i]).name == "NameText")
			{
				componentsInChildren[i].text = state.model.Name;
			}
		}
		if ((Object)state.intentIcon != (Object)null)
		{
			state.intentIcon.color = (state.model.IsDead ? Color.gray : GetIntentColor(state.model));
		}
		UpdateHealthBar(state.healthFill, state.healthBufferFill, state.shieldFill, state.model.CurrentHealth, state.model.Data.maxHealth, state.model.Shield, instant);
		RefreshBuffRoot(state.buffRoot, state.model.Buffs, null);
		if ((Object)state.focusMarker != (Object)null)
		{
			((Component)state.focusMarker).gameObject.SetActive(battleManager != null && battleManager.FocusTarget == state.model);
		}
		if (!suppressEnemyIntentRefresh)
			RebuildEnemyIntentViews(state);
		Tween healthNumberTween = state.healthNumberTween;
		if (healthNumberTween != null)
		{
			TweenExtensions.Kill(healthNumberTween, false);
		}
		HealthBarUI.SetHealthTextColor(state.healthText, state.model.Shield > 0);
		state.healthNumberTween = UpdateHealthText(state.healthText, state.displayedHealth, HealthBarUI.GetHealthTextValue(state.model.CurrentHealth, state.model.Shield), instant, delegate(int value)
		{
			state.displayedHealth = value;
		});
	}

	private void RebuildEnemyIntentViews(EnemyViewState state)
	{
		if (state == null || state.viewRect == null || state.model == null)
			return;

		RectTransform root = EnsureIntentRoot(state.viewRect);
		IReadOnlyList<EnemyIntentData> intents = state.model.CurrentIntents;
		while (state.intentViews.Count < intents.Count)
			state.intentViews.Add(CreateEnemyIntentView(root));

		float intentSpacing = 64f;
		int totalIntentCount = GetTotalVisibleIntentCount();
		int phaseStart = GetIntentPhaseStartIndex(state.model);
		for (int i = 0; i < state.intentViews.Count; i++)
		{
			EnemyIntentView view = state.intentViews[i];
			bool visible = i < intents.Count;
			view.gameObject.SetActive(visible);
			if (!visible)
				continue;

			RectTransform rect = view.RectTransform;
			Vector2 position = new Vector2((i - (intents.Count - 1) * 0.5f) * intentSpacing, 38f);
			view.SetBaseAnchoredPosition(position);
			rect.localScale = Vector3.one * 1.5f;
			view.Bind(state.model, intents[i], playerState, phaseStart + i, totalIntentCount);
		}
	}

	private RectTransform EnsureIntentRoot(RectTransform enemyView)
	{
		Transform rootTransform = enemyView.Find("IntentRoot");
		RectTransform root = rootTransform as RectTransform;
		if (root == null)
		{
			root = new GameObject("IntentRoot", typeof(RectTransform)).GetComponent<RectTransform>();
			root.SetParent(enemyView, false);
		}
		root.anchorMin = new Vector2(0.5f, 1f);
		root.anchorMax = new Vector2(0.5f, 1f);
		root.pivot = new Vector2(0.5f, 0.5f);
		root.anchoredPosition = Vector2.zero;
		root.sizeDelta = new Vector2(120f, 60f);
		return root;
	}

	private EnemyIntentView CreateEnemyIntentView(RectTransform parent)
	{
		RectTransform rect = new GameObject("IntentView", typeof(RectTransform), typeof(EnemyIntentView)).GetComponent<RectTransform>();
		rect.SetParent(parent, false);
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(42f, 42f);

		Image ripple = new GameObject("Ripple", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
		ripple.transform.SetParent(rect, false);
		ripple.raycastTarget = false;
		ripple.color = Color.white;
		ripple.gameObject.SetActive(false);
		RectTransform rippleRect = ripple.rectTransform;
		rippleRect.anchorMin = new Vector2(0.5f, 0.5f);
		rippleRect.anchorMax = new Vector2(0.5f, 0.5f);
		rippleRect.pivot = new Vector2(0.5f, 0.5f);
		rippleRect.anchoredPosition = Vector2.zero;
		rippleRect.sizeDelta = new Vector2(74f, 74f);

		Image icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
		icon.transform.SetParent(rect, false);
		icon.raycastTarget = false;
		RectTransform iconRect = icon.rectTransform;
		iconRect.anchorMin = new Vector2(0.5f, 0.5f);
		iconRect.anchorMax = new Vector2(0.5f, 0.5f);
		iconRect.pivot = new Vector2(0.5f, 0.5f);
		iconRect.anchoredPosition = Vector2.zero;
		iconRect.sizeDelta = new Vector2(28f, 28f);

		TMP_Text valueText = new GameObject("ValueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
		valueText.transform.SetParent(rect, false);
		valueText.font = GetDefaultFont();
		valueText.fontSize = 16;
		valueText.fontStyle = FontStyles.Bold;
		valueText.color = Color.white;
		valueText.alignment = TextAlignmentOptions.Center;
		valueText.raycastTarget = false;
		RectTransform textRect = valueText.rectTransform;
		textRect.anchorMin = new Vector2(0.5f, 0.5f);
		textRect.anchorMax = new Vector2(0.5f, 0.5f);
		textRect.pivot = new Vector2(0.5f, 0.5f);
		textRect.anchoredPosition = new Vector2(22f, 0f);
		textRect.sizeDelta = new Vector2(36f, 24f);

		return rect.GetComponent<EnemyIntentView>();
	}

	private int GetTotalVisibleIntentCount()
	{
		int count = 0;
		for (int i = 0; i < enemyModels.Count; i++)
		{
			EnemyModel enemy = enemyModels[i];
			if (enemy != null && !enemy.IsDead)
				count += enemy.CurrentIntents.Count;
		}
		return Mathf.Max(1, count);
	}

	private int GetIntentPhaseStartIndex(EnemyModel target)
	{
		int index = 0;
		for (int i = 0; i < enemyModels.Count; i++)
		{
			EnemyModel enemy = enemyModels[i];
			if (enemy == target)
				return index;
			if (enemy != null && !enemy.IsDead)
				index += enemy.CurrentIntents.Count;
		}
		return index;
	}

	private void EnsureEnemyIntentView(RectTransform enemyView)
	{
		Transform valueTransform = enemyView.Find("IntentValueText");
		if (valueTransform == null)
			valueTransform = enemyView.Find("ActionText");
		if (valueTransform != null)
			valueTransform.gameObject.SetActive(false);

		Transform iconTransform = enemyView.Find("IntentIcon");
		if (iconTransform != null)
			iconTransform.gameObject.SetActive(false);
	}

	private string GetIntentText(EnemyModel model)
	{
		IReadOnlyList<EnemyIntentData> currentIntents = model.CurrentIntents;
		if (currentIntents.Count == 0)
		{
			return string.Empty;
		}
		string text = string.Empty;
		for (int i = 0; i < currentIntents.Count; i++)
		{
			if (i > 0)
			{
				text += " + ";
			}
            text += GetIntentDisplayValue(model, currentIntents[i]);

		}
		return text;
	}

	private string GetIntentDisplayValue(EnemyModel model, EnemyIntentData intent)
	{
		if (intent == null)
		{
			return string.Empty;
		}
		if (intent.actionType == EnemyActionType.Attack || intent.actionType == EnemyActionType.AttackAll)
		{
			return (model != null ? model.GetIntentAttackValue(intent, playerState) : intent.value).ToString();
		}
		if (intent.actionType == EnemyActionType.GainShield)
		{
			return (model != null ? model.GetIntentShieldValue(intent) : intent.value).ToString();
		}
		if (intent.actionType == EnemyActionType.Summon)
		{
			return intent.summonCount > 1 ? "×" + intent.summonCount : string.Empty;
		}
		if (intent.actionType == EnemyActionType.ApplyBuff || intent.actionType == EnemyActionType.ApplyDebuff)
		{
			return string.Empty;
		}
		if (intent.value > 0)
		{
			return intent.value.ToString();
		}
		return LocalizationSystem.GetText(intent.descriptionKey, string.Empty);
	}

	private Color GetIntentColor(EnemyModel model)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		IReadOnlyList<EnemyIntentData> currentIntents = model.CurrentIntents;
		if (currentIntents.Count == 0)
		{
			return Color.gray;
		}
		return (Color)(currentIntents[0].intentType switch
		{
			EnemyIntentType.Attack => new Color(0.95f, 0.18f, 0.14f, 1f), 
			EnemyIntentType.Defend => new Color(0.25f, 0.55f, 1f, 1f), 
			EnemyIntentType.ApplyBuff => new Color(0.75f, 0.35f, 1f, 1f),
			EnemyIntentType.ApplyDebuff => new Color(0.25f, 0.8f, 0.35f, 1f),
			EnemyIntentType.Summon => new Color(1f, 0.62f, 0.16f, 1f),
			_ => Color.gray, 
		});
	}

	private void UpdateContinuousCastCounter(int count, bool instant)
	{
		GetUIManager().PlayArea?.UpdateContinuousCastCounter(count, instant);
	}

	private void HideContinuousCastCounterUI()
	{
		UpdateContinuousCastCounter(0, true);
	}

	private void ResetContinuousCastCounterUI()
	{
		if (battleManager != null)
			battleManager.ResetContinuousCastCount();
		HideContinuousCastCounterUI();
	}

	private void MoveIndicatorToCardRange(List<MaterialModel> cards, int startIndex, int count, bool instant)
	{
		if (count <= 0)
			return;

		HandCardView first = FindView(cards[startIndex]);
		HandCardView last = FindView(cards[startIndex + count - 1]);
		GetUIManager().PlayArea?.MoveIndicatorToCardRange(first?.RectTransform, last?.RectTransform, playArea, layoutDuration, layoutEase, instant);
	}

	private void ResetMagicHighlights()
	{
		for (int i = 0; i < magicViews.Count; i++)
		{
			magicViews[i].ResetRecipeHighlights();
		}
	}

	private IEnumerator PlayMagicAcquireAnimation(MagicData magicData, int slotIndex, RectTransform sourceRect)
    {
        RectTransform targetRect = GetMagicSlotRect(slotIndex);
        RectTransform animationRoot = transform as RectTransform;
        if (magicData == null || sourceRect == null || targetRect == null || magicViewPrefab == null || animationRoot == null)
            yield break;

        RectTransform clone = Object.Instantiate(magicViewPrefab, animationRoot);
        clone.gameObject.SetActive(true);
        clone.SetAsLastSibling();
        MagicItemView view = clone.GetComponent<MagicItemView>();
        if (view != null)
            view.Bind(MagicFactory.Create(magicData, slotIndex));
        UIManager.RemoveJuicyMotion(clone.transform);
        DisableGraphicRaycasts(clone);

        yield return PlayAcquireRectAnimation(clone, sourceRect, GetAreaCenterWorldPosition(targetRect), targetRect.rotation, GetScaleRelativeToParent(targetRect, animationRoot), targetRect.rect.size, AcquireMagicAnimationDuration);
        if (clone != null)
            Object.Destroy(clone.gameObject);
    }

    private IEnumerator PlayMaterialAcquireAnimation(MaterialEnum material, RectTransform sourceRect)
    {
        RectTransform animationRoot = transform as RectTransform;
        RectTransform materialPrefab = GetShopMaterialCardPrefab();
        if (material == MaterialEnum.None || sourceRect == null || materialPrefab == null || deckPileArea == null || animationRoot == null)
            yield break;

        RectTransform clone = Object.Instantiate(materialPrefab, animationRoot);
        clone.gameObject.SetActive(true);
        clone.SetAsLastSibling();
        MaterialCardView view = clone.GetComponent<MaterialCardView>();
        if (view != null)
            view.Bind(new MaterialModel("shop_fly_" + material, material));
        UIManager.RemoveJuicyMotion(clone.transform);
        DisableGraphicRaycasts(clone);

        yield return PlayAcquireRectAnimation(clone, sourceRect, GetAreaCenterWorldPosition(deckPileArea), deckPileArea.rotation, GetScaleRelativeToParent(deckPileArea, animationRoot), deckPileArea.rect.size, AcquireMaterialAnimationDuration);
        if (clone != null)
            Object.Destroy(clone.gameObject);
    }

    private RectTransform GetShopMaterialCardPrefab()
    {
        RectTransform prefab = GetUIManager().ShopPanel != null ? GetUIManager().ShopPanel.MaterialCardPrefab : null;
        if (prefab != null)
            return prefab;

        PrefabReferenceLibrary library = GetComponentInParent<PrefabReferenceLibrary>();
        return library != null ? library.MaterialCardPrefab : null;
    }

    private IEnumerator PlayAcquireRectAnimation(RectTransform clone, RectTransform sourceRect, Vector3 targetWorldPosition, Quaternion targetWorldRotation, Vector3 targetLocalScale, Vector2 targetSize, float duration)
    {
        if (clone == null || sourceRect == null)
            yield break;

        RectTransform animationRoot = clone.parent as RectTransform;
        clone.position = sourceRect.position;
        clone.rotation = sourceRect.rotation;
        clone.localScale = GetScaleRelativeToParent(sourceRect, animationRoot);
        clone.sizeDelta = sourceRect.rect.size;
        sourceRect.gameObject.SetActive(false);

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(clone.DOMove(targetWorldPosition, duration).SetEase(Ease.OutCubic));
        sequence.Join(clone.DORotateQuaternion(targetWorldRotation, duration).SetEase(Ease.OutCubic));
        sequence.Join(clone.DOScale(targetLocalScale, duration).SetEase(Ease.OutCubic));
        sequence.Join(clone.DOSizeDelta(targetSize, duration).SetEase(Ease.OutCubic));
        yield return sequence.WaitForCompletion();
    }

    private RectTransform GetMagicSlotRect(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= magicViews.Count || magicViews[slotIndex] == null)
            return null;

        return magicViews[slotIndex].transform as RectTransform;
    }

    private static Vector3 GetScaleRelativeToParent(RectTransform rect, RectTransform parent)
    {
        if (rect == null)
            return Vector3.one;

        Vector3 scale = rect.lossyScale;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        return new Vector3(
            parentScale.x != 0f ? scale.x / parentScale.x : rect.localScale.x,
            parentScale.y != 0f ? scale.y / parentScale.y : rect.localScale.y,
            parentScale.z != 0f ? scale.z / parentScale.z : rect.localScale.z);
    }

    private static void DisableGraphicRaycasts(RectTransform root)
    {
        if (root == null)
            return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

	private void AnimateViewsToDeck(List<HandCardView> views, TweenCallback onComplete)
	{
		AnimateViewsToArea(views, deckPileArea, onComplete);
	}

	private void AnimateReturningViews(List<HandCardView> views, List<MaterialModel> removedTemporaryCards, RectTransform returnArea, TweenCallback onComplete)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Expected O, but got Unknown
		if (removedTemporaryCards == null || removedTemporaryCards.Count == 0)
		{
			AnimateViewsToArea(views, returnArea, onComplete);
			return;
		}
		List<HandCardView> list = new List<HandCardView>();
		List<HandCardView> temporaryViews = new List<HandCardView>();
		for (int i = 0; i < views.Count; i++)
		{
			HandCardView handCardView = views[i];
			if (!((Object)handCardView == (Object)null))
			{
				if (removedTemporaryCards.Contains(handCardView.Card))
				{
					if (playerState.Deck.Contains(handCardView.Card))
					{
						MarkBattleDeckCardConsumed(handCardView.Card);
						list.Add(handCardView);
					}
					else
					{
						temporaryViews.Add(handCardView);
					}
				}
				else
				{
					list.Add(handCardView);
				}
			}
		}
		AnimateViewsToArea(list, returnArea, (TweenCallback)delegate
		{
			AnimateTemporaryViewsDissolve(temporaryViews, onComplete);
		});
	}

	private void AnimateTemporaryViewsDissolve(List<HandCardView> views, TweenCallback onComplete)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Expected O, but got Unknown
		//IL_0086: Expected O, but got Unknown
		if (views.Count == 0)
		{
			TweenCallback obj = onComplete;
			if (obj != null)
			{
				obj.Invoke();
			}
			return;
		}
		int remaining = 0;
		TweenCallback val = default(TweenCallback);
		for (int i = 0; i < views.Count; i++)
		{
			HandCardView handCardView = views[i];
			if ((Object)handCardView == (Object)null)
			{
				continue;
			}
			cardViews.Remove(handCardView);
			remaining++;
			TweenCallback obj2 = val;
			if (obj2 == null)
			{
				TweenCallback val2 = delegate
				{
					remaining--;
					if (remaining == 0)
					{
						TweenCallback obj4 = onComplete;
						if (obj4 != null)
						{
							obj4.Invoke();
						}
					}
				};
				TweenCallback val3 = val2;
				val = val2;
				obj2 = val3;
			}
			((MonoBehaviour)this).StartCoroutine(PlayTemporaryCardDissolve(handCardView, obj2));
		}
		if (remaining == 0)
		{
			TweenCallback obj3 = onComplete;
			if (obj3 != null)
			{
				obj3.Invoke();
			}
		}
	}

	private IEnumerator PlayTemporaryCardDissolve(HandCardView view, TweenCallback onComplete)
	{
		EnsureTemporaryCardDissolveMaterial();
		Image[] componentsInChildren = ((Component)view).GetComponentsInChildren<Image>(true);
		Material material = (((Object)(object)temporaryCardDissolveMaterialTemplate == (Object)null) ? ((Material)null) : new Material(temporaryCardDissolveMaterialTemplate));
		if ((Object)(object)material == (Object)null)
		{
			Object.Destroy((Object)((Component)view).gameObject);
			if (onComplete != null)
			{
				onComplete.Invoke();
			}
			yield break;
		}
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].material = material;
		}
		float t = 0f;
		while (t < 0.45f)
		{
			t += Time.deltaTime;
			material.SetFloat("_Dissolve", Mathf.Clamp01(t / 0.45f));
			yield return null;
		}
		Object.Destroy((Object)((Component)view).gameObject);
		Object.Destroy((Object)material);
		if (onComplete != null)
		{
			onComplete.Invoke();
		}
	}

	private void EnsureTemporaryCardDissolveMaterial()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		if (!((Object)(object)temporaryCardDissolveMaterialTemplate != (Object)null))
		{
			Shader val = Shader.Find("UI/TemporaryCardDissolve");
			if ((Object)(object)val != (Object)null)
			{
				temporaryCardDissolveMaterialTemplate = new Material(val);
			}
		}
	}

	private void AnimateViewsToDiscard(List<HandCardView> views, TweenCallback onComplete)
	{
		AnimateViewsToArea(views, GetDiscardPileArea(), onComplete);
	}

	private void AnimateViewsToArea(List<HandCardView> views, RectTransform targetArea, TweenCallback onComplete)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected O, but got Unknown
		if (views.Count == 0)
		{
			TweenCallback val = onComplete;
			if (val != null)
			{
				val.Invoke();
			}
			return;
		}
		int remaining = 0;
		for (int i = 0; i < views.Count; i++)
		{
			HandCardView view = views[i];
			if ((Object)view == (Object)null)
			{
				continue;
			}
			cardViews.Remove(view);
			int num = remaining;
			remaining = num + 1;
			AnimateCardToArea(view, targetArea, GetAreaCenterWorldPosition(targetArea), 90f, (TweenCallback)delegate
			{
				//IL_000b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0015: Expected O, but got Unknown
				Object.Destroy((Object)((Component)view).gameObject);
				int num2 = remaining;
				remaining = num2 - 1;
				if (remaining == 0)
				{
					TweenCallback val3 = onComplete;
					if (val3 != null)
					{
						val3.Invoke();
					}
				}
			});
		}
		if (remaining == 0)
		{
			TweenCallback val2 = onComplete;
			if (val2 != null)
			{
				val2.Invoke();
			}
		}
	}

	private void AnimateCardToArea(HandCardView view, RectTransform targetParent, Vector3 targetWorldPosition, float targetZRotation, TweenCallback onComplete)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected O, but got Unknown
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Expected O, but got Unknown
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Expected O, but got Unknown
		Vector3 position = ((Transform)view.RectTransform).position;
		Quaternion rotation = ((Transform)view.RectTransform).rotation;
		ShortcutExtensions.DOKill((Component)view.RectTransform, false);
		((Transform)view.RectTransform).SetParent((Transform)targetParent, true);
		((Transform)view.RectTransform).position = position;
		((Transform)view.RectTransform).rotation = rotation;
		Sequence obj = DOTween.Sequence();
		TweenSettingsExtensions.Join(obj, (Tween)TweenSettingsExtensions.SetEase<TweenerCore<Vector3, Vector3, VectorOptions>>(ShortcutExtensions.DOMove((Transform)view.RectTransform, targetWorldPosition, layoutDuration, false), layoutEase));
		TweenSettingsExtensions.Join(obj, (Tween)TweenSettingsExtensions.SetEase<TweenerCore<Quaternion, Vector3, QuaternionOptions>>(ShortcutExtensions.DORotate((Transform)view.RectTransform, new Vector3(0f, 0f, targetZRotation), layoutDuration, (RotateMode)0), layoutEase));
		TweenSettingsExtensions.SetTarget<Sequence>(obj, (object)this);
		TweenSettingsExtensions.OnComplete<Sequence>(obj, onComplete);
	}

	private void SetButtonsInteractable(bool interactable)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		if ((Object)refreshButton != (Object)null)
		{
			refreshButton.interactable = interactable && (!refreshUsedThisTurn || playerState.GetBuffStack(BuffEnum.ExtraRefresh) > 0);
		}
		if ((Object)endTurnButton != (Object)null)
		{
			endTurnButton.interactable = interactable;
		}
	}

	private RectTransform GetDiscardPileArea()
	{
		return deckPileArea;
	}

	private static Vector3 GetAreaCenterWorldPosition(RectTransform area)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		Vector3[] array = (Vector3[])(object)new Vector3[4];
		area.GetWorldCorners(array);
		return (array[0] + array[2]) * 0.5f;
	}
}
