using System;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public class HandSystemUI : MonoBehaviour
{
	private class EnemyViewState
	{
		public EnemyModel model;

		public EnemyViewUI viewUI;

		public RectTransform viewRect;

		public RectTransform motionRoot;

		public TMP_Text nameText;

		public Image healthFill;

		public Image intentIcon;

		public Image bodyImage;

		public Color bodyBaseColor;

		public Image healthBufferFill;

		public Image shieldFill;

		public TMP_Text healthText;

		public TMP_Text shieldText;
	
			public RectTransform buffRoot;


			public BuffPopupEffectController buffPopupEffect;

			public Graphic focusMarker;


		public RectTransform intentRoot;

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
	private RectTransform discardPileArea;

	[SerializeField]
	private RectTransform consumedPileArea;

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
	private RectTransform enemyIntentViewPrefab;

	[SerializeField]
	private RectTransform floatingTextPrefab;

	[SerializeField]
	private RectTransform buffSlotPrefab;

		[SerializeField]
		private Button refreshButton;

		[SerializeField]
		private Image refreshChanceBadgeImage;

		[SerializeField]
		private TMP_Text refreshChanceText;

		[SerializeField]
		private Color refreshChanceBadgeColor = new Color(0.95f, 0.42f, 0.16f, 1f);

		[SerializeField]
		private Color refreshChanceBadgeDisabledColor = new Color(0.35f, 0.35f, 0.35f, 1f);

		[SerializeField]
		private Color refreshChanceTextColor = Color.white;

		[SerializeField]
		private Color refreshChanceTextDisabledColor = new Color(0.75f, 0.75f, 0.75f, 1f);

		[SerializeField]
		private Button endTurnButton;

		[SerializeField]
		private Sprite endTurnResolveSprite;

		[SerializeField]
		private Sprite endTurnPrepareSprite;

		private TMP_Text endTurnButtonText;

		private Image endTurnButtonImage;

		private Sprite endTurnButtonDefaultSprite;

    private bool buttonsInteractable = true;

	[SerializeField]
	private TMP_Text deckCountText;

	[SerializeField]
	private TMP_Text discardCountText;

	[SerializeField]
	private TMP_Text consumedCountText;

	[Header("Buff栏参数")]
	[SerializeField]
	private float buffSlotSize = 42f;

	[SerializeField]
	private float buffSlotSpacing = 6f;

	[SerializeField]
	private float cardSpacing = 118f;

    [SerializeField]
    private float handLayoutY;

    [SerializeField]
    private float handLayoutZ;

	[SerializeField]
	private float playCardSpacing = 118f;

    [SerializeField]
    private float cardHoverYOffset = 32f;

    [SerializeField]
    private float cardHoverScale = 1.18f;

    [SerializeField]
    private float playLayoutY;

    [SerializeField]
    private float playLayoutZ;

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

	[SerializeField]
	private float magicDamageHitInterval = 0.15f;

    [Header("玩家施法音效")]
    [SerializeField]
    private float playerCastSwingPitchBase = 1f;

    [SerializeField]
    private float playerCastSwingPitchIncrease = 0.08f;

    [SerializeField]
    private float playerCastSwingPitchMax = 1.45f;

	[Header("敌人死亡碎裂参数")]
	[SerializeField]
	private float enemyDeathScaleDuration = 0.35f;

	[SerializeField]
	private Ease enemyDeathScaleEase = Ease.InBack;

	[SerializeField]
	[FormerlySerializedAs("enemyDissolveDuration")]
	private float enemyDeathExplosionDuration = 0.7f;

	[SerializeField]
	private int enemyDeathShardColumns = 5;

	[SerializeField]
	private int enemyDeathShardRows = 4;

	[SerializeField]
	private float enemyDeathExplosionDistance = 120f;

	[SerializeField]
	private float enemyDeathExplosionDistanceMinMultiplier = 0.62f;

	[SerializeField]
	private float enemyDeathExplosionRandomness = 26f;

	[SerializeField]
	private Vector2 enemyDeathShardScaleRange = new Vector2(0.82f, 1.12f);

	[SerializeField]
	private float enemyDeathShardRotationRange = 170f;

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

    private RectTransform disabledCardPopupRoot;

    private TMP_Text disabledCardPopupText;

    private CanvasGroup disabledCardPopupCanvasGroup;

    private Tween disabledCardPopupTween;

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
	private Ease enemyHealthEase = Ease.OutQuad;

	[SerializeField]
	private float levelSelectAfterMapHideExtraDelay = 0.06f;

	[SerializeField]
	private float levelSelectAfterMapHideFallbackDelay = 0.28f;

		[SerializeField]
		private UIManager uiManager;

    private const string BattleInputConfigResourcePath = "Config/BattleInputConfig";

        private Camera cachedMainCamera;
        private static readonly Rect DefaultWideViewportRect = new Rect(0f, 0.1f, 1f, 0.8f);
        private static readonly Color LetterboxColor = Color.black;

		[Header("出牌输入")]

    [SerializeField]
    [FormerlySerializedAs("mobilePlayInputScreenHeightThreshold")]
    [Range(0f, 1f)]
    private float playInputScreenHeightThreshold = 0.5f;

    [SerializeField]
    private bool simulateMobileInteractionInEditor;

    [SerializeField]
    private Vector2 rewardMagicConfirmCellSize = new Vector2(196f, 92f);

	private readonly List<HandCardView> cardViews = new List<HandCardView>();

		private readonly List<MagicItemView> magicViews = new List<MagicItemView>();

    private readonly List<int> debugMagicDropdownIds = new List<int>();

		private readonly List<MagicItemView> castableMagicViews = new List<MagicItemView>();

		private readonly List<MaterialModel> selectedCards = new List<MaterialModel>();

    private HandCardView layoutHoverCardView;

    private bool cardDragActive;

    private HandCardView draggedCardView;

    private MaterialModel draggedCard;

    private bool dragSourceIsPlayZone;

    private int dragSourceIndex = -1;

    private Vector2 dragStartScreenPosition;

    private bool dragPreviewActive;

    private bool dragPreviewTargetIsPlayZone;

    private int dragPreviewIndex = -1;

    private readonly Vector3[] cardQueueWorldCorners = new Vector3[4];

	private readonly HashSet<HandCardView> newCardViews = new HashSet<HandCardView>();


	private readonly HashSet<MaterialModel> consumedBattleDeckCards = new HashSet<MaterialModel>();

	private readonly List<EnemyModel> enemyModels = new List<EnemyModel>();

	private readonly List<EnemyViewState> enemyViewStates = new List<EnemyViewState>();

    private readonly List<RaycastResult> playInputRaycastResults = new List<RaycastResult>(8);

    private PointerEventData playInputPointerEventData;

    private EventSystem playInputEventSystem;

    private BattleInputConfig battleInputConfig;

    private MaterialModel lastClickedHandCard;

    private float lastHandCardClickTime = -999f;

    private BuffSlotView pinnedBuffTooltipSlot;

		private BattleManager battleManager;

    private TMP_Dropdown debugMagicDropdown;

    private Coroutine debugMagicDropdownShowRoutine;

    private bool debugMagicDropdownInitialized;

    private int debugMagicDropdownSlotIndex = -1;

    private bool debugBattleActive;

	    private RunManager runManager;

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

	[SerializeField]
	private BuffPopupEffectController playerBuffPopupEffect;

	private RectTransform spellParticleEmitter;

	private MagicModel pendingCastParticleMagic;

	private RectTransform pendingCastParticleTarget;

    private readonly List<RectTransform> pendingCastParticleTargets = new List<RectTransform>();

    private readonly List<RectTransform> castParticleTargetBuffer = new List<RectTransform>();

	private Action pendingCastParticleImpactHandler;

	private int pendingCastShakeCount;

    private int castReleaseToken;

    private bool castReleaseHandled;

    private Coroutine castReleaseFallbackRoutine;

	private Image enemyHealthBufferFill;

	private Image enemyShieldFill;

	private TMP_Text enemyHealthText;

	private TMP_Text enemyShieldText;
	
	private int displayedEnemyHealth;


	private Tween enemyHealthNumberTween;


	private ChapterData activeChapter;

	private MagicData pendingRewardMagic;

    [SerializeField]
    private RectTransform rewardMagicConfirmPanel;

    [SerializeField]
    private RectTransform rewardMagicConfirmExistingRoot;

    [SerializeField]
    private RectTransform rewardMagicConfirmNewRoot;

    [SerializeField]
    private Button rewardMagicConfirmButton;

    [SerializeField]
    private Button rewardMagicConfirmCancelButton;

    private int rewardMagicConfirmSlotIndex = -1;

    private RectTransform rewardMagicConfirmSourceRect;

    private MagicData pendingShopMagic;

    private int undoRewardMagicSlotIndex = -1;

    private MagicModel undoRewardPreviousMagic;

    private bool undoRewardAvailable;

    private readonly List<MaterialEnum> forcedRefreshMaterials = new List<MaterialEnum>();

    private Action<int> pendingShopMagicSlotChosen;

	private MagicModifierData pendingMagicModifier;

    private MaterialModifierData pendingMaterialModifier;

	private EventModel currentEvent;

	private EventPanelUI eventPanel;

	private bool refreshUsedThisTurn;

	private bool suppressEnemyIntentRefresh;

	private int currentMapNodeIndex;

    private bool pendingChapterMapBossStart;
    private bool currentChapterMapBossLevel;
    private bool chapterMapMoveInProgress;

	private bool busy;

	private bool choosingEventCard;

	private EventOptionData pendingChoiceOption;

	private int pendingChoiceCount;

	private readonly List<MaterialModel> pendingChoiceCards = new List<MaterialModel>();

		private bool runEnded;

        private string lastRunCheckpointKey;
	
	    private bool eliteMagicModifierRewardResolved;

    private float loadedRunPlaySeconds;

    private float runStartRealtime;

	private TutorialManagerUI TutorialManager => GetUIManager().TutorialManager;

	private const int RestDefaultHealResultId = 300;

	private const int RestStudyResultId = 301;

	private const int RestDeepStudyResultId = 302;

	private const float RestDefaultHealRatio = 0.3f;

    private const float AcquireMagicAnimationDuration = 0.42f;

    private const float AcquireMaterialAnimationDuration = 0.42f;

    private const int BuffRootColumnCount = 5;

    private const int BuffRootRowCount = 2;

    private const float EnemyHealthTextWidth = 56f;

    private const int DiscardShuffleAnimationMaxCards = 18;

    private const float DiscardShuffleAnimationStagger = 0.035f;

    private const string EnemyDeathExplosionMaterialPath = "Materials/Style/Sprite/M_Sprite_FragmentExplosion_Default";

	public PlayerState PlayerState => playerState;

    public RunManager RunManager => runManager;

    public int ActiveChapterNumericId => activeChapter != null ? activeChapter.numericId : 0;

    public int MagicSlotViewCount => magicViews.Count > 0 ? magicViews.Count : (magicBookArea != null ? magicBookArea.GetComponentsInChildren<MagicItemView>(true).Length : 0);

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

    public void DebugStartBattleLevel(LevelData level)
    {
        if (level == null || battleManager == null)
            return;

        HideDebugMagicDropdown();
        debugBattleActive = true;
        currentLevel = level;
        currentEvent = null;
        refreshUsedThisTurn = false;
        ResetBattleDeckState();
        HideMapPanel();
        enemyModels.Clear();
        battleManager.ClearEnemies();
        ClearEnemyViews();
        SpawnDebugLevelEnemies(level);
        if (battleManager.Enemies.Count == 0)
            return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBattleMusic();
        battleManager.BeginBattleRules();
				BeginPlayerTurn(playerState.DrawCount);
                SaveRunProgress();
				ResetMagicHighlights();

        busy = false;
        SetButtonsInteractable(true);
    }

    public void ShowDebugMagicReplacementDropdown(int slotIndex, Vector2 screenPosition)
    {
        if (!debugBattleActive)
            return;

        CacheDebugMagicDropdown();
        if (debugMagicDropdown == null || playerState == null || slotIndex < 0)
            return;

        debugMagicDropdownSlotIndex = slotIndex;
        debugMagicDropdownIds.Clear();
        debugMagicDropdown.ClearOptions();
        List<MagicData> magics = new List<MagicData>(GameDataDatabase.MagicData.Values);
        magics.Sort((left, right) => left.numericId.CompareTo(right.numericId));
        List<string> options = new List<string>(magics.Count + 1) { "选择替换道具" };
        debugMagicDropdownIds.Add(0);
        for (int i = 0; i < magics.Count; i++)
        {
            MagicData data = magics[i];
            debugMagicDropdownIds.Add(data.numericId);
            options.Add($"{data.numericId} {LocalizationSystem.GetText(data.nameKey, data.id)}");
        }

        debugMagicDropdown.ClearOptions();
        debugMagicDropdown.AddOptions(options);
        debugMagicDropdown.SetValueWithoutNotify(0);
        debugMagicDropdown.RefreshShownValue();
        PositionDebugMagicDropdown(screenPosition);

        bool waitForInitialization = !debugMagicDropdownInitialized;
        debugMagicDropdown.gameObject.SetActive(true);
        if (debugMagicDropdownShowRoutine != null)
            StopCoroutine(debugMagicDropdownShowRoutine);

        if (waitForInitialization)
        {
            debugMagicDropdownShowRoutine = StartCoroutine(ShowDebugMagicDropdownAfterInitialization());
            return;
        }

        debugMagicDropdown.Show();
    }

    private IEnumerator ShowDebugMagicDropdownAfterInitialization()
    {
        yield return null;
        debugMagicDropdownShowRoutine = null;
        debugMagicDropdownInitialized = true;
        if (!debugBattleActive || debugMagicDropdown == null || playerState == null || debugMagicDropdownSlotIndex < 0 || !debugMagicDropdown.gameObject.activeInHierarchy)
            yield break;

        debugMagicDropdown.Show();
    }

    private void SpawnDebugLevelEnemies(LevelData level)
    {
        if (level.randomEnemyGroups != null && level.randomEnemyGroups.Length > 0)
        {
            LevelEnemyGroupData group = level.randomEnemyGroups[NextRunRandomInt(0, level.randomEnemyGroups.Length)];
            if (group?.enemies == null)
                return;
            for (int i = 0; i < group.enemies.Length; i++)
                battleManager.SpawnEnemy(group.enemies[i]);
            return;
        }

        if (level.enemies != null && level.enemies.Length > 0)
        {
            for (int i = 0; i < level.enemies.Length; i++)
                battleManager.SpawnEnemy(level.enemies[i]);
            return;
        }

        if (level.enemyIds == null)
            return;

        for (int i = 0; i < level.enemyIds.Length; i++)
            battleManager.SpawnEnemy(level.enemyIds[i]);
    }

    private void CacheDebugMagicDropdown()
    {
        if (debugMagicDropdown != null)
            return;

        Transform dropdownTransform = transform.Find("DebugMagicDropdown");
        if (dropdownTransform == null)
            return;

        debugMagicDropdown = dropdownTransform.GetComponent<TMP_Dropdown>();
        if (debugMagicDropdown != null)
        {
            debugMagicDropdown.onValueChanged.RemoveListener(OnDebugMagicDropdownValueChanged);
            debugMagicDropdown.onValueChanged.AddListener(OnDebugMagicDropdownValueChanged);
        }
    }

    private void PositionDebugMagicDropdown(Vector2 screenPosition)
    {
        RectTransform dropdownRect = debugMagicDropdown != null ? debugMagicDropdown.transform as RectTransform : null;
        RectTransform canvasRect = transform as RectTransform;
        if (dropdownRect == null || canvasRect == null)
            return;

        Camera camera = GetComponentInParent<Canvas>()?.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, camera, out Vector2 localPoint);
        dropdownRect.anchoredPosition = localPoint;
    }

    private void OnDebugMagicDropdownValueChanged(int optionIndex)
    {
        if (optionIndex <= 0 || optionIndex >= debugMagicDropdownIds.Count || playerState == null || debugMagicDropdownSlotIndex < 0)
            return;

        if (GameDataDatabase.TryGetMagicData(debugMagicDropdownIds[optionIndex], out MagicData magicData))
        {
            playerState.SetMagicAtSlot(MagicFactory.Create(magicData, debugMagicDropdownSlotIndex), debugMagicDropdownSlotIndex);
            CreateMagicViews();
            RefreshStaticUI();
        }

        HideDebugMagicDropdown();
    }

    private void HideDebugMagicDropdown()
    {
        if (debugMagicDropdownShowRoutine != null)
        {
            StopCoroutine(debugMagicDropdownShowRoutine);
            debugMagicDropdownShowRoutine = null;
        }

        if (debugMagicDropdown != null)
            debugMagicDropdown.gameObject.SetActive(false);
        debugMagicDropdownSlotIndex = -1;
    }

    private void DebugApplyRandomMaterialModifiersToDeck()
    {
        if (playerState == null || playerState.Deck.Count == 0)
            return;

        List<MaterialModifierData> pool = GetDebugMaterialModifierPool();
        if (pool.Count == 0)
            return;

        int appliedCount = 0;
        for (int i = 0; i < playerState.Deck.Count; i++)
        {
            MaterialModel card = playerState.Deck[i];
            if (card == null || card.material == MaterialEnum.None)
                continue;

            MaterialModifierData data = pool[NextRunRandomInt(0, pool.Count)];
            MaterialModifierModel modifier = MaterialModifierFactory.Create(data);
            if (modifier == null)
                continue;

            card.AddModifier(modifier);
            appliedCount++;
        }

        if (appliedCount == 0)
            return;

        ClearSelectedCards(true);
        RefreshMaterialListPanel();
        RebuildCards(animateFromCurrent: true);
        RefreshStaticUI();
        SaveRunProgress();
        GameLog.Data($"Debug apply random material modifiers to deck count={appliedCount}");
    }

    private List<MaterialModifierData> GetDebugMaterialModifierPool()
    {
        DataTable<MaterialModifierData> table = GameDataReader.LoadTable<MaterialModifierData>("MaterialModifierData");
        List<MaterialModifierData> pool = new List<MaterialModifierData>();
        for (int i = 0; table != null && table.items != null && i < table.items.Count; i++)
        {
            MaterialModifierData data = table.items[i];
            if (data != null && !string.IsNullOrEmpty(data.script) && MaterialModifierFactory.Create(data) != null)
                pool.Add(data);
        }
        return pool;
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

    public RunMapGridModel ChapterMapGrid => runManager != null ? runManager.MapGrid : null;

	public int CurrentMapNodeIndex => currentMapNodeIndex;

	private const int RunNodeCount = 21;

	private const int DebugRewardLevelNumericId = 301;

	private const int FirstFixedEventNodeIndex = 7;

	private const int SecondFixedEventNodeIndex = 15;

	private void ShowLevelSelect()
	{
        chapterMapMoveInProgress = false;
		busy = true;
		SetButtonsInteractable(interactable: false);
		HideLevelSelectPanel();
        EnsureChapterMapGrid();
        RunMapGridModel grid = ChapterMapGrid;
        if (grid == null || grid.CellCount == 0)
        {
            ShowVictoryPanel();
            return;
        }

        ChapterGridPanelUI panel = GetUIManager().ChapterGridPanel;
        if (panel != null)
            panel.SetDirectionCardSource(handArea, cardPrefab, cardSpacing);
        GetUIManager().ShowChapterGridPanel(grid);
        TutorialManager?.OnLevelSelectShown(currentMapNodeIndex);
        if (ShouldActivateBossMapForCurrentSelection(grid))
        {
            if (panel != null)
            {
                panel.SetInputLocked(true);
                panel.PlayBossTransform(() =>
                {
                    ActivateBossMapForCurrentSelection(panel);
                    panel.SetInputLocked(false);
                });
            }
            else
            {
                ActivateBossMapForCurrentSelection(null);
            }
        }
	}

    private bool ShouldActivateBossMapForCurrentSelection(RunMapGridModel grid)
    {
        ChapterData chapter = activeChapter ?? GetActiveChapter();
        return grid != null && grid.CellCount > 0 && !grid.bossMapActive && (chapter == null || chapter.numericId != TutorialManagerUI.TutorialChapterNumericId) && currentMapNodeIndex >= Mathf.Max(1, GetActiveChapterLength()) - 1;
    }

    private void ActivateBossMapForCurrentSelection(ChapterGridPanelUI panel)
    {
        runManager?.ActivateBossMap();
        panel?.RefreshCellVisuals();
        panel?.RefreshTexts();
        SaveRunProgress();
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
		List<LevelData> levels = GetLevels(LevelType.Battle);
		RemoveBossBattleLevel(levels, GetBossBattleLevel(levels));
		return levels;
	}

	private LevelData GetBossBattleLevel()
	{
		return GetBossBattleLevel(null);
	}

	private LevelData GetBossBattleLevel(List<LevelData> battleLevels)
	{
		LevelData bossLevel = null;
		if (battleLevels != null)
		{
			for (int i = 0; i < battleLevels.Count; i++)
			{
				LevelData level = battleLevels[i];
				if (level != null && IsBattleLevel(level) && (bossLevel == null || level.numericId > bossLevel.numericId))
					bossLevel = level;
			}
			return bossLevel;
		}

		foreach (LevelData level in GameDataDatabase.LevelData.Values)
		{
			if (level != null && IsBattleLevel(level) && (bossLevel == null || level.numericId > bossLevel.numericId))
				bossLevel = level;
		}
		return bossLevel;
	}

	private static bool IsBattleLevel(LevelData level)
	{
		return level != null && (level.levelType == LevelType.Battle || level.levelType == LevelType.Elite);
	}

	private void RemoveBossBattleLevel(List<LevelData> levels, LevelData bossLevel)
	{
		if (levels == null || bossLevel == null)
			return;

		for (int i = levels.Count - 1; i >= 0; i--)
		{
			LevelData level = levels[i];
			if (IsBattleLevel(level) && level.numericId == bossLevel.numericId)
				levels.RemoveAt(i);
		}
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
        bool chapterMapBossStart = pendingChapterMapBossStart;
        currentChapterMapBossLevel = chapterMapBossStart;
        LevelData selectedMapLevel = level;
        level = ResolveSelectedMapLevel(level);
        pendingChapterMapBossStart = false;
        chapterMapMoveInProgress = false;
        if (level == null)
            return;

		GameLog.Data($"Start level node={currentMapNodeIndex + 1}/{mapNodes.Count} id={level.id} type={level.levelType}");
		currentLevel = level;
		runManager?.SelectCurrentNodeLevel(level);
		if (currentMapNodeIndex >= 0 && currentMapNodeIndex < mapNodes.Count)
		{
			mapNodes[currentMapNodeIndex].selectedLevel = selectedMapLevel ?? level;
            RevealSelectedMapLevel(mapNodes[currentMapNodeIndex], selectedMapLevel ?? level);
			GetUIManager().MapPanel?.RefreshNodeVisual(currentMapNodeIndex);
			GetUIManager().MapPanel?.SetPlayerMarkerNodeIndex(currentMapNodeIndex);
		}
        SaveRunProgress();
        busy = true;
        SetButtonsInteractable(interactable: false);
        HideMapPanel();
        HideLevelSelectPanelAnimated(() => StartLevelAfterSelectClosed(level));
	}

	private void RevealSelectedMapLevel(RunMapNodeModel node, LevelData selectedLevel)
	{
		if (node == null || selectedLevel == null)
			return;

		if (node.leftLevel == selectedLevel)
			node.leftHidden = false;
		if (node.rightLevel == selectedLevel)
			node.rightHidden = false;
	}

	private LevelData ResolveSelectedMapLevel(LevelData level)
	{
		if (level == null)
			return null;

        if (currentChapterMapBossLevel)
            return level;

		ChapterData chapter = activeChapter ?? GetActiveChapter();
		if (level.levelType == LevelType.Battle)
			return DrawMapBattleLevel(chapter, level);
		if (level.levelType == LevelType.Event)
			return DrawMapEventLevel(chapter, level);
		if (level.levelType == LevelType.Elite)
			return runManager != null ? runManager.DrawEliteLevel(chapter) ?? level : level;
		return level;
	}

    private static bool IsEventLikeLevel(LevelType levelType)
    {
        return levelType == LevelType.Event || levelType == LevelType.RemoveMaterial || levelType == LevelType.AddMaterial;
    }

        private void StartSavedLevel(LevelData level, RunSaveData saveData)
        {
            if (level == null)
                return;

            currentLevel = level;
            runManager?.SelectCurrentNodeLevel(level);
            if (currentMapNodeIndex >= 0 && currentMapNodeIndex < mapNodes.Count)
            {
                mapNodes[currentMapNodeIndex].selectedLevel = level;
                RevealSelectedMapLevel(mapNodes[currentMapNodeIndex], level);
                GetUIManager().MapPanel?.RefreshNodeVisual(currentMapNodeIndex);
                GetUIManager().MapPanel?.SetPlayerMarkerNodeIndex(currentMapNodeIndex);
            }
            busy = true;
            SetButtonsInteractable(false);
            HideMapPanel();
            HideLevelSelectPanelAnimated(() => StartLevelAfterSelectClosed(level, saveData));
        }

		private void StartLevelAfterSelectClosed(LevelData level)
        {
            StartLevelAfterSelectClosed(level, null);
        }

		private void StartLevelAfterSelectClosed(LevelData level, RunSaveData saveData)
	    {
			if (IsEventLikeLevel(level.levelType))
			{
				StartEventLevel(level, RunSaveSystem.GetSavedEvent(saveData, level));
			}
			else if (level.levelType == LevelType.Shop)
			{
				StartShopLevel(level, RunSaveSystem.GetSavedShop(saveData, level));
			}
			else if (level.levelType == LevelType.Rest)
			{
				StartRestLevel(level, RunSaveSystem.GetSavedEvent(saveData, level));
			}
			else if (level.levelType == LevelType.Reward)
			{
				StartRewardLevel(level);
			}
			else
			{
				StartBattleLevel(level, RunSaveSystem.GetSavedBattle(saveData));
			}
		}


		private void StartBattleLevel(LevelData level, BattleNodeSaveData savedBattle = null)
		{

		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
        int configuredEnemyCount = level.randomEnemyGroups != null && level.randomEnemyGroups.Length > 0
            ? level.randomEnemyGroups[0].enemies.Length
            : level.enemies != null && level.enemies.Length > 0 ? level.enemies.Length : (level.enemyIds != null ? level.enemyIds.Length : 0);
		GameLog.Data("Start battle level=" + level.id + " enemies=" + configuredEnemyCount);
        if (AudioManager.Instance != null)
        {
            if (currentChapterMapBossLevel)
                AudioManager.Instance.PlayBossBattleMusic();
            else
                AudioManager.Instance.PlayBattleMusic();
        }
		currentLevel = level;
		runManager?.BeginLevel(level);
        runManager?.SetBattle(battleManager);
			currentEvent = null;
			refreshUsedThisTurn = false;
			ResetBattleDeckState();
			HideMapPanel();
			enemyModels.Clear();
			battleManager.ClearEnemies();
        ClearEnemyViews();
        if (savedBattle != null)
        {
            RunSaveSystem.RestoreBattle(savedBattle, battleManager, playerState);
            RefreshStaticUI();
            RefreshMaterialListPanel();
            RebuildCards(true);
            RefreshEnemyUI((RectTransform)null, true);
            ResetMagicHighlights();
            busy = false;
            SetButtonsInteractable(true);
            SaveRunProgress();
            return;
        }
        runManager?.AdvanceRunRandomStep();
        SaveRunProgress();

		bool tutorialBattle = TutorialManager != null && TutorialManager.ShouldUseTutorialBattle(level);
		if (tutorialBattle)
		{
			TutorialManager.BeginTutorialBattle();
		}
        if (level.randomEnemyGroups != null && level.randomEnemyGroups.Length > 0)
        {
            LevelEnemyGroupData group = level.randomEnemyGroups[NextRunRandomInt(0, level.randomEnemyGroups.Length)];
            if (group != null && group.enemies != null)
            {
                for (int i = 0; i < group.enemies.Length; i++)
                {
                    battleManager.SpawnEnemy(group.enemies[i]);
                }
            }
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
            battleManager.BeginBattleRules();
				BeginPlayerTurn(playerState.DrawCount);
                SaveRunProgress();
				ResetMagicHighlights();
				busy = false;

			SetButtonsInteractable(interactable: true);
		}
	}

		private void BeginPlayerTurn(int drawCount)
		{
			battleManager?.BeginPlayerTurnStartRules(drawCount, TryApplyFixedTutorialHand);
			refreshUsedThisTurn = false;
			GetUIManager().TurnBanner?.Show("你的回合");
			ResetContinuousCastCounterUI();
			suppressEnemyIntentRefresh = false;
			RefreshEnemyUI();
			GetUIManager().PlayerFeedback?.UpdateVignetteRange(playerState);
			RefreshStaticUI();
			RefreshMaterialListPanel();
			RebuildCards(animateFromCurrent: true);
		}


		private bool TryApplyFixedTutorialHand()
		{
			return TutorialManager != null && TutorialManager.TryApplyFixedTurnHand(playerState);
		}


			private void StartShopLevel(LevelData level, ShopNodeSaveData savedShopState = null)

		{

		GameLog.Data("Start shop level=" + level.id);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameplayMusic();
		ResetBattleDeckState();
		currentLevel = level;
		runManager?.BeginLevel(level);
        currentEvent = null;
        HideRewardMagicConfirmPanel(false);
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
            if (savedShopState == null)
            {
                RunSaveData currentRunSave = RunSaveSystem.LoadCurrentRun();
                savedShopState = currentRunSave != null
                    && currentRunSave.currentNode != null
                    && currentRunSave.currentNode.levelId == level.numericId
                    ? currentRunSave.currentNode.shop
                    : null;
            }
				GetUIManager().ShowShopPanel(level, savedShopState);
            SaveRunProgress();

	        TutorialManager?.OnShopPanelShown();

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

		private void StartRestLevel(LevelData level, EventNodeSaveData savedEvent = null)
		{

		GameLog.Data("Start rest level=" + level.id);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameplayMusic();
		ResetBattleDeckState();
		currentLevel = level;
		runManager?.BeginLevel(level);
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
            currentEvent.RestoreSaveData(savedEvent);
				eventPanel.Bind(currentEvent);
            SaveRunProgress();

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
		runManager?.BeginLevel(level);
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
		playerState.DrawBasicMaterialCards(bonusData != null && bonusData.drawCount > 0 ? bonusData.drawCount : 5);
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

		private void StartEventLevel(LevelData level, EventNodeSaveData savedEvent = null)
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
		runManager?.BeginLevel(level);
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
            currentEvent.RestoreSaveData(savedEvent);

		if ((Object)eventPanel != (Object)null)
		{
			eventPanel.Close();
		}
		eventPanel = GetOrCreateEventPanel();
		EventPanelUI eventPanelUI = eventPanel;
		Transform transform = ((Component)this).transform;
		eventPanelUI.Initialize((RectTransform)((transform is RectTransform) ? transform : null), GetDefaultFont(), DrawEventOptionsHand);
			eventPanel.Bind(currentEvent);
            SaveRunProgress();
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

		int drawCount = currentEvent.Data.drawCount >= 0 ? currentEvent.Data.drawCount : playerState.DrawCount;
		GameLog.Data($"Draw event options hand count={drawCount}");
		refreshUsedThisTurn = false;
        if (TutorialManager != null && TutorialManager.ShouldUseTutorialEventFixedDraw(currentEvent.Data))
        {
            List<MaterialModel> removedTemporaryCards = new List<MaterialModel>();
            playerState.ReturnHandCardsToDiscardPile(new List<MaterialModel>(playerState.Hand), removedTemporaryCards);
            playerState.ReturnPlayZoneCardsToDiscardPile(removedTemporaryCards);
            playerState.DrawSpecificMaterialsToHand(new[] { MaterialEnum.Wind, MaterialEnum.Wind }, true);
            TutorialManager.OnEventOptionsShown();
        }
        else
        {
		    playerState.DrawCards(drawCount);
        }
		RefreshStaticUI();
		RefreshMaterialListPanel();
		RebuildCards(animateFromCurrent: true);
	}

    private EventModel CreateRestEventModel(LevelData level)
    {
        EventOptionData defaultRest = new EventOptionData
        {
            id = "default_rest",
            titleKey = "rest.option.rest",
            resultId = RestDefaultHealResultId,
            nextNodeId = "rest_result",
            isExitOption = true
        };
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
        string[] restTextKeys = level != null && level.restTextKeys != null ? level.restTextKeys : Array.Empty<string>();
        string startTextKey = restTextKeys.Length > 0 ? restTextKeys[0] : string.Empty;
        string resultTextKey = restTextKeys.Length > 1 ? restTextKeys[1] : startTextKey;
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
                    textKeys = !string.IsNullOrEmpty(startTextKey) ? new[] { startTextKey } : Array.Empty<string>(),
                    options = new[] { defaultRest, study, deepStudy }
                },
                new EventNodeData
                {
                    id = "rest_result",
                    textKeys = !string.IsNullOrEmpty(resultTextKey) ? new[] { resultTextKey } : Array.Empty<string>(),
                    options = Array.Empty<EventOptionData>()
                }
            }
        };
        return new EventModel(data);
    }

    private string CreateRandomRecipe(int length)
    {
        char[] recipe = new char[Mathf.Max(1, length)];
        for (int i = 0; i < recipe.Length; i++)
            recipe[i] = (char)('0' + NextRunRandomInt(1, 5));
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

    public void ShowEnemyIntentTooltip(EnemyIntentView view, EnemyModel enemy, EnemyIntentData intent, PlayerState intentPlayerState)
    {
        GetUIManager().ShowEnemyIntentTooltip(view, enemy, intent, intentPlayerState);
    }

    public void HideEnemyIntentTooltip(EnemyIntentView view)
    {
        GetUIManager().HideEnemyIntentTooltip(view);
    }

    public void TogglePinnedBuffTooltip(BuffSlotView slot, BuffModel buff)
    {
        if (slot == null || buff == null)
            return;

        UIManager uiManager = GetUIManager();
        UnifiedDetailPopupUI popup = uiManager != null ? uiManager.UnifiedDetailPopup : null;
        if (popup != null && popup.IsPinnedFor(slot))
        {
            pinnedBuffTooltipSlot = null;
            uiManager.UnpinUnifiedDetailPopup();
            return;
        }

        pinnedBuffTooltipSlot = slot;
        uiManager?.PinBuffTooltip(slot, buff);
    }

    public void ClearPinnedBuffTooltip(BuffSlotView slot)
    {
        if (pinnedBuffTooltipSlot != slot)
            return;

        pinnedBuffTooltipSlot = null;
    }

    private void HidePinnedBuffTooltipOnOutsideClick()
    {
        if (pinnedBuffTooltipSlot == null || !TryGetPrimaryPointerDown(out Vector2 screenPosition))
            return;

        UIManager uiManager = GetUIManager();
        UnifiedDetailPopupUI popup = uiManager != null ? uiManager.UnifiedDetailPopup : null;
        if (popup != null && popup.ContainsScreenPoint(screenPosition))
            return;

        if (IsPointerOverBuffSlot(screenPosition))
            return;

        BuffSlotView slot = pinnedBuffTooltipSlot;
        pinnedBuffTooltipSlot = null;
        if (popup != null && popup.IsPinnedFor(slot))
            uiManager.UnpinUnifiedDetailPopup();
    }

    private bool TryGetPrimaryPointerDown(out Vector2 screenPosition)
    {
        screenPosition = default;
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        if (Input.touchCount <= 0)
            return false;

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began)
            return false;

        screenPosition = touch.position;
        return true;
    }

    private bool IsPointerOverBuffSlot(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        if (playInputPointerEventData == null || playInputEventSystem != eventSystem)
        {
            playInputEventSystem = eventSystem;
            playInputPointerEventData = new PointerEventData(eventSystem);
        }

        playInputPointerEventData.Reset();
        playInputPointerEventData.position = screenPosition;
        playInputPointerEventData.button = PointerEventData.InputButton.Left;
        playInputRaycastResults.Clear();
        eventSystem.RaycastAll(playInputPointerEventData, playInputRaycastResults);

        for (int i = 0; i < playInputRaycastResults.Count; i++)
        {
            GameObject hitObject = playInputRaycastResults[i].gameObject;
            if (hitObject != null && hitObject.GetComponentInParent<BuffSlotView>() != null)
                return true;
        }

        return false;
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

	private Graphic EnsureFocusMarker(RectTransform enemyView)
	{
		Transform val = ((Transform)enemyView).Find("FocusMarker");
		Graphic marker = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<Graphic>() : null);
		if ((Object)marker == (Object)null)
		{
			marker = new GameObject("FocusMarker", new Type[3]
			{
				typeof(RectTransform),
				typeof(CanvasRenderer),
				typeof(SpringLineHighlightUI)
			}).GetComponent<Graphic>();
			((Component)marker).transform.SetParent((Transform)enemyView, false);
		}
		marker.color = new Color(1f, 0.08f, 0.06f, 1f);
		marker.raycastTarget = false;
		RectTransform component = ((Component)marker).GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.5f, 0.5f);
		component.anchorMax = new Vector2(0.5f, 0.5f);
		component.pivot = new Vector2(0.5f, 0.5f);
		component.anchoredPosition = new Vector2(0f, 40f);
		component.sizeDelta = new Vector2(34f, 34f);
		((Transform)component).localEulerAngles = Vector3.zero;
		((Component)marker).gameObject.SetActive(false);
		return marker;
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
		bool created = false;
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
			created = true;
		}
		if (created)
		{
			val2.anchorMin = new Vector2(0.5f, 0.5f);
			val2.anchorMax = new Vector2(0.5f, 0.5f);
			val2.pivot = new Vector2(0.5f, 0.5f);
			val2.anchoredPosition = anchoredPosition;
			float slotSize = BuffSlotSize;
			float slotSpacing = BuffSlotSpacing;
			val2.sizeDelta = new Vector2(slotSize * BuffRootColumnCount + slotSpacing * (BuffRootColumnCount - 1), slotSize * BuffRootRowCount + slotSpacing * (BuffRootRowCount - 1));
		}
		ConfigureBuffRootLayout(val2);
		return val2;
	}

	private void ConfigureBuffRootLayout(RectTransform root)
	{
		if ((Object)root == (Object)null)
			return;

		float slotSize = BuffSlotSize;
		float slotSpacing = BuffSlotSpacing;
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
			if (value.stack > 0 && value.IsVisible)
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
		component.color = new Color(0.08f, 0.08f, 0.1f, 1f);
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
        runManager?.SetActiveChapter(chapter);
		List<LevelData> eventLevels = GetEventLevelsForChapter(chapter);
		int chapterLength = GetActiveChapterLength();
		LevelData bossLevel = runManager != null ? runManager.DrawBossLevel(chapter) : GetBossBattleLevel();
		for (int i = 0; i < chapterLength; i++)
		{
			int progress = i + 1;
			RunMapNodeModel mapNodeModel = new RunMapNodeModel();
			if (progress == chapterLength && bossLevel != null)
			{
				mapNodeModel.leftLevel = bossLevel;
				mapNodeModel.rightLevel = bossLevel;
				mapNodeModel.fixedSingleChoice = true;
				ApplyHiddenRolls(chapter, mapNodeModel);
				mapNodes.Add(mapNodeModel);
				continue;
			}

			List<LevelData> candidateLevels = GetLevelsForProgress(chapter, progress);
			RemoveBossBattleLevel(candidateLevels, bossLevel);
			if (candidateLevels.Count == 0)
				candidateLevels = GetBattleLevels();
			if (candidateLevels.Count == 0)
				return;
			if (i == 0 && TutorialManager != null && TutorialManager.ShouldForceFirstNodeBattles())
			{
				List<LevelData> battleLevels = GetBattleLevels();
				if (battleLevels.Count == 0)
					return;
                mapNodeModel.leftLevel = battleLevels[NextRunRandomInt(0, battleLevels.Count)];
                mapNodeModel.rightLevel = battleLevels[NextRunRandomInt(0, battleLevels.Count)];
                ApplyHiddenRolls(chapter, mapNodeModel);
				mapNodes.Add(mapNodeModel);
				continue;
			}
			LevelData fixedLevel = GetFixedLevelForProgress(chapter, progress);
			if (fixedLevel != null)
			{
				mapNodeModel.leftLevel = fixedLevel;
				mapNodeModel.rightLevel = fixedLevel;
				mapNodeModel.fixedSingleChoice = true;
			}
			else
			{
				mapNodeModel.leftLevel = ChooseRandomMapLevel(chapter, candidateLevels);
				mapNodeModel.rightLevel = ChooseRandomMapLevel(chapter, candidateLevels);
			}
			ApplyHiddenRolls(chapter, mapNodeModel);
			mapNodes.Add(mapNodeModel);
		}
		currentMapNodeIndex = 0;
        runManager?.SetCurrentMapNodeIndex(currentMapNodeIndex);
        BuildChapterMapGrid(chapter);
		RefreshChapterProgressUI();
		GetUIManager().MapPanel?.SetPlayerMarkerNodeIndex(currentMapNodeIndex);
		GameLog.Data($"Build map nodes={mapNodes.Count} randomizedEvents={eventLevels.Count}");
	}

	private void BuildChapterMapGrid(ChapterData chapter)
    {
        if (runManager == null)
            return;

        int width = Mathf.Max(1, chapter != null && chapter.mapWidth > 0 ? chapter.mapWidth : 5);
        int height = Mathf.Max(1, chapter != null && chapter.mapHeight > 0 ? chapter.mapHeight : 5);
        int cellCount = width * height;
        List<LevelData> levels = new List<LevelData>(cellCount);
        for (int i = 0; i < cellCount; i++)
            levels.Add(null);

        if (chapter != null && chapter.numericId == TutorialManagerUI.TutorialChapterNumericId)
        {
            BuildTutorialChapterMapGrid(levels, width, height);
            runManager.BuildMapGrid(chapter, levels);
            ApplyTutorialChapterMapCellFlags();
            return;
        }

        LevelData bossLevel = GetChapterBossPreviewLevel(chapter);
        if (width == 5 && height == 8)
            BuildDesignedChapterMapGrid(chapter, levels, width, height, bossLevel);
        else
            BuildWeightedChapterMapGrid(chapter, levels, width, height, bossLevel);

        runManager.BuildMapGrid(chapter, levels);
        if (width == 5 && height == 8)
            ApplyDesignedChapterMapCellFlags();
    }

    private void BuildTutorialChapterMapGrid(List<LevelData> levels, int width, int height)
    {
        SetMapGridLevel(levels, width, 0, 1, GetLevelData(TutorialManagerUI.TutorialBattleLevelId));
        SetMapGridLevel(levels, width, 0, 2, GetLevelData(TutorialManagerUI.TutorialEventLevelId));
        SetMapGridLevel(levels, width, 0, 3, GetLevelData(TutorialManagerUI.TutorialShopLevelId));
        SetMapGridLevel(levels, width, 0, 4, GetLevelData(TutorialManagerUI.TutorialRestLevelId));
        SetMapGridLevel(levels, width, 0, 5, GetLevelData(TutorialManagerUI.TutorialBossLevelId));
    }

    private void ApplyTutorialChapterMapCellFlags()
    {
        RunMapGridModel grid = ChapterMapGrid;
        if (grid == null)
            return;

        for (int i = 0; i < grid.cells.Count; i++)
        {
            RunMapCellModel cell = grid.cells[i];
            if (cell == null)
                continue;

            bool isStartCell = cell.x == grid.playerX && cell.y == grid.playerY;
            cell.isAvailable = isStartCell || cell.level != null;
            cell.isBoss = cell.level != null && cell.level.numericId == TutorialManagerUI.TutorialBossLevelId;
            cell.isRevealed = cell.isBoss;
        }
        runManager?.RevealCurrentMapNeighbors();
    }

    private static LevelData GetLevelData(int numericId)
    {
        return GameDataDatabase.TryGetLevelData(numericId, out LevelData level) ? level : null;
    }

    private void BuildWeightedChapterMapGrid(ChapterData chapter, List<LevelData> levels, int width, int height, LevelData bossLevel)
    {
        int cellCount = width * height;
        for (int i = 0; i < cellCount; i++)
        {
            int progress = i + 1;
            List<LevelData> candidateLevels = GetMapGridCandidateLevels(chapter, progress, bossLevel);
            levels[i] = ChooseRandomMapLevel(chapter, candidateLevels);
        }
    }

    private void BuildDesignedChapterMapGrid(ChapterData chapter, List<LevelData> levels, int width, int height, LevelData bossLevel)
    {
        SetMapGridLevel(levels, width, 2, 7, bossLevel);
        SetMapGridLevel(levels, width, 2, 2, ChooseRandomMapLevel(chapter, GetBattleLevels()));
        SetMapGridLevel(levels, width, 2, 1, ChooseRandomMapLevel(chapter, GetBattleLevels()));

        List<Vector2Int> positions = new List<Vector2Int>();
        for (int y = 6; y >= 2; y--)
        {
            for (int x = 0; x < width; x++)
            {
                if (x == 2 && y == 2)
                    continue;
                positions.Add(new Vector2Int(x, y));
            }
        }

        AssignDesignedMapLevels(chapter, levels, width, positions, LevelType.Shop, 1);
        AssignDesignedMapLevels(chapter, levels, width, positions, LevelType.Battle, 3);
        AssignDesignedMapLevels(chapter, levels, width, positions, LevelType.Elite, 2);
        AssignDesignedMapLevels(chapter, levels, width, positions, LevelType.RemoveMaterial, 1);
        AssignDesignedMapLevels(chapter, levels, width, positions, LevelType.AddMaterial, 1);
        AssignDesignedMapLevels(chapter, levels, width, positions, LevelType.Rest, 2);

        while (positions.Count > 0)
        {
            Vector2Int position = TakeRandomMapPosition(positions);
            List<LevelData> candidateLevels = GetMapGridCandidateLevels(chapter, position.y * width + position.x + 1, bossLevel);
            SetMapGridLevel(levels, width, position.x, position.y, ChooseRandomMapLevel(chapter, candidateLevels));
        }
    }

    private void AssignDesignedMapLevels(ChapterData chapter, List<LevelData> levels, int width, List<Vector2Int> positions, LevelType levelType, int count)
    {
        for (int i = 0; i < count && positions.Count > 0; i++)
        {
            Vector2Int position = TakeRandomMapPosition(positions);
            SetMapGridLevel(levels, width, position.x, position.y, ChooseRandomMapLevel(chapter, GetDesignedMapLevels(levelType)));
        }
    }

    private List<LevelData> GetDesignedMapLevels(LevelType levelType)
    {
        if (levelType == LevelType.Battle)
            return GetBattleLevels();
        if (levelType == LevelType.Rest)
            return GetRestLevels();
        return GetLevels(levelType);
    }

    private Vector2Int TakeRandomMapPosition(List<Vector2Int> positions)
    {
        int index = NextRunRandomInt(0, positions.Count);
        Vector2Int position = positions[index];
        positions.RemoveAt(index);
        return position;
    }

    private static void SetMapGridLevel(List<LevelData> levels, int width, int x, int y, LevelData level)
    {
        int index = y * width + x;
        if (index >= 0 && index < levels.Count)
            levels[index] = level;
    }

    private void ApplyDesignedChapterMapCellFlags()
    {
        RunMapGridModel grid = ChapterMapGrid;
        if (grid == null)
            return;

        for (int i = 0; i < grid.cells.Count; i++)
        {
            RunMapCellModel cell = grid.cells[i];
            if (cell == null)
                continue;

            cell.isAvailable = IsDesignedChapterMapCellAvailable(cell.x, cell.y);
            cell.isBoss = cell.x == 2 && cell.y == 7;
            cell.isRevealed = cell.isBoss;
        }
        runManager?.RevealCurrentMapNeighbors();
    }

    private static bool IsDesignedChapterMapCellAvailable(int x, int y)
    {
        return (x == 2 && y == 7) || (y >= 2 && y <= 6) || (x == 2 && (y == 0 || y == 1));
    }

    private List<LevelData> GetMapGridCandidateLevels(ChapterData chapter, int progress, LevelData bossLevel)
    {
        List<LevelData> candidateLevels = GetLevelsForProgress(chapter, progress);
        RemoveBossBattleLevel(candidateLevels, bossLevel);
        AddLevelsIfMissing(candidateLevels, GetEventLevelsForChapter(chapter));
        AddLevelsIfMissing(candidateLevels, GetLevels(LevelType.RemoveMaterial));
        AddLevelsIfMissing(candidateLevels, GetLevels(LevelType.AddMaterial));
        AddLevelsIfMissing(candidateLevels, GetRestLevels());
        AddLevelsIfMissing(candidateLevels, GetLevels(LevelType.Shop));
        AddLevelsIfMissing(candidateLevels, GetLevels(LevelType.Reward));
        if (candidateLevels.Count == 0)
            candidateLevels = GetBattleLevels();
        return candidateLevels;
    }

    private void AddLevelsIfMissing(List<LevelData> target, List<LevelData> source)
    {
        if (target == null || source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            LevelData level = source[i];
            if (level != null && !target.Contains(level))
                target.Add(level);
        }
    }

    private LevelData GetChapterBossPreviewLevel(ChapterData chapter)
    {
        if (chapter != null && chapter.BossPool != null)
        {
            for (int i = 0; i < chapter.BossPool.Length; i++)
            {
                if (GameDataDatabase.TryGetLevelData(chapter.BossPool[i], out LevelData level))
                    return level;
            }
        }
        return GetBossBattleLevel();
    }

    private void EnsureChapterMapGrid()
    {
        RunMapGridModel grid = ChapterMapGrid;
        if (grid == null || grid.CellCount == 0)
            BuildChapterMapGrid(activeChapter ?? GetActiveChapter());
    }

    public void OnChapterMapDirectionClicked(MaterialEnum material)
    {
        if (runManager == null || chapterMapMoveInProgress || currentLevel != null || runManager.State != RunFlowState.MapSelection || TutorialManager != null && TutorialManager.IsMapTutorialBlockingInput)
            return;

        StartCoroutine(ChapterMapMoveRoutine(material));
    }

    private IEnumerator ChapterMapMoveRoutine(MaterialEnum material)
    {
        chapterMapMoveInProgress = true;
        ChapterGridPanelUI panel = GetUIManager().ChapterGridPanel;
        RunMapCellModel targetCell = runManager.MoveMapPlayer(material);
        if (targetCell == null)
        {
            chapterMapMoveInProgress = false;
            yield break;
        }

        busy = true;
        panel?.SetInputLocked(true);
        panel?.RefreshTexts();
        Tween moveTween = panel?.MoveMarkerToCurrentCell(true);
        if (moveTween != null)
            yield return moveTween.WaitForCompletion();

        panel?.RefreshCellVisuals();
        float delay = panel != null ? panel.EnterLevelDelayAfterMove : 0.2f;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        pendingChapterMapBossStart = targetCell.isBoss || (ChapterMapGrid != null && ChapterMapGrid.bossMapActive);
        LevelData level = pendingChapterMapBossStart ? runManager.DrawBossLevel(activeChapter ?? GetActiveChapter()) : targetCell.level;
        if (level == null)
        {
            if (panel != null)
                panel.SetInputLocked(false);
            busy = false;
            chapterMapMoveInProgress = false;
            SaveRunProgress();
            yield break;
        }
        runManager.ConsumeCurrentMapCellLevel();
        StartLevel(level);
    }

	private void ApplyHiddenRolls(ChapterData chapter, RunMapNodeModel node)
	{
		if (node == null)
			return;

		float hiddenWeight = Mathf.Clamp01(chapter != null ? chapter.hiddenLevelWeight : 0f);
		if (hiddenWeight <= 0f)
			return;

		node.leftHidden = RollHiddenLevel(hiddenWeight);
		node.rightHidden = node.fixedSingleChoice || node.rightLevel == node.leftLevel ? node.leftHidden : RollHiddenLevel(hiddenWeight);
	}

	private bool RollHiddenLevel(float hiddenWeight)
	{
		return NextRunRandomInt(0, 10000) < Mathf.RoundToInt(hiddenWeight * 10000f);
	}

	private LevelData ChooseRandomMapLevel(ChapterData chapter, List<LevelData> levels)
	{
		if (levels == null || levels.Count == 0)
			return null;

		ChapterData weightChapter = chapter ?? activeChapter ?? GetActiveChapter();
        List<LevelType> types = new List<LevelType>();
        List<int> weights = new List<int>();
        int totalWeight = 0;
        for (int i = 0; i < levels.Count; i++)
        {
            LevelData level = levels[i];
            if (level == null || types.Contains(level.levelType))
                continue;

            int weight = GetMapLevelWeight(level, weightChapter);
            if (weight <= 0)
                continue;

            types.Add(level.levelType);
            weights.Add(weight);
            totalWeight += weight;
        }

        if (types.Count == 0 || totalWeight <= 0)
            return levels[NextRunRandomInt(0, levels.Count)];

        LevelType selectedType = types[types.Count - 1];
        int roll = NextRunRandomInt(0, totalWeight);
        for (int i = 0; i < types.Count; i++)
        {
            if (roll < weights[i])
            {
                selectedType = types[i];
                break;
            }
            roll -= weights[i];
        }

        List<LevelData> sameTypeLevels = new List<LevelData>();
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] != null && levels[i].levelType == selectedType)
                sameTypeLevels.Add(levels[i]);
        }
        return sameTypeLevels.Count > 0 ? sameTypeLevels[NextRunRandomInt(0, sameTypeLevels.Count)] : levels[NextRunRandomInt(0, levels.Count)];
	}

	private LevelData DrawMapBattleLevel(ChapterData chapter, LevelData fallbackLevel)
	{
		if (runManager == null || !HasChapterBattlePools(chapter))
			return fallbackLevel;

		return runManager.DrawNextBattleLevel(chapter) ?? fallbackLevel;
	}

	private LevelData DrawMapEventLevel(ChapterData chapter, LevelData fallbackLevel)
	{
		if (runManager == null || !HasChapterEventPool(chapter))
			return fallbackLevel;

		return runManager.DrawEventLevel(chapter) ?? fallbackLevel;
	}

	private bool HasChapterEventPool(ChapterData chapter)
	{
		return chapter != null && chapter.EventPool != null && chapter.EventPool.Length > 0;
	}

	private bool HasChapterBattlePools(ChapterData chapter)
	{
		return chapter != null &&
			((chapter.BeginPool != null && chapter.BeginPool.Length > 0) ||
			(chapter.MidPool != null && chapter.MidPool.Length > 0) ||
			(chapter.NormalPool != null && chapter.NormalPool.Length > 0));
	}

	private int GetMapLevelWeight(LevelData level, ChapterData chapter)
	{
		if (level == null)
			return 0;

		int weight = chapter != null ? chapter.defaultMapLevelWeight : 1;
        switch (level.levelType)
        {
            case LevelType.Battle:
                weight = chapter != null ? chapter.battleMapLevelWeight : 10;
                break;
            case LevelType.Elite:
                weight = chapter != null ? chapter.eliteMapLevelWeight : 10;
                break;
            case LevelType.Event:
                weight = chapter != null ? chapter.eventMapLevelWeight : 4;
                break;
            case LevelType.RemoveMaterial:
                weight = chapter != null ? chapter.removeMapLevelWeight : 2;
                break;
            case LevelType.AddMaterial:
                weight = chapter != null ? chapter.addMapLevelWeight : 2;
                break;
            case LevelType.Rest:
                weight = chapter != null ? chapter.restMapLevelWeight : 2;
                break;
            case LevelType.Shop:
                weight = chapter != null ? chapter.shopMapLevelWeight : 2;
                break;
            case LevelType.Reward:
                weight = chapter != null ? chapter.rewardMapLevelWeight : 2;
                break;
        }
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

			if (fixedLevel.levelId > 0 && GameDataDatabase.TryGetLevelData(fixedLevel.levelId, out LevelData configuredLevel))
				return configuredLevel;

			List<LevelData> levels = IsFixedBattleLevelType(fixedLevel.levelType) ? GetBattleLevels() : GetLevels(fixedLevel.levelType);
            return levels.Count > 0 ? levels[NextRunRandomInt(0, levels.Count)] : null;
		}

		return null;
	}

	private static bool IsFixedBattleLevelType(LevelType levelType)
	{
		return levelType == LevelType.Battle || levelType == LevelType.Elite;
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
			if (GameDataDatabase.TryGetLevelData(poolIds[i], out LevelData level) && (level.levelType == LevelType.Battle || level.levelType == LevelType.Elite || level.levelType == LevelType.Event || level.levelType == LevelType.RemoveMaterial || level.levelType == LevelType.AddMaterial || level.levelType == LevelType.Shop || level.levelType == LevelType.Rest || level.levelType == LevelType.Reward))
				levels.Add(level);
		}
        if (chapter == null || progress < chapter.levelLength)
        {
            AddLevelsIfMissing(levels, GetLevels(LevelType.RemoveMaterial));
            AddLevelsIfMissing(levels, GetLevels(LevelType.AddMaterial));
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
        RunMapGridModel grid = ChapterMapGrid;
        if (grid != null && grid.CellCount > 0)
        {
            int maxSteps = Mathf.Max(1, GetActiveChapterLength());
            GetUIManager().ChapterProgress?.SetProgress(Mathf.Min(currentMapNodeIndex + 1, maxSteps), maxSteps);
            return;
        }
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
        GetUIManager().HideChapterGridPanel();
        ClearMapDirectionCardsFromHandArea();
        ClearOrphanedCardViews();
	}

    private void ClearMapDirectionCardsFromHandArea()
    {
        if (handArea == null)
            return;

        for (int i = handArea.childCount - 1; i >= 0; i--)
        {
            Transform child = handArea.GetChild(i);
            if (IsMapDirectionCardObject(child))
                Destroy(child.gameObject);
        }
    }

    private static bool IsMapDirectionCardObject(Transform child)
    {
        if (child == null)
            return false;

        if (child.name.StartsWith("MapDirection_"))
            return true;

        HandCardView cardView = child.GetComponent<HandCardView>();
        MaterialModel card = cardView != null ? cardView.Card : null;
        return card != null && !string.IsNullOrEmpty(card.instanceId) && card.instanceId.StartsWith("map_direction_");
    }

    private void ClearOrphanedCardViews()
    {
        for (int i = cardViews.Count - 1; i >= 0; i--)
        {
            HandCardView view = cardViews[i];
            if (view == null)
            {
                cardViews.RemoveAt(i);
                continue;
            }

            MaterialModel card = view.Card;
            if (card != null && (playerState.Hand.Contains(card) || playerState.PlayZone.Contains(card)))
                continue;

            cardViews.RemoveAt(i);
            Destroy(view.gameObject);
        }
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

        bool startingTutorialRun = !continueSavedRun && RunSaveSystem.ConsumeStartingTutorialRun();
        if (startingTutorialRun && GameDataDatabase.TryGetChapterData(TutorialManagerUI.TutorialChapterNumericId, out ChapterData tutorialChapter))
            activeChapter = tutorialChapter;

        PlayerStatus playerStatus = saveData != null ? RunSaveSystem.CreatePlayerStatus(saveData) : PlayerStatus.CreateDefaultStatus();
        if (startingTutorialRun && playerStatus.Gold < 10)
            playerStatus.AddGold(10 - playerStatus.Gold);
		playerState = playerStatus;
        runManager = RunManager.Create(playerStatus);
        runManager.AttachMapNodes(mapNodes);
        runManager.AttachMapGrid(new RunMapGridModel());
        playerState.BuffAdded += OnPlayerBuffAdded;
        playerState.DiscardPileShuffledIntoDrawPile += OnDiscardPileShuffledIntoDrawPile;
		battleManager = BattleManager.Create(playerState);
        runManager.SetBattle(battleManager);
        battleManager.EnemyAdded += OnBattleEnemyAdded;
		((UnityEvent)refreshButton.onClick).AddListener(new UnityAction(RefreshSelectedCards));
		((UnityEvent)endTurnButton.onClick).AddListener(new UnityAction(EndTurn));
			CacheEndTurnButtonText();
        EnsureActionButtonMotion();
				GetUIManager();
            CacheDebugMagicDropdown();
	            ApplyLetterboxCameraSettings();
			EnsurePileButtons();

			CreateTopBar();
        if (saveData != null)
        {
            activeChapter = null;
            if (saveData.chapterNumericId > 0)
                GameDataDatabase.TryGetChapterData(saveData.chapterNumericId, out activeChapter);
            runManager.SetActiveChapter(activeChapter);
            runManager.RestorePoolState(saveData.runPools);
            RunSaveSystem.RestoreMapNodes(saveData, mapNodes);
            if (!runManager.RestoreMapGrid(RunSaveSystem.RestoreMapGrid(saveData)))
                BuildChapterMapGrid(activeChapter ?? GetActiveChapter());
            currentMapNodeIndex = Mathf.Clamp(saveData.currentMapNodeIndex, 0, Mathf.Max(0, mapNodes.Count - 1));
            runManager.SetCurrentMapNodeIndex(currentMapNodeIndex);
            RefreshChapterProgressUI();
            GetUIManager().MapPanel?.SetPlayerMarkerNodeIndex(currentMapNodeIndex);
        }
        else
        {
		    BuildDebugMap();
            runManager.SetActiveChapter(activeChapter ?? GetActiveChapter());
        }
		CreateMagicViews();
		CreateParticleCaster();
		CreatePlayerCastAnimator();
		InitializePlayerUiComponents();
		RebuildCards(animateFromCurrent: true);
        bool autoStartSavedNode = RunSaveSystem.ShouldAutoStartSavedNode(saveData);
        if (!autoStartSavedNode)
            SaveRunProgress();
        if (autoStartSavedNode)
        {
            LevelData savedLevel = RunSaveSystem.GetSavedCurrentLevel(saveData);
            if (savedLevel != null)
                StartSavedLevel(savedLevel, saveData);
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
        HidePinnedBuffTooltipOnOutsideClick();

        bool tutorialClickConsumedThisFrame = TutorialManager != null && TutorialManager.WasTutorialClickConsumedThisFrame();


			if (Input.GetKeyDown(KeyCode.Escape))
			{
				ToggleSettingsPanel();
			}


#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.U))
        {
            DebugApplyRandomMaterialModifiersToDeck();
        }
#endif

        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.R))
        {
            if (TryUndoRewardMagicClaim())
                return;
            if (GetUIManager().ShopPanel != null && GetUIManager().ShopPanel.TryUndoLastPurchase())
                return;
        }

			if (!tutorialClickConsumedThisFrame && IsPlaySelectedCardsInputDown())
			{
				PlaySelectedCardsByInput();
			}

			if (!tutorialClickConsumedThisFrame && currentLevel != null && currentLevel.levelType == LevelType.Rest && eventPanel != null && eventPanel.WaitingForFinalClick && Input.GetMouseButtonDown(0))

		{
			FinishRestLevel();
		}
			else if (!tutorialClickConsumedThisFrame && currentEvent != null && eventPanel != null && eventPanel.WaitingForFinalClick && Input.GetMouseButtonDown(0))

		{
			if (currentEvent.TryAdvanceToNextNode())
			{
				StartCoroutine(CompleteEventChoiceNodeRoutine());
			}
			else
			{
				FinishEventLevel();
			}
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
        if (playerState != null)
        {
            playerState.BuffAdded -= OnPlayerBuffAdded;
            playerState.DiscardPileShuffledIntoDrawPile -= OnDiscardPileShuffledIntoDrawPile;
        }
        if (battleManager != null)
        {
            battleManager.EnemyAdded -= OnBattleEnemyAdded;
            BattleManager.ClearInstance(battleManager);
        }
        if (runManager != null)
            RunManager.ClearCurrent(runManager);
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
		if ((Object)(object)discardPileArea != (Object)null)
		{
			Button component = ((Component)discardPileArea).GetComponent<Button>();
			if ((Object)(object)component != (Object)null)
			{
				((UnityEvent)component.onClick).RemoveListener(new UnityAction(ToggleDiscardPilePanel));
			}
		}
		if ((Object)(object)consumedPileArea != (Object)null)
		{
			Button component = ((Component)consumedPileArea).GetComponent<Button>();
			if ((Object)(object)component != (Object)null)
			{
				((UnityEvent)component.onClick).RemoveListener(new UnityAction(ToggleConsumedPilePanel));
			}
		}
        disabledCardPopupTween?.Kill(false);
        disabledCardPopupTween = null;
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
                MovePlayCardToHandByClick(cardView);
                return;
            }

        if (TryPlayHandCardByDoubleClick(cardView))
            return;

		bool flag = !selectedCards.Contains(cardView.Card);
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
	        UpdateLayout(false);
	        RefreshEndTurnButtonText();
	        RefreshPlayerAnimationState();
	        TutorialManager?.OnBattleCardsSelected(selectedCards);

		}

			public void OnCardPlayRequested(HandCardView cardView)
			{
				if (busy || cardView.InPlayZone)
		            return;

		        if (playerState.IsMaterialDisabled(cardView.Card))
		        {
		            ShowDisabledCardPopup();
		            return;
		        }

		        PlayCard(cardView.Card);
			}

    private bool TryPlayHandCardByDoubleClick(HandCardView cardView)
    {
        if (cardView == null || cardView.InPlayZone || cardView.Card == null)
            return false;

        float interval = GetBattleInputConfig().HandCardDoubleClickPlayInterval;
        if (interval <= 0f)
            return false;

        float now = Time.unscaledTime;
        bool doubleClicked = lastClickedHandCard == cardView.Card && now - lastHandCardClickTime <= interval;
        lastClickedHandCard = cardView.Card;
        lastHandCardClickTime = now;
        if (!doubleClicked)
            return false;

        lastClickedHandCard = null;
        lastHandCardClickTime = -999f;
        OnCardPlayRequested(cardView);
        return true;
    }

    private BattleInputConfig GetBattleInputConfig()
    {
        if (battleInputConfig == null)
            battleInputConfig = Resources.Load<BattleInputConfig>(BattleInputConfigResourcePath);
        if (battleInputConfig == null)
            battleInputConfig = ScriptableObject.CreateInstance<BattleInputConfig>();
        return battleInputConfig;
    }

    public void OnCardDragBegin(HandCardView cardView, PointerEventData eventData)
    {
        if (!CanDragCard(cardView))
            return;

        cardDragActive = true;
        draggedCardView = cardView;
        draggedCard = cardView.Card;
        dragSourceIsPlayZone = cardView.InPlayZone;
        List<MaterialModel> sourceCards = dragSourceIsPlayZone ? playerState.PlayZone : playerState.Hand;
        dragSourceIndex = sourceCards != null ? sourceCards.IndexOf(draggedCard) : -1;
        dragStartScreenPosition = eventData != null ? eventData.pressPosition : Vector2.zero;
        if (eventData != null && dragStartScreenPosition == Vector2.zero)
            dragStartScreenPosition = eventData.position;
        dragPreviewActive = false;
        dragPreviewTargetIsPlayZone = false;
        dragPreviewIndex = -1;

        ClearCardHover(cardView, false);
        GetUIManager()?.HideUnifiedDetailPopup(cardView);
        ShortcutExtensions.DOKill((Transform)cardView.RectTransform, false);
        cardView.RectTransform.SetAsLastSibling();
        UpdateLayout(false);
    }

    public void OnCardDragged(HandCardView cardView, PointerEventData eventData)
    {
        if (!CanDragCard(cardView) || eventData == null || draggedCardView != cardView)
            return;

        MoveDraggedCardToPointer(cardView, eventData);
        UpdateDragPreview(eventData);
    }

    public void OnCardDragEnd(HandCardView cardView, PointerEventData eventData)
    {
        bool wasDraggingThisCard = draggedCardView == cardView && draggedCard != null;
        MaterialModel card = wasDraggingThisCard ? draggedCard : (cardView != null ? cardView.Card : null);
        bool sourceIsPlayZone = wasDraggingThisCard ? dragSourceIsPlayZone : (cardView != null && cardView.InPlayZone);
        bool canComplete = wasDraggingThisCard && eventData != null && CanDragCard(cardView);
        bool targetIsPlayZone = sourceIsPlayZone;
        int targetIndex = Mathf.Max(0, dragSourceIndex);
        bool hasTarget = false;

        if (canComplete)
            hasTarget = TryGetDragDropTarget(eventData, out targetIsPlayZone, out targetIndex);

        ResetCardDragState();

        if (!canComplete || card == null || !hasTarget)
        {
            RebuildCards(animateFromCurrent: true);
            return;
        }

        bool changed = ApplyCardDragDrop(card, sourceIsPlayZone, targetIsPlayZone, targetIndex);
        if (changed)
        {
            RefreshEndTurnButtonText();
            RefreshPlayerAnimationState();
        }
        RebuildCards(animateFromCurrent: true);
    }

    private void MoveDraggedCardToPointer(HandCardView cardView, PointerEventData eventData)
    {
        RectTransform parent = cardView.RectTransform.parent as RectTransform;
        if (parent == null)
            return;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector3 worldPosition))
            cardView.RectTransform.position = worldPosition;
    }

    private void UpdateDragPreview(PointerEventData eventData)
    {
        if (TryGetDragDropTarget(eventData, out bool targetIsPlayZone, out int targetIndex))
            SetDragPreview(true, targetIsPlayZone, targetIndex);
        else
            SetDragPreview(false, false, -1);
    }

    private bool TryGetDragDropTarget(PointerEventData eventData, out bool targetIsPlayZone, out int targetIndex)
    {
        if (TryGetDragZoneSwitchTarget(eventData, out targetIsPlayZone, out targetIndex))
            return true;

        if (TryGetDragHoverDropTarget(eventData, out targetIsPlayZone, out targetIndex))
            return true;

        if (TryGetDragSwipeTarget(eventData, out targetIsPlayZone))
        {
            targetIndex = GetDropTailIndex(targetIsPlayZone);
            return true;
        }

        targetIsPlayZone = false;
        targetIndex = -1;
        return false;
    }

    private bool TryGetDragZoneSwitchTarget(PointerEventData eventData, out bool targetIsPlayZone, out int targetIndex)
    {
        targetIsPlayZone = false;
        targetIndex = -1;
        if (eventData == null || playerState == null)
            return false;

        float threshold = GetBattleInputConfig().CardQueueZoneSwitchScreenDistance;
        if (threshold <= 0f)
            return false;

        float deltaY = eventData.position.y - dragStartScreenPosition.y;
        if (!dragSourceIsPlayZone && deltaY >= threshold)
            targetIsPlayZone = true;
        else if (dragSourceIsPlayZone && deltaY <= -threshold)
            targetIsPlayZone = false;
        else
            return false;

        Camera eventCamera = GetQueueDropEventCamera(eventData);
        targetIndex = GetQueueScreenDropIndex(targetIsPlayZone ? playerState.PlayZone : playerState.Hand, eventData.position, eventCamera);
        return true;
    }

    private bool TryGetDragHoverDropTarget(PointerEventData eventData, out bool targetIsPlayZone, out int targetIndex)
    {
        targetIsPlayZone = false;
        targetIndex = -1;
        if (eventData == null || playerState == null)
            return false;

        Camera eventCamera = GetQueueDropEventCamera(eventData);
        bool playHit = TryGetQueueDropIndex(playerState.PlayZone, playArea, eventData.position, eventCamera, out int playIndex, out float playDistance);
        bool handHit = TryGetQueueDropIndex(playerState.Hand, handArea, eventData.position, eventCamera, out int handIndex, out float handDistance);
        if (playHit && (!handHit || playDistance <= handDistance))
        {
            targetIsPlayZone = true;
            targetIndex = playIndex;
            return true;
        }

        if (handHit)
        {
            targetIsPlayZone = false;
            targetIndex = handIndex;
            return true;
        }

        return false;
    }

    private bool TryGetDragSwipeTarget(PointerEventData eventData, out bool targetIsPlayZone)
    {
        targetIsPlayZone = false;
        if (eventData == null)
            return false;

        Vector2 delta = eventData.position - dragStartScreenPosition;
        float verticalDistance = Mathf.Abs(delta.y);
        BattleInputConfig config = GetBattleInputConfig();
        if (verticalDistance < config.DragSwipeMinDistance)
            return false;

        if (verticalDistance < Mathf.Abs(delta.x) * config.DragSwipeVerticalRatio)
            return false;

        targetIsPlayZone = delta.y > 0f;
        return true;
    }

    private Camera GetQueueDropEventCamera(PointerEventData eventData)
    {
        if (eventData != null && eventData.pressEventCamera != null)
            return eventData.pressEventCamera;
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.worldCamera : null;
    }

    private bool TryGetQueueDropIndex(List<MaterialModel> cards, RectTransform area, Vector2 screenPosition, Camera eventCamera, out int targetIndex, out float verticalDistance)
    {
        targetIndex = 0;
        verticalDistance = float.MaxValue;
        if (!TryGetQueueScreenBounds(cards, area, eventCamera, out Rect bounds))
            return false;

        Vector2 padding = GetBattleInputConfig().CardQueueDropScreenPadding;
        Rect paddedBounds = Rect.MinMaxRect(bounds.xMin - padding.x, bounds.yMin - padding.y, bounds.xMax + padding.x, bounds.yMax + padding.y);
        if (!paddedBounds.Contains(screenPosition))
            return false;

        targetIndex = GetQueueScreenDropIndex(cards, screenPosition, eventCamera);
        verticalDistance = Mathf.Abs(screenPosition.y - bounds.center.y);
        return true;
    }

    private bool TryGetQueueScreenBounds(List<MaterialModel> cards, RectTransform fallbackArea, Camera eventCamera, out Rect bounds)
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        bool hasBounds = false;

        for (int i = 0; cards != null && i < cards.Count; i++)
        {
            MaterialModel card = cards[i];
            if (IsCardExcludedFromLayout(card))
                continue;

            HandCardView view = FindView(card);
            if ((Object)view != (Object)null)
                EncapsulateScreenBounds(view.RectTransform, eventCamera, ref min, ref max, ref hasBounds);
        }

        if (!hasBounds && fallbackArea != null)
            EncapsulateScreenBounds(fallbackArea, eventCamera, ref min, ref max, ref hasBounds);

        bounds = hasBounds ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
        return hasBounds;
    }

    private void EncapsulateScreenBounds(RectTransform rectTransform, Camera eventCamera, ref Vector2 min, ref Vector2 max, ref bool hasBounds)
    {
        if ((Object)rectTransform == (Object)null)
            return;

        rectTransform.GetWorldCorners(cardQueueWorldCorners);
        for (int i = 0; i < cardQueueWorldCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, cardQueueWorldCorners[i]);
            min = hasBounds ? Vector2.Min(min, screenPoint) : screenPoint;
            max = hasBounds ? Vector2.Max(max, screenPoint) : screenPoint;
            hasBounds = true;
        }
    }

    private int GetQueueScreenDropIndex(List<MaterialModel> cards, Vector2 screenPosition, Camera eventCamera)
    {
        int index = 0;
        for (int i = 0; cards != null && i < cards.Count; i++)
        {
            MaterialModel card = cards[i];
            if (IsCardExcludedFromLayout(card))
                continue;

            HandCardView view = FindView(card);
            if ((Object)view != (Object)null && screenPosition.x > GetRectScreenCenter(view.RectTransform, eventCamera).x)
                index++;
        }
        return index;
    }

    private Vector2 GetRectScreenCenter(RectTransform rectTransform, Camera eventCamera)
    {
        if ((Object)rectTransform == (Object)null)
            return Vector2.zero;

        rectTransform.GetWorldCorners(cardQueueWorldCorners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, cardQueueWorldCorners[0]);
        Vector2 max = min;
        for (int i = 1; i < cardQueueWorldCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, cardQueueWorldCorners[i]);
            min = Vector2.Min(min, screenPoint);
            max = Vector2.Max(max, screenPoint);
        }
        return (min + max) * 0.5f;
    }

    private void SetDragPreview(bool active, bool targetIsPlayZone, int targetIndex)
    {
        if (active)
            targetIndex = Mathf.Clamp(targetIndex, 0, GetDropTailIndex(targetIsPlayZone));

        if (dragPreviewActive == active && dragPreviewTargetIsPlayZone == targetIsPlayZone && dragPreviewIndex == targetIndex)
            return;

        dragPreviewActive = active;
        dragPreviewTargetIsPlayZone = targetIsPlayZone;
        dragPreviewIndex = targetIndex;
        UpdateLayout(false);
    }

    private int GetDropTailIndex(bool targetIsPlayZone)
    {
        if (playerState == null)
            return 0;

        return CountLayoutCards(targetIsPlayZone ? playerState.PlayZone : playerState.Hand);
    }

    private bool ApplyCardDragDrop(MaterialModel card, bool sourceIsPlayZone, bool targetIsPlayZone, int targetIndex)
    {
        if (targetIsPlayZone)
        {
            if (sourceIsPlayZone)
                return playerState.ReorderPlayCard(card, targetIndex);

            bool changed = TryDragHandCardToPlay(card, targetIndex);
            if (changed)
            {
                selectedCards.Remove(card);
                ClearPlayedCardFeedback(card);
            }
            return changed;
        }

        if (sourceIsPlayZone)
            return TryDragPlayCardToHand(card, targetIndex);

        return playerState.ReorderHandCard(card, targetIndex);
    }

    private void ResetCardDragState()
    {
        cardDragActive = false;
        draggedCardView = null;
        draggedCard = null;
        dragSourceIsPlayZone = false;
        dragSourceIndex = -1;
        dragStartScreenPosition = Vector2.zero;
        dragPreviewActive = false;
        dragPreviewTargetIsPlayZone = false;
        dragPreviewIndex = -1;
    }

public bool IsCardDragActive => cardDragActive;

	    public void SetCardHover(HandCardView cardView, bool instant)
    {
        if (cardDragActive || cardView == null || playerState == null || (!playerState.Hand.Contains(cardView.Card) && !playerState.PlayZone.Contains(cardView.Card)))
            return;

        if (layoutHoverCardView == cardView)
            return;

        if (layoutHoverCardView != null)
            layoutHoverCardView.SetLayoutHover(false, cardHoverScale, instant);

        layoutHoverCardView = cardView;
        layoutHoverCardView.SetLayoutHover(true, cardHoverScale, instant);
        UpdateLayout(instant);
    }

    public void ClearCardHover(HandCardView cardView, bool instant)
    {
        if (layoutHoverCardView != cardView)
            return;

        layoutHoverCardView.SetLayoutHover(false, cardHoverScale, instant);
        layoutHoverCardView = null;
        UpdateLayout(instant);
    }

    private void MovePlayCardToHandByClick(HandCardView cardView)
    {
        if (!CanDragCard(cardView) || !cardView.InPlayZone)
            return;

        int targetIndex = playerState.Hand.Count;
        if (!TryDragPlayCardToHand(cardView.Card, targetIndex))
            return;

        selectedCards.Remove(cardView.Card);
        cardView.SetSelected(false, instant: false);
        RefreshEndTurnButtonText();
        RefreshPlayerAnimationState();
        RebuildCards(animateFromCurrent: true);
    }

		    private bool CanDragCard(HandCardView cardView)

	    {
	        return !busy && !choosingEventCard && cardView != null && playerState != null && (playerState.Hand.Contains(cardView.Card) || playerState.PlayZone.Contains(cardView.Card));
	    }


	    private bool TryDragHandCardToPlay(MaterialModel card, int targetIndex)
	    {
	        if (card == null || playerState.IsMaterialDisabled(card))
	        {
	            ShowDisabledCardPopup();
	            return false;
	        }

	        if (TutorialManager != null && !TutorialManager.CanMoveCardToPlay(card, playerState.PlayZone))
	            return false;

	        MaterialModifierContext previousContext = MaterialModifierModel.CurrentContext;
	        MaterialModifierContext modifierContext = new MaterialModifierContext { PlayerState = playerState, BattleManager = battleManager };
	        MaterialModifierModel.CurrentContext = modifierContext;
	        bool moved;
	        try
	        {
	            moved = playerState.TryMoveHandCardToPlay(card, targetIndex);
	            if (moved)
	                TriggerArcOnCardPlayed();
	        }
	        finally
	        {
	            MaterialModifierModel.CurrentContext = previousContext;
	        }

        if (!moved)
            return false;

        ClearPlayedCardFeedback(card);
        TutorialManager?.OnBattleCardsPlayed(playerState.PlayZone);

	        TutorialManager?.OnBattleReadyToEndTurn(playerState.PlayZone);
	        if (modifierContext.EnemyBuffChanged)
	            RefreshEnemyUI();
	        return true;
	    }

	    private bool TryDragPlayCardToHand(MaterialModel card, int targetIndex)
	    {
	        if (TutorialManager != null && !TutorialManager.CanMovePlayCardToHand(card))
	            return false;

	        bool moved = playerState.TryMovePlayCardToHand(card, targetIndex);
	        if (moved)
	            TutorialManager?.OnBattleCardCanceled(playerState.PlayZone);
	        return moved;
	    }


    private void ShowDisabledCardPopup()
    {
        CacheDisabledCardPopupReferences();
        if ((Object)disabledCardPopupRoot == (Object)null || disabledCardPopupCanvasGroup == null || disabledCardPopupText == null)
            return;

        disabledCardPopupText.text = LocalizationSystem.GetText("ui.battle.card_disabled", "这张牌本回合无法打出！");
        disabledCardPopupTween?.Kill(false);
        ((Component)disabledCardPopupRoot).gameObject.SetActive(true);
        disabledCardPopupRoot.SetAsLastSibling();
        PopupLayerUtility.ApplyTo(disabledCardPopupRoot);
        disabledCardPopupCanvasGroup.alpha = 0f;
        ((Transform)disabledCardPopupRoot).localScale = new Vector3(0.72f, 0.72f, 1f);

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Append(disabledCardPopupCanvasGroup.DOFade(1f, 0.12f));
        sequence.Join(disabledCardPopupRoot.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack));
        sequence.AppendInterval(0.72f);
        sequence.Append(disabledCardPopupCanvasGroup.DOFade(0f, 0.14f));
        sequence.Join(disabledCardPopupRoot.DOScale(new Vector3(0.82f, 0.82f, 1f), 0.16f).SetEase(Ease.InBack));
        disabledCardPopupTween = sequence.OnComplete(() => ((Component)disabledCardPopupRoot).gameObject.SetActive(false));
    }

    private void CacheDisabledCardPopupReferences()
    {
        if ((Object)disabledCardPopupRoot == (Object)null)
            disabledCardPopupRoot = UIManager.FindChildRect(((Component)this).transform, "DisabledCardPopup");
        if ((Object)disabledCardPopupRoot == (Object)null)
            return;

        disabledCardPopupCanvasGroup = ((Component)disabledCardPopupRoot).GetComponent<CanvasGroup>();
        disabledCardPopupText = disabledCardPopupText != null ? disabledCardPopupText : UIManager.FindChildComponent<TMP_Text>(((Component)disabledCardPopupRoot).transform, "Text");
        if (disabledCardPopupCanvasGroup != null)
            disabledCardPopupCanvasGroup.alpha = 0f;
        ((Component)disabledCardPopupRoot).gameObject.SetActive(false);
    }

	private void HandleEventCardChoice(HandCardView cardView)
	{
		if (cardView == null || cardView.InPlayZone || !playerState.Hand.Contains(cardView.Card))
			return;

		if (pendingChoiceCards.Contains(cardView.Card))
		{
				pendingChoiceCards.Remove(cardView.Card);
				cardView.SetSelected(false, instant: false);
				UpdateLayout(false);
				return;

		}

			pendingChoiceCards.Add(cardView.Card);
			cardView.SetSelected(true, instant: false);
			UpdateLayout(false);
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
					playerState.ConsumeCardForBattle(choiceCards[i]);
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

			ClearSelectedCards(false);
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
		AnimateReturningViews(views, removedTemporaryCards, GetDiscardPileArea(), (TweenCallback)delegate
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

    private bool IsPlaySelectedCardsInputDown()
    {
        if (Input.GetMouseButtonDown(1))
            return !IsPointerOverClickHandler(Input.mousePosition);

        if (!TryGetPlayInputScreenPosition(out Vector2 screenPosition))
            return false;

        return IsAbovePlayInputThreshold(screenPosition) && !IsPointerOverClickHandler(screenPosition);
    }

    private bool TryGetPlayInputScreenPosition(out Vector2 screenPosition)
    {
        screenPosition = default;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began)
                return false;

            screenPosition = touch.position;
            return true;
        }

        if (!Input.GetMouseButtonDown(0))
            return false;

        screenPosition = Input.mousePosition;
        return true;
    }

    private bool IsAbovePlayInputThreshold(Vector2 screenPosition)
    {
        return screenPosition.y > Screen.height * Mathf.Clamp01(playInputScreenHeightThreshold);
    }

    public bool TryPlaySelectedCardsFromCardClick(HandCardView cardView, PointerEventData eventData)
    {
        if (cardView == null || eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return false;

        if (!IsAbovePlayInputThreshold(eventData.position))
            return false;

        if (!selectedCards.Contains(cardView.Card))
        {
            if (!cardView.Selected || playerState == null || !playerState.Hand.Contains(cardView.Card))
                return false;

            selectedCards.Add(cardView.Card);
        }

        PlaySelectedCardsByInput();
        return true;
    }

    private bool IsPointerOverClickHandler(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        if (playInputPointerEventData == null || playInputEventSystem != eventSystem)
        {
            playInputEventSystem = eventSystem;
            playInputPointerEventData = new PointerEventData(eventSystem);
        }

        playInputPointerEventData.Reset();
        playInputPointerEventData.position = screenPosition;
        playInputPointerEventData.button = PointerEventData.InputButton.Left;
        playInputRaycastResults.Clear();
        eventSystem.RaycastAll(playInputPointerEventData, playInputRaycastResults);

        for (int i = 0; i < playInputRaycastResults.Count; i++)
        {
            GameObject hitObject = playInputRaycastResults[i].gameObject;
            if (hitObject != null && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject) != null)
                return true;
        }

        return false;
    }

			private void PlaySelectedCards()
			{
            RestoreSelectedCardsFromViewState();
				if (selectedCards.Count == 0)

			{
				return;
			}
			bool flag = false;
	        List<MaterialModel> movedCards = new List<MaterialModel>();
	        MaterialModifierContext previousContext = MaterialModifierModel.CurrentContext;
	        MaterialModifierContext modifierContext = new MaterialModifierContext { PlayerState = playerState, BattleManager = battleManager };
	        MaterialModifierModel.CurrentContext = modifierContext;
	        try
	        {
			    for (int i = 0; i < playerState.Hand.Count; i++)
			    {
				    MaterialModel materialModel = playerState.Hand[i];
				    if (selectedCards.Contains(materialModel) && !playerState.IsMaterialDisabled(materialModel) && (TutorialManager == null || TutorialManager.CanMoveCardToPlay(materialModel, playerState.PlayZone)))
				    {
					    bool moved = playerState.TryMoveHandCardToPlay(materialModel);
	                            if (moved)
	                            {
	                                TriggerArcOnCardPlayed();
	                                movedCards.Add(materialModel);
	                            }
					    flag |= moved;
					    if (moved)
						    i--;
				    }
			    }
	        }
	        finally
	        {
	            MaterialModifierModel.CurrentContext = previousContext;
	        }
        for (int i = 0; i < movedCards.Count; i++)
        {
            selectedCards.Remove(movedCards[i]);
            ClearPlayedCardFeedback(movedCards[i]);
        }

	        RefreshEndTurnButtonText();
	        RefreshPlayerAnimationState();
			if (flag)
			{
	            TutorialManager?.OnBattleCardsPlayed(playerState.PlayZone);
	            TutorialManager?.OnBattleReadyToEndTurn(playerState.PlayZone);
	            if (modifierContext.EnemyBuffChanged)
	                RefreshEnemyUI();
				RebuildCards(animateFromCurrent: true);
			}
		}


	private void PlayCard(MaterialModel card)
	{
		if (playerState.IsMaterialDisabled(card))
			return;

		if (TutorialManager != null && !TutorialManager.CanMoveCardToPlay(card, playerState.PlayZone))
			return;

        MaterialModifierContext previousContext = MaterialModifierModel.CurrentContext;
        MaterialModifierContext modifierContext = new MaterialModifierContext { PlayerState = playerState, BattleManager = battleManager };
        MaterialModifierModel.CurrentContext = modifierContext;
        bool moved;
        try
        {
            moved = playerState.TryMoveHandCardToPlay(card);
            if (moved)
                TriggerArcOnCardPlayed();
        }
        finally
        {
            MaterialModifierModel.CurrentContext = previousContext;
        }
		if (moved)
		{
			selectedCards.Remove(card);
            ClearPlayedCardFeedback(card);
            TutorialManager?.OnBattleCardsPlayed(playerState.PlayZone);
            TutorialManager?.OnBattleReadyToEndTurn(playerState.PlayZone);
            RefreshEndTurnButtonText();
            RefreshPlayerAnimationState();
            if (modifierContext.EnemyBuffChanged)
                RefreshEnemyUI();
			RebuildCards(animateFromCurrent: true);
		}
	}

    private void ClearPlayedCardFeedback(MaterialModel card)
    {
        HandCardView view = FindView(card);
        if ((Object)view == (Object)null)
            return;

        ClearCardHover(view, false);
        view.ClearPlayFeedback(false);
        GetUIManager()?.HideUnifiedDetailPopup(view);
    }

    private void TriggerArcOnCardPlayed()
    {
        if (battleManager == null)
            return;

        bool changed = false;
        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        for (int i = 0; enemies != null && i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            int arcStack = enemy.GetBuffStack(BuffEnum.Arc);
            if (arcStack <= 0)
                continue;

            enemy.TakeDamage(arcStack);
            changed = true;
        }

        if (changed)
            RefreshEnemyUI();
    }

	private void RefreshSelectedCards()
	{
		RefreshSelectedCards(ignoreOncePerTurn: false);
	}

		private void RefreshSelectedCards(bool ignoreOncePerTurn)
		{
            RestoreSelectedCardsFromViewState();
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)

		//IL_0069: Expected O, but got Unknown
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Expected O, but got Unknown
			if (TutorialManager != null && TutorialManager.TutorialBattleRunning && !TutorialManager.CanRefreshSelected(selectedCards))
			{
				return;
			}
			if (busy || (refreshUsedThisTurn && !ignoreOncePerTurn && playerState.ExtraRefreshChancesThisTurn <= 0) || selectedCards.Count == 0)
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
				if (TutorialManager != null && AreSelectedCardsOnlyInHand() && TutorialManager.TryGetForcedRefreshMaterials(selectedCards.Count, forcedRefreshMaterials))
				{
					int returnedCount = playerState.ReturnHandCardsToDiscardPile(selectedCards, list2);
					int drawnCount = playerState.DrawSpecificMaterialsToHand(forcedRefreshMaterials, true);
					refreshResult = new PlayerState.RefreshHandResult(drawnCount, returnedCount);
				}
			else if (currentLevel != null && currentLevel.levelType == LevelType.Reward)
			{
				refreshResult = playerState.RefreshBasicCombatCards(selectedCards, list2);
			}
			else
			{
				refreshResult = playerState.RefreshCombatCards(selectedCards, list2, battleManager);
			}

			ClearSelectedCards(false);
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
		AnimateReturningViews(list, list2, GetDiscardPileArea(), (TweenCallback)delegate
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

			if (busy)
	            return;

            RestoreSelectedCardsFromViewState();

	        if (HasSelectedArrowCard())
        {
            PlaySelectedCards();
            return;
        }

		if (TutorialManager != null && !TutorialManager.CanEndTurn(playerState.PlayZone))
			return;

        TutorialManager?.OnBattleEndTurnStarted();

		GameLog.Data((currentEvent != null) ? "Click end turn in event" : "Click end turn in battle");
			ClearSelectedCards(false);
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

	private IEnumerator ResolveEndTurnRoutine()
	{
		busy = true;
		SetButtonsInteractable(interactable: false);
		ResetMagicHighlights();
		battleManager?.BeginPlayerResolveRules();
		List<MaterialModel> list = new List<MaterialModel>(playerState.PlayZone);
		if (list.Count > 0)
		{
			yield return PlayResolveAnimation(list);
			GetUIManager().PlayArea?.HideResolveIndicator();
			ResetContinuousCastCounterUI();
			if (AllEnemiesDead())
			{
                battleManager?.EndPlayerResolveRules();
				yield return FinishBattleRoutine();
				yield break;
			}
		}
		List<HandCardView> list2 = new List<HandCardView>();
		for (int i = 0; i < cardViews.Count; i++)
		{
			list2.Add(cardViews[i]);
		}
		List<MaterialModel> removedTemporaryCards = new List<MaterialModel>();
        battleManager?.EndPlayerTurnRules(removedTemporaryCards);
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
        if (TutorialManager != null && TutorialManager.ShouldKillTutorialEnemyAfterPlayerTurn)
        {
            battleManager?.EndPlayerResolveRules();
            for (int i = 0; i < enemyModels.Count; i++)
                enemyModels[i]?.Kill(new CombatantModel(playerState));
            RefreshEnemyUI((RectTransform)null, false);
            yield return PlayPendingEnemyDeaths();
            yield return FinishBattleRoutine();
            yield break;
        }
        battleManager?.BeginEnemyTurn();
		for (int num = 0; num < enemyModels.Count; num++)
		{
			EnemyModel enemy = enemyModels[num];
			if (enemy == null || enemy.IsDead || !enemy.CanActThisEnemyTurn)
				continue;

			if (!enemyTurnBannerShown)
			{
				enemyTurnBannerShown = true;
				GetUIManager().TurnBanner?.Show("敌方回合");
			}

			yield return ResolveEnemyIntentsRoutine(enemy);
			if (HasActingEnemyAfter(num))
				yield return new WaitForSeconds(enemyBetweenDelay);
		}
        battleManager?.EndEnemyTurn();
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
	        ArrowReadSequence playSequence = ArrowReadSystem.BuildSequence(playerState.PlayZone, playerState, battleManager);
	        EventOptionData matchedOption = null;
	        bool matched = currentEvent != null && currentEvent.TryGetMatchedOption(playSequence.Tokens, out matchedOption);

        if (!matched && currentEvent != null)
            matched = currentEvent.TryGetExitOption(out matchedOption);
        GameLog.Data(string.Format("Resolve rest end turn matched={0} option={1}", matched, (matchedOption != null) ? matchedOption.id : "none"));
        if (matched && (Object)eventPanel != (Object)null)
        {
            yield return eventPanel.PlayOptionChosen(matchedOption);
            RectTransform matchedOptionRect = eventPanel.MatchedOptionRect;
	            for (int i = 0; i < playSequence.Tokens.Count; i++)
	            {
	                ArrowReadToken token = playSequence.Tokens[i];
	                HandCardView handCardView = FindView(token.SourceCard);
	                if ((Object)handCardView != (Object)null && (Object)matchedOptionRect != (Object)null)
	                    PlayMaterialFillParticle(handCardView, matchedOptionRect, token.DisplayMaterial);
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
        AnimateReturningViews(views, removedTemporaryCards, GetDiscardPileArea(), (TweenCallback)delegate
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
        if (matchedOption != null && !string.IsNullOrEmpty(matchedOption.nextNodeId) && currentEvent != null && currentEvent.AdvanceToNextNode(matchedOption) && (Object)eventPanel != (Object)null)
        {
            yield return eventPanel.ShowCurrentNodeRoutine();
            yield break;
        }
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
        AnimateReturningViews(views, removedTemporaryCards, GetDiscardPileArea(), (TweenCallback)delegate
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
			ArrowReadSequence playSequence = ArrowReadSystem.BuildSequence(playerState.PlayZone, playerState, battleManager);
			EventOptionData matchedOption = null;
			bool matched = currentEvent != null && currentEvent.TryGetMatchedOption(playSequence.Tokens, out matchedOption);

		if (matched)
			TutorialManager?.OnEventOptionResolved();
		if (!matched && currentEvent != null)
			matched = currentEvent.TryGetExitOption(out matchedOption);
		if (!matched && currentEvent != null)
			matched = currentEvent.TryGetDefaultEndOption(out matchedOption);
		GameLog.Data(string.Format("Resolve event end turn matched={0} option={1}", matched, (matchedOption != null) ? matchedOption.id : "none"));

		RectTransform matchedOptionRect = null;
		if (matched && (Object)eventPanel != (Object)null)
		{
			yield return eventPanel.PlayOptionChosen(matchedOption);
			matchedOptionRect = eventPanel.MatchedOptionRect;
				for (int i = 0; i < playSequence.Tokens.Count; i++)
				{
					ArrowReadToken token = playSequence.Tokens[i];
					HandCardView handCardView = FindView(token.SourceCard);
					if ((Object)handCardView != (Object)null && (Object)matchedOptionRect != (Object)null)
						PlayMaterialFillParticle(handCardView, matchedOptionRect, token.DisplayMaterial);
				}

			yield return (object)new WaitForSeconds(GetParticleArrivalWait());
		}

		if (matched && !HasEventEffects(matchedOption) && IsCardChoiceEventResult(matchedOption.resultId))
		{
			StartEventCardChoice(matchedOption);
			yield break;
		}

		if (matched)
		{
			yield return ResolveEventOptionEffectsRoutine(matchedOption, matchedOptionRect, deferred: false);
			if (CheckPlayerDefeated())
				yield break;
		}

		List<HandCardView> list = new List<HandCardView>();
		for (int j = 0; j < cardViews.Count; j++)
			list.Add(cardViews[j]);

		List<MaterialModel> removedTemporaryCards = new List<MaterialModel>();
		playerState.EndTurn(removedTemporaryCards);
		RefreshStaticUI();
		bool returnDone = false;
		AnimateReturningViews(list, removedTemporaryCards, GetDiscardPileArea(), (TweenCallback)delegate
		{
			returnDone = true;
		});
		while (!returnDone)
			yield return null;

		RebuildCards(animateFromCurrent: true);
		refreshUsedThisTurn = false;
		if (!matched)
		{
			FinishEventLevel();
			yield break;
		}

		currentEvent?.MarkOptionResolved(matchedOption);
		yield return ResolveEventOptionEffectsRoutine(matchedOption, matchedOptionRect, deferred: true);
		if (CheckPlayerDefeated())
			yield break;

		CompleteEventChoiceOption(matchedOption);
	}

	private IEnumerator ResolveEventOptionEffectsRoutine(EventOptionData option, RectTransform sourceRect, bool deferred)
	{
		if (option == null)
			yield break;

		if (!HasEventEffects(option))
		{
			if (!deferred && option.resultId != 0)
			{
				currentEvent.ResolveResult(option.resultId, playerState);
				GameLog.Data($"Event option selected id={option.id} result={option.resultId}");
				RefreshStaticUI();
				SaveRunProgress();
			}
			yield break;
		}

		for (int i = 0; i < option.effects.Length; i++)
		{
			EventEffectData effect = option.effects[i];
			if (effect == null || IsDeferredEventEffect(effect.rewardType) != deferred)
				continue;

			yield return ResolveEventEffectRoutine(effect, option, sourceRect);
			if (CheckPlayerDefeated())
				yield break;
		}
	}

	private IEnumerator ResolveEventEffectRoutine(EventEffectData effect, EventOptionData option, RectTransform sourceRect)
	{
		switch (effect.rewardType)
		{
			case EventRewardType.Heal:
				ApplyEventHeal(GetEventEffectAmount(effect, 10));
				break;
			case EventRewardType.LoseHealth:
				ApplyEventLoseHealth(option, effect);
				break;
			case EventRewardType.GainGold:
				yield return GainGoldAnimated(GetEventEffectAmount(effect, 1), GetEventEffectSourceRect(sourceRect));
				break;
			case EventRewardType.GainMagic:
				yield return ShowEventMagicRewardRoutine();
				break;
			case EventRewardType.GainMagicModifier:
				yield return ShowEventMagicModifierRoutine(GetEventEffectChoiceCount(effect, option, 2));
				break;
            case EventRewardType.GainMaterialModifier:
                yield return ShowEventMaterialModifierRoutine(effect.modifierId);
                break;
			case EventRewardType.IncreaseMaxHealth:
				ApplyEventIncreaseMaxHealth(GetEventEffectAmount(effect, 5));
				break;
			case EventRewardType.GainMaterial:
				AddEventMaterial(effect.material != MaterialEnum.None ? effect.material : GetRandomBasicMaterial(), GetEventEffectCount(effect, 1), effect.modifierId);
				break;
			case EventRewardType.GainRandomMaterial:
				AddEventRandomMaterials(GetEventEffectCount(effect, 1));
				break;
			case EventRewardType.GainSameRandomMaterials:
				AddEventMaterial(GetRandomBasicMaterial(), GetEventEffectCount(effect, 1));
				break;
			case EventRewardType.IncreaseDrawCount:
				ApplyEventIncreaseDrawCount(GetEventEffectAmount(effect, 1));
				break;
            case EventRewardType.RemoveMaterial:
                yield return RemoveEventMaterialsRoutine(GetEventEffectChoiceCount(effect, option, 1));
                break;
            case EventRewardType.GainNextBattleStartShield:
                ApplyEventNextBattleStartShield(GetEventEffectAmount(effect, 1));
                break;
            case EventRewardType.SpendAllGold:
                ApplyEventSpendAllGold();
                break;
            case EventRewardType.RandomizeDeckBasicMaterials:
                ApplyEventRandomizeDeckBasicMaterials();
                break;
            case EventRewardType.GainRandomSyntaxMaterial:
                AddEventRandomSyntaxMaterial();
                break;
		}
	}

	private bool HasEventEffects(EventOptionData option)
	{
		return option != null && option.effects != null && option.effects.Length > 0;
	}

	private bool IsDeferredEventEffect(EventRewardType rewardType)
	{
		return rewardType == EventRewardType.GainMagic || rewardType == EventRewardType.GainMagicModifier || rewardType == EventRewardType.GainMaterialModifier;
	}

	private int GetEventEffectAmount(EventEffectData effect, int defaultAmount)
	{
		return effect != null && effect.amount != 0 ? effect.amount : defaultAmount;
	}

	private int GetEventEffectCount(EventEffectData effect, int defaultCount)
	{
		return effect != null && effect.count > 0 ? effect.count : defaultCount;
	}

	private int GetEventEffectChoiceCount(EventEffectData effect, EventOptionData option, int defaultCount)
	{
		if (effect != null && effect.choiceCount > 0)
			return effect.choiceCount;
		if (option != null && option.choiceCount > 0)
			return option.choiceCount;
		return defaultCount;
	}

	private RectTransform GetEventEffectSourceRect(RectTransform sourceRect)
	{
		if ((Object)sourceRect != (Object)null)
			return sourceRect;

		return transform as RectTransform;
	}

	private void ApplyEventHeal(int amount)
	{
		int healthBefore = playerState.CurrentHealth;
		playerState.Heal(amount);
		int healed = playerState.CurrentHealth - healthBefore;
		if (healed > 0)
		{
			PlayPlayerCornerFeedback(new Color(0.1f, 0.95f, 0.25f, 0.48f));
			ShowPlayerFloatingText("+" + healed, FloatingTextType.Heal);
		}
		RefreshStaticUI();
		SaveRunProgress();
	}

	private void ApplyEventLoseHealth(EventOptionData option, EventEffectData effect)
	{
		int damage = GetEventEffectAmount(effect, 1);
		if (effect != null && effect.escalatePerUse != 0 && currentEvent != null)
			damage += currentEvent.GetOptionResolveCount(option) * effect.escalatePerUse;

		int healthBefore = playerState.CurrentHealth;
		playerState.TakeDirectDamage(damage);
		int damageTaken = healthBefore - playerState.CurrentHealth;
		if (damageTaken > 0)
		{
			GetUIManager().PlayerFeedback?.PlayDamageFeedback(new Color(0.95f, 0.05f, 0.02f, 0.72f), playerState);
			ShowPlayerFloatingText("-" + damageTaken, FloatingTextType.Damage);
		}
		RefreshStaticUI();
		SaveRunProgress();
	}

		private void ApplyEventIncreaseMaxHealth(int amount)
		{
			playerState.IncreaseMaxHealthOnly(amount);
			PlayPlayerCornerFeedback(new Color(0.1f, 0.95f, 0.25f, 0.48f));
			ShowPlayerFloatingText("+" + amount + "上限", FloatingTextType.Heal);
			RefreshStaticUI();
			SaveRunProgress();
		}


	private void ApplyEventIncreaseDrawCount(int amount)
	{
		playerState.DrawCount += amount;
		GameLog.Data($"Event result player draw count +{amount} now={playerState.DrawCount}");
		RefreshStaticUI();
		SaveRunProgress();
	}

    private void ApplyEventNextBattleStartShield(int amount)
    {
        playerState.AddBuff(BuffEnum.PreparedShield, amount);
        SaveRunProgress();
    }

    private void ApplyEventSpendAllGold()
    {
        if (playerState == null || playerState.Gold <= 0)
            return;

        playerState.AddGold(-playerState.Gold);
        RefreshStaticUI();
        SaveRunProgress();
    }

    private void ApplyEventRandomizeDeckBasicMaterials()
    {
        if (playerState == null)
            return;

        bool changed = false;
        for (int i = 0; i < playerState.Deck.Count; i++)
        {
            MaterialModel card = playerState.Deck[i];
            if (card == null || card.material == MaterialEnum.None || card.material == MaterialEnum.Wild)
                continue;

            MaterialEnum material = GetRandomBasicMaterial();
            if (card.material != material)
                changed = true;
            card.material = material;
        }

        if (!changed)
            return;

        RefreshMaterialListPanel();
        RebuildCards(animateFromCurrent: true);
        RefreshStaticUI();
        SaveRunProgress();
    }

    private void AddEventRandomSyntaxMaterial()
    {
        AddEventMaterial(MaterialEnum.Wild, 1, GetRandomSyntaxModifierId());
    }

    private string GetRandomSyntaxModifierId()
    {
        switch (NextRunRandomInt(0, 3))
        {
            case 0:
                return "period_arrow";
            case 1:
                return "return_arrow";
            default:
                return "pack_arrow";
        }
    }

		private void AddEventRandomMaterials(int count)
		{
			for (int i = 0; i < count; i++)
				AddEventMaterial(GetRandomBasicMaterial(), 1);
		}

        private bool CanSelectDisabledMaterialForNonBattleAction(MaterialModel materialModel)
        {
            return materialModel != null && playerState != null && playerState.Deck.Contains(materialModel);
        }


	private void AddEventMaterial(MaterialEnum material, int count)
	{
        AddEventMaterial(material, count, null);
	}

	private void AddEventMaterial(MaterialEnum material, int count, string modifierId)
	{
		if (material == MaterialEnum.None || count <= 0)
			return;

        MaterialModifierData modifierData = GetMaterialModifierDataById(modifierId);
		for (int i = 0; i < count; i++)
        {
			MaterialModel card = playerState.AddDeckMaterial(material);
            MaterialModifierModel modifier = MaterialModifierFactory.Create(modifierData);
            if (card != null && modifier != null)
                card.AddModifier(modifier);
        }
		RefreshMaterialListPanel();
		RefreshStaticUI();
		SaveRunProgress();
	}

	private MaterialEnum GetRandomBasicMaterial()
	{
		return (MaterialEnum)NextRunRandomInt((int)MaterialEnum.Fire, (int)MaterialEnum.Earth + 1);
	}

	private IEnumerator RemoveEventMaterialsRoutine(int choiceCount)
	{
			MaterialListPanelUI panel = GetUIManager().MaterialSelectionPanel;
			if ((Object)panel == (Object)null || playerState == null)

			yield break;

		int selectableCount = 0;
		for (int i = 0; i < playerState.Deck.Count; i++)
		{
			if (playerState.Deck[i] != null)
				selectableCount++;
		}
		if (selectableCount == 0)
			yield break;

		int targetCount = Mathf.Clamp(choiceCount, 1, selectableCount);
		bool completed = false;
		List<MaterialModel> selectedMaterials = new List<MaterialModel>();
		panel.BeginSelection(targetCount, IsEventRemoveMaterialSelectable, delegate(IReadOnlyList<MaterialModel> materials)
		{
			selectedMaterials.Clear();
			for (int i = 0; materials != null && i < materials.Count; i++)
			{
				if (IsEventRemoveMaterialSelectable(materials[i]))
					selectedMaterials.Add(materials[i]);
			}
			completed = true;
		});

		while (!completed)
			yield return null;

		for (int i = 0; i < selectedMaterials.Count; i++)
			playerState.RemoveCardEverywhere(selectedMaterials[i]);
		RefreshMaterialListPanel();
		RebuildCards(animateFromCurrent: true);
		RefreshStaticUI();
		SaveRunProgress();
	}

	private bool IsEventRemoveMaterialSelectable(MaterialModel materialModel)
	{
		return materialModel != null && playerState != null && playerState.Deck.Contains(materialModel);
	}

	private IEnumerator ShowEventMagicRewardRoutine()
	{
		RewardPanelUI panel = GetUIManager().RewardPanel;
		if ((Object)panel == (Object)null)
			yield break;

		bool completed = false;
		panel.ShowMagicOnly(delegate { completed = true; });
		while (!completed)
			yield return null;

		RefreshStaticUI();
		SaveRunProgress();
	}

	private IEnumerator ShowEventMagicModifierRoutine(int choiceCount)
	{
		bool completed = false;
		ShowMagicModifierSelection(choiceCount, delegate { completed = true; });
		while (!completed)
			yield return null;

		RefreshStaticUI();
		SaveRunProgress();
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
			ClearSelectedCards(false);
			choosingEventCard = true;

			MaterialListPanelUI panel = GetUIManager().MaterialSelectionPanel;
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
            return CanSelectDisabledMaterialForNonBattleAction(materialModel);

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
		ArrowReadSequence readSequence = ArrowReadSystem.BuildSequence(cards, playerState, battleManager);
        List<ArrowReadStep> afterReadActionSteps = new List<ArrowReadStep>();
		for (int stepIndex = 0; stepIndex < readSequence.Steps.Count; stepIndex++)
		{
			ArrowReadStep step = readSequence.Steps[stepIndex];
			MaterialModel card = step.SourceCard;
			if (card != null)
			{
				ResetMagicHighlights();
				MoveIndicatorToReadStep(cards, step, stepIndex == 0);
				yield return (object)new WaitForSeconds(layoutDuration * 0.35f);
				HandCardView handCardView = FindView(card);
				if ((Object)handCardView != (Object)null)
				{
					TweenSettingsExtensions.SetTarget<Tweener>(ShortcutExtensions.DOPunchPosition((Transform)handCardView.RectTransform, Vector3.up * materialCardPunchStrength, materialCardPunchDuration, materialCardPunchVibrato, materialCardPunchElasticity, false), (object)this);
				}
                GameLog.Data($"Resolve arrow from play zone material={card.material} cardIndex={step.SourceCardIndex} step={stepIndex}");
                playerCastAnimator?.PlayMaterialAction(step.PrimaryDisplayMaterial);
                MagicCastResult materialResult = ResolveArrowReadStepEffect(step);
                if (step.RemovesSourceAfterRead && !ContainsAfterReadActionStep(afterReadActionSteps, card))
                    afterReadActionSteps.Add(step);
				PlayMagicCastFeedback(materialResult);
				yield return PlayPendingEnemyDeaths();
				if (AllEnemiesDead())
				{
                    yield return ApplyArrowReadAfterActions(afterReadActionSteps);
					yield break;
				}
				yield return (object)new WaitForSeconds(postMagicResolveDelay);
			}

				if (step.FirstTokenIndex < 0)
				{
					continue;
				}

				int stepTokenCount = step.Tokens.Count;
				for (int tokenStart = step.FirstTokenIndex; tokenStart < step.FirstTokenIndex + stepTokenCount; tokenStart++)
				{
					CollectCastableMagicsByRecipeLength(readSequence.Tokens, tokenStart);
					if (castableMagicViews.Count == 0)
					{
						continue;
					}
					for (int matchedIndex = 0; matchedIndex < castableMagicViews.Count; matchedIndex++)
					{
						ResetMagicHighlights();
						MagicItemView matchedMagicView = castableMagicViews[matchedIndex];
						int matchLength = GetRecipeLength(matchedMagicView.Magic);
						MoveIndicatorToTokenRange(cards, readSequence.Tokens, tokenStart, matchLength, false);
						yield return (object)new WaitForSeconds(layoutDuration * 0.65f);
						for (int i = 0; i < matchLength; i++)
						{
							ArrowReadToken token = readSequence.Tokens[tokenStart + i];
							HandCardView handCardView = FindView(token.SourceCard);
							if ((Object)handCardView != (Object)null)
							{
								TweenSettingsExtensions.SetTarget<Tweener>(ShortcutExtensions.DOPunchPosition((Transform)handCardView.RectTransform, Vector3.up * materialCardPunchStrength, materialCardPunchDuration, materialCardPunchVibrato, materialCardPunchElasticity, false), (object)this);
								PlayMaterialFillParticle(handCardView, matchedMagicView, token.DisplayMaterial);
							}
						}
						yield return (object)new WaitForSeconds(GetParticleArrivalWait());
						for (int j = 0; j < matchLength; j++)
						{
							MaterialModifierModel.CurrentContext = new MaterialModifierContext { PlayerState = playerState, BattleManager = battleManager, Token = readSequence.Tokens[tokenStart + j], Step = step };
							readSequence.Tokens[tokenStart + j].SourceCard.TriggerOnTokenInvoke(readSequence.Tokens[tokenStart + j], step);
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
						GameLog.Data($"Resolve magic from arrow tokens magic={matchedMagicView.Magic.Id} tokenStart={tokenStart} length={matchLength}");
						CastMagic(matchedMagicView.Magic);
						yield return PlayPendingEnemyDeaths();
						if (AllEnemiesDead())
						{
						    yield return ApplyArrowReadAfterActions(afterReadActionSteps);
							yield break;
						}
						yield return (object)new WaitForSeconds(postMagicResolveDelay);
						ResetMagicHighlights();
					}
				}

		}
        yield return ApplyArrowReadAfterActions(afterReadActionSteps);
	}

    private static bool ContainsAfterReadActionStep(List<ArrowReadStep> steps, MaterialModel card)
    {
        if (steps == null || card == null)
            return false;

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null && steps[i].SourceCard == card)
                return true;
        }
        return false;
    }

    private IEnumerator ApplyArrowReadAfterActions(List<ArrowReadStep> steps)
    {
        if (steps == null || steps.Count == 0 || playerState == null)
            yield break;

        List<HandCardView> consumedViews = new List<HandCardView>();
        List<HandCardView> discardViews = new List<HandCardView>();
        List<HandCardView> returnViews = new List<HandCardView>();
        for (int i = 0; i < steps.Count; i++)
        {
            ArrowReadStep step = steps[i];
            MaterialModel card = step?.SourceCard;
            if (card == null)
                continue;

            HandCardView view = FindView(card);
            if (!playerState.ApplyArrowReadAfterAction(card, step.AfterReadAction) || (Object)view == (Object)null)
                continue;

            if (step.AfterReadAction == ArrowReadAfterReadAction.SplitIntoHalfArrowsToDiscard)
                discardViews.Add(view);
            else if (step.AfterReadAction == ArrowReadAfterReadAction.ReturnNextTurn)
                returnViews.Add(view);
            else
                consumedViews.Add(view);
        }

        RefreshStaticUI();
        RefreshMaterialListPanel();
        yield return AnimateRemovedReadViews(discardViews, GetDiscardPileArea());
        yield return AnimateRemovedReadViews(returnViews, deckPileArea != null ? deckPileArea : GetDiscardPileArea());
        yield return AnimateRemovedReadViews(consumedViews, GetConsumedPileArea());
    }

    private IEnumerator AnimateRemovedReadViews(List<HandCardView> views, RectTransform targetArea)
    {
        if (views == null || views.Count == 0)
            yield break;

        bool animationDone = false;
        AnimateViewsToArea(views, targetArea, (TweenCallback)delegate
        {
            animationDone = true;
        });
        while (!animationDone)
            yield return null;
    }

    private IEnumerator AnimateConsumedResolveCards(List<MaterialModel> cards)
    {
        if (cards == null || cards.Count == 0 || playerState == null)
            yield break;

        List<HandCardView> consumedViews = new List<HandCardView>();
        for (int i = 0; i < cards.Count; i++)
        {
            MaterialModel card = cards[i];
            if (card == null)
                continue;

            HandCardView view = FindView(card);
            if (playerState.ConsumeCardForBattle(card))
            {
                MarkBattleDeckCardConsumed(card);
                if ((Object)view != (Object)null)
                    consumedViews.Add(view);
            }
        }

        RefreshStaticUI();
        RefreshMaterialListPanel();
        if (consumedViews.Count == 0)
            yield break;

        bool animationDone = false;
        AnimateViewsToArea(consumedViews, GetConsumedPileArea(), (TweenCallback)delegate
        {
            animationDone = true;
        });
        while (!animationDone)
            yield return null;
    }

	private MagicCastResult ResolveArrowReadStepEffect(ArrowReadStep step)
	{
		MagicCastResult result = new MagicCastResult();
		if (step == null || step.SourceCard == null || playerState == null)
			return result;

			step.SourceCard.TriggerOnArrowBaseEffectResolve(new ArrowReadContext(playerState, battleManager));

		for (int i = 0; i < step.BaseEffectDirections.Count; i++)
			ResolveMaterialBaseEffect(step.BaseEffectDirections[i], result);
		return result;
	}

	private void ResolveMaterialBaseEffect(MaterialEnum material, MagicCastResult result)
	{
		switch (material)
		{
			case MaterialEnum.Fire:
				ResolveMaterialDamage(3, result);
				break;
			case MaterialEnum.Water:
				ResolveMaterialEnemyBuff(NextRunRandomInt(0, 2) == 0 ? BuffEnum.Weak : BuffEnum.Slow, 1, result);
				break;
			case MaterialEnum.Wind:
				playerState.AddBuff(BuffEnum.ExtraDraw, 1);
				GameLog.Data("Material Wind add player buff ExtraDraw stack=1");
				RefreshStaticUI();
				break;
			case MaterialEnum.Earth:
				int shieldGain = playerState.GainShield(3);
				GameLog.Data($"Material Earth shield player value={shieldGain}");
				result.playerShield += shieldGain;
				break;
		}
	}

	private void ResolveMaterialDamage(int damage, MagicCastResult result)
	{
		if (battleManager == null || damage <= 0)
			return;

		EnemyModel target = battleManager.BeginCastTarget();
		if (target == null)
			return;

		CombatantModel targetCombatant = new CombatantModel(target);
		CombatantModel caster = new CombatantModel(playerState);
		int attackValue = damage;
		playerState.TriggerOnAttack(targetCombatant, ref attackValue);
		CombatDamageResult damageResult = target.TakeDamageResult(attackValue, caster);
        int attackResult = damageResult.HealthDamage;
        playerState.TriggerAfterAttack(targetCombatant, ref attackResult);
		result.AddEnemyDamageHit(target, attackResult, damageResult.ShieldDamage);
		GameLog.Data($"Material Fire damage target={target.Id} value={attackValue}");
		battleManager.EndCastTarget();
	}

	private void ResolveMaterialEnemyBuff(BuffEnum buffType, int stack, MagicCastResult result)
	{
		if (battleManager == null || buffType == BuffEnum.None || stack <= 0)
			return;

		EnemyModel target = battleManager.BeginCastTarget();
		if (target == null)
			return;

		target.AddBuff(buffType, stack);
		result.enemyBuffApplied = true;
		GameLog.Data($"Material Water add buff target={target.Id} buff={buffType} stack={stack}");
		battleManager.EndCastTarget();
	}

	private void CollectCastableMagicsByRecipeLength(IReadOnlyList<ArrowReadToken> tokens, int startIndex)
	{
		castableMagicViews.Clear();
		for (int i = 0; i < magicViews.Count; i++)
		{
			MagicItemView magicItemView = magicViews[i];
			MagicModel magic = magicItemView.Magic;
			if (magic == null || !magic.IsMatch(tokens, startIndex))
			{
				continue;
			}
			int recipeLength = GetRecipeLength(magic);
			int insertIndex = castableMagicViews.Count;
			for (int j = 0; j < castableMagicViews.Count; j++)
			{
				if (recipeLength > GetRecipeLength(castableMagicViews[j].Magic))
				{
					insertIndex = j;
					break;
				}
			}
			castableMagicViews.Insert(insertIndex, magicItemView);
		}
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
        ClearMapDirectionCardsFromHandArea();
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
            SynchronizeCardSelectionState(!animateFromCurrent);
		}

        private void ClearSelectedCards(bool instant)
        {
            selectedCards.Clear();
            SynchronizeCardSelectionState(instant);
        }

        private void SynchronizeCardSelectionState(bool instant)
        {
            RemoveInvalidSelectedCards();
            for (int i = 0; i < cardViews.Count; i++)
            {
                HandCardView view = cardViews[i];
                if ((Object)view == (Object)null)
                    continue;

                bool shouldBeSelected = choosingEventCard
                    ? pendingChoiceCards.Contains(view.Card)
                    : selectedCards.Contains(view.Card) && playerState != null && playerState.Hand.Contains(view.Card);
                if (view.Selected != shouldBeSelected)
                    view.SetSelected(shouldBeSelected, instant);
		            }

		            if (playerState != null)
		                UpdateLayout(instant);
		            RefreshEndTurnButtonText();
		            RefreshPlayerAnimationState();
		        }

		        private void RestoreSelectedCardsFromViewState()

        {
            if (playerState == null || choosingEventCard)
                return;

            RemoveInvalidSelectedCards();
            for (int i = 0; i < cardViews.Count; i++)
            {
                HandCardView view = cardViews[i];
                if ((Object)view == (Object)null || !view.Selected)
                    continue;

                MaterialModel card = view.Card;
                if (card != null && playerState.Hand.Contains(card) && !selectedCards.Contains(card))
                    selectedCards.Add(card);
            }
        }

        private void RemoveInvalidSelectedCards()
        {
            if (playerState == null)
            {
                selectedCards.Clear();
                return;
            }

            for (int i = selectedCards.Count - 1; i >= 0; i--)
            {
                MaterialModel card = selectedCards[i];
                if (card == null || !playerState.Hand.Contains(card))
                    selectedCards.RemoveAt(i);
            }
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
        if (cards == null || area == null)
            return;

        int visibleCount = CountLayoutCards(cards);
        bool previewForArea = dragPreviewActive && dragPreviewTargetIsPlayZone == playZone;
        int layoutCount = visibleCount + (previewForArea ? 1 : 0);
        if (layoutCount <= 0)
            return;

        float spacing = GetLayoutSpacing(cards, area, playZone, layoutCount);
        float minX = layoutCount > 1 ? -spacing * (layoutCount - 1) * 0.5f : 0f;
        int hoverIndex = previewForArea ? -1 : GetLayoutHoverIndex(cards);
        int previewIndex = previewForArea ? Mathf.Clamp(dragPreviewIndex, 0, visibleCount) : -1;
        int spreadCenterIndex = previewForArea ? previewIndex : hoverIndex;
        BattleInputConfig inputConfig = GetBattleInputConfig();
        float spreadExtraSpacing = inputConfig.CardQueueSpreadExtraSpacing;
        float spreadFalloffPower = inputConfig.CardQueueSpreadFalloffPower;
        int visualIndex = 0;
        Vector2 position = default(Vector2);

		for (int i = 0; i < cards.Count; i++)
		{
            MaterialModel card = cards[i];
            if (IsCardExcludedFromLayout(card))
                continue;

            int layoutIndex = visualIndex;
            if (previewForArea && layoutIndex >= previewIndex)
                layoutIndex++;
            visualIndex++;

			HandCardView handCardView = FindView(card);
			if (!((Object)handCardView == (Object)null))
			{
				if ((Object)((Transform)handCardView.RectTransform).parent != (Object)area)
				{
					((Transform)handCardView.RectTransform).SetParent((Transform)area, true);
				}
                bool selectedForLift = !playZone && (choosingEventCard ? pendingChoiceCards.Contains(card) : selectedCards.Contains(card));
                float x = minX + spacing * layoutIndex + GetQueueSpreadOffset(layoutIndex, spreadCenterIndex, spreadExtraSpacing, spreadFalloffPower);
                float y = GetLayoutY(playZone) + (layoutIndex == hoverIndex || selectedForLift ? cardHoverYOffset : 0f);

				position = new Vector2(x, y);
				handCardView.SetInPlayZone(playZone);
				if (instant)
				{
					((Transform)handCardView.RectTransform).SetParent((Transform)area, false);
                    SetCardLayoutPosition(handCardView.RectTransform, position, playZone);
					handCardView.SetBaseRotation(0f, instant: true);
				}
				else
				{
					bool animateFromExistingView = (Object)((Transform)handCardView.RectTransform).parent == (Object)area && !newCardViews.Remove(handCardView);
					AnimateCardToLayoutTarget(handCardView, area, position, playZone, animateFromExistingView);
				}
			}
		}
		}

    private float GetQueueSpreadOffset(int layoutIndex, int centerIndex, float maxExtraSpacing, float falloffPower)
    {
        if (centerIndex < 0 || layoutIndex == centerIndex || maxExtraSpacing <= 0f)
            return 0f;

        int signedDistance = layoutIndex - centerIndex;
        float distance = Mathf.Abs(signedDistance);
        if (distance <= 0f)
            return 0f;

        float power = Mathf.Max(0f, falloffPower);
        float strength = 1f / Mathf.Pow(distance, power);
        return Mathf.Sign(signedDistance) * maxExtraSpacing * strength;
    }

    private int CountLayoutCards(List<MaterialModel> cards)

    {
        if (cards == null)
            return 0;

        int count = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (!IsCardExcludedFromLayout(cards[i]))
                count++;
        }
        return count;
    }

    private bool IsCardExcludedFromLayout(MaterialModel card)
    {
        return cardDragActive && card != null && card == draggedCard;
    }

    private int GetLayoutHoverIndex(List<MaterialModel> cards)
    {
        if (cardDragActive || layoutHoverCardView == null || cards == null)
            return -1;

        int visualIndex = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsCardExcludedFromLayout(cards[i]))
                continue;

            if (cards[i] == layoutHoverCardView.Card)
                return visualIndex;
            visualIndex++;
        }
        return -1;
    }



	private float GetLayoutSpacing(List<MaterialModel> cards, RectTransform area, bool playZone, int count)
    {
        float preferredSpacing = playZone ? playCardSpacing : cardSpacing;
        if (count <= 1 || area == null)
            return preferredSpacing;

        float cardWidth = 0f;
        for (int i = 0; cards != null && i < cards.Count; i++)
        {
            HandCardView view = FindView(cards[i]);
            if (view != null)
            {
                cardWidth = view.RectTransform.rect.width;
                if (cardWidth > 0f)
                    break;
            }
        }
        if (cardWidth <= 0f && cardPrefab != null)
            cardWidth = cardPrefab.rect.width;
        if (cardWidth <= 0f)
            return preferredSpacing;

        float availableSpacingWidth = Mathf.Max(0f, area.rect.width - cardWidth);
        return Mathf.Min(preferredSpacing, availableSpacingWidth / (count - 1));
    }

	private float GetLayoutY(bool playZone)
    {
        return playZone ? playLayoutY : handLayoutY;
    }

    private float GetLayoutZ(bool playZone)
    {
        return playZone ? playLayoutZ : handLayoutZ;
    }

	private void AnimateCardToLayoutTarget(HandCardView view, RectTransform targetParent, Vector2 targetAnchoredPosition, bool playZone, bool animateFromExistingView)
	{
        Vector3 position = ((Transform)view.RectTransform).position;
        Quaternion rotation = ((Transform)view.RectTransform).rotation;
        ShortcutExtensions.DOKill((Transform)view.RectTransform, false);

        if (!animateFromExistingView)
        {
            ((Transform)view.RectTransform).SetParent((Transform)targetParent, true);
            ((Transform)view.RectTransform).position = position;
            ((Transform)view.RectTransform).rotation = rotation;
        }

        Vector3 targetLocalPosition = GetCardLayoutLocalPosition(view.RectTransform, targetAnchoredPosition, playZone);
        Sequence sequence = DOTween.Sequence();
        TweenSettingsExtensions.Join(sequence, (Tween)TweenSettingsExtensions.SetEase<TweenerCore<Vector3, Vector3, VectorOptions>>(ShortcutExtensions.DOLocalMove((Transform)view.RectTransform, targetLocalPosition, layoutDuration, false), layoutEase));
        TweenSettingsExtensions.Join(sequence, (Tween)TweenSettingsExtensions.SetEase<TweenerCore<Quaternion, Vector3, QuaternionOptions>>(ShortcutExtensions.DOLocalRotate((Transform)view.RectTransform, Vector3.zero, layoutDuration, (RotateMode)0), layoutEase));
        TweenSettingsExtensions.SetTarget<Sequence>(sequence, (object)view.RectTransform);
	}

    private Vector3 GetCardLayoutLocalPosition(RectTransform rectTransform, Vector2 targetAnchoredPosition, bool playZone)
    {
        Vector2 anchoredPosition = rectTransform.anchoredPosition;
        Vector3 localPosition = rectTransform.localPosition;
        SetCardLayoutPosition(rectTransform, targetAnchoredPosition, playZone);
        Vector3 targetLocalPosition = rectTransform.localPosition;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.localPosition = localPosition;
        return targetLocalPosition;
    }

    private void SetCardLayoutPosition(RectTransform rectTransform, Vector2 anchoredPosition, bool playZone)
    {
        rectTransform.anchoredPosition = anchoredPosition;
        Vector3 localPosition = rectTransform.localPosition;
        localPosition.z = GetLayoutZ(playZone);
        rectTransform.localPosition = localPosition;
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
			deckCountText.text = playerState != null ? $"抽牌堆：{playerState.DrawPile.Count}\n弃牌堆：{playerState.DiscardPile.Count}\n已消耗：{playerState.ConsumedPile.Count}" : "抽牌堆\n弃牌堆\n已消耗";
		}
		if ((Object)(object)discardCountText != (Object)null)
		{
			discardCountText.text = string.Empty;
		}
		if ((Object)(object)consumedCountText != (Object)null)
		{
			consumedCountText.text = string.Empty;
		}
	        RefreshEndTurnButtonText();
	        RefreshRefreshChanceUI();
	        RefreshPlayerAnimationState();
		}

	    private int GetRemainingRefreshChanceCount()
	    {
		        if (playerState == null)
		            return 0;

		        int remaining = refreshUsedThisTurn ? 0 : 1;
		        return remaining + playerState.ExtraRefreshChancesThisTurn;
		    }



	    private void CacheRefreshChanceViews()
	    {
	        if ((Object)refreshButton == (Object)null)
	            return;

	        Transform refreshButtonTransform = ((Component)refreshButton).transform;
	        if ((Object)refreshChanceBadgeImage == (Object)null)
	            refreshChanceBadgeImage = UIManager.FindChildComponent<Image>(refreshButtonTransform, "RefreshChanceBadge");
	        if ((Object)refreshChanceText == (Object)null)
	            refreshChanceText = UIManager.FindChildComponent<TMP_Text>(refreshButtonTransform, "RefreshChanceBadge/CountText");
	    }

	    private void RefreshRefreshChanceUI()
	    {
	        CacheRefreshChanceViews();
	        int remaining = GetRemainingRefreshChanceCount();
	        bool active = remaining > 0 && (Object)refreshButton != (Object)null && refreshButton.interactable;

	        if ((Object)refreshChanceText != (Object)null)
	        {
	            refreshChanceText.text = remaining.ToString();
	            refreshChanceText.color = active ? refreshChanceTextColor : refreshChanceTextDisabledColor;
	        }

	        if ((Object)refreshChanceBadgeImage != (Object)null)
	            refreshChanceBadgeImage.color = active ? refreshChanceBadgeColor : refreshChanceBadgeDisabledColor;
		}

	    private void CacheEndTurnButtonText()
    {
        if ((Object)endTurnButton == (Object)null)
            return;

        if ((Object)endTurnButtonText == (Object)null)
        {
            endTurnButtonText = UIManager.FindChildComponent<TMP_Text>(((Component)endTurnButton).transform, "Text");
            if ((Object)endTurnButtonText == (Object)null)
                endTurnButtonText = ((Component)endTurnButton).GetComponentInChildren<TMP_Text>(true);
        }

        if ((Object)endTurnButtonImage == (Object)null)
        {
            Transform iconTransform = ((Component)endTurnButton).transform.Find("Image (1)");
            endTurnButtonImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            if ((Object)endTurnButtonImage == (Object)null)
                endTurnButtonImage = endTurnButton.targetGraphic as Image;
            if ((Object)endTurnButtonImage == (Object)null)
                endTurnButtonImage = ((Component)endTurnButton).GetComponent<Image>();
            if ((Object)endTurnButtonImage == (Object)null)
                endTurnButtonImage = ((Component)endTurnButton).GetComponentInChildren<Image>(true);
            if ((Object)endTurnButtonImage != (Object)null)
                endTurnButtonDefaultSprite = endTurnButtonImage.sprite;
        }
    }

    private void RefreshEndTurnButtonText()
    {
        CacheEndTurnButtonText();
        bool preparing = HasSelectedArrowCard();
        if ((Object)endTurnButtonText != (Object)null)
        {
            endTurnButtonText.text = preparing
                ? LocalizationSystem.GetText("ui.battle.end_turn.prepare", "预备")
                : LocalizationSystem.GetText("ui.battle.end_turn.resolve", "出手");
        }

        if ((Object)endTurnButtonImage != (Object)null)
        {
            Sprite sprite = preparing ? endTurnPrepareSprite : endTurnResolveSprite;
            endTurnButtonImage.sprite = (Object)sprite != (Object)null ? sprite : endTurnButtonDefaultSprite;
        }

        RefreshEndTurnButtonInteractable(preparing);
    }

    private void RefreshEndTurnButtonInteractable(bool preparing)
    {
        if ((Object)endTurnButton == (Object)null)
            return;

        endTurnButton.interactable = buttonsInteractable;
        if (!endTurnButton.interactable && (Object)playerCastAnimator != (Object)null)
            playerCastAnimator.ClearEndTurnHover();
    }

	    private bool HasSelectedArrowCard()
	    {
        if (playerState == null)
            return false;

        for (int i = 0; i < selectedCards.Count; i++)

	        {
	            MaterialModel card = selectedCards[i];
	            if (card != null && playerState.Hand.Contains(card) && !playerState.IsMaterialDisabled(card) && card.IsArrowReadable())
	                return true;
	        }
	        return false;
	    }

	    private bool AreSelectedCardsOnlyInHand()
	    {
	        if (playerState == null)
	            return false;

	        for (int i = 0; i < selectedCards.Count; i++)
	        {
	            MaterialModel card = selectedCards[i];
	            if (card == null || !playerState.Hand.Contains(card))
	                return false;
	        }
	        return selectedCards.Count > 0;
	    }

	    private void EnsureActionButtonMotion()

    {
        if ((Object)refreshButton != (Object)null)
            AddJuicyMotion(((Component)refreshButton).transform);
        if ((Object)endTurnButton != (Object)null)
            AddJuicyMotion(((Component)endTurnButton).transform);
    }

	private void EnsurePileButtons()
	{
		BindPileButton(deckPileArea, ToggleMaterialListPanel);
		UnbindPileButton(discardPileArea, ToggleDiscardPilePanel);
		UnbindPileButton(consumedPileArea, ToggleConsumedPilePanel);
	}

	private void BindPileButton(RectTransform pileArea, UnityAction action)
	{
		if (pileArea == null)
			return;

		Button button = pileArea.GetComponent<Button>();
		if (button == null)
			button = pileArea.gameObject.AddComponent<Button>();
		button.onClick.RemoveListener(action);
		button.onClick.AddListener(action);
		AddJuicyMotion((Transform)(object)pileArea);
	}

	private void UnbindPileButton(RectTransform pileArea, UnityAction action)
	{
		if (pileArea == null)
			return;

		Button button = pileArea.GetComponent<Button>();
		if (button != null)
			button.onClick.RemoveListener(action);
	}

	private void ToggleMaterialListPanel()
	{
		GetUIManager().ToggleMaterialListPanel();
	}

	private void ToggleDiscardPilePanel()
	{
		GetUIManager().ToggleDiscardPilePanel();
	}

	private void ToggleConsumedPilePanel()
	{
		GetUIManager().ToggleConsumedPilePanel();
	}

	private void RefreshMaterialListPanel()
	{
		GetUIManager().RefreshMaterialListPanel();
	}

	private void CreateEnemyHealthView(RectTransform enemyView, EnemyData enemyData, bool preserveLayout = false)
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
			SetupHealthText(enemyHealthText);

		if ((Object)enemyHealthFill == (Object)null)
		{
			return;
		}
		Transform parent = ((Transform)((Component)enemyHealthFill).GetComponent<RectTransform>()).parent;
		RectTransform val2 = (RectTransform)((parent is RectTransform) ? parent : null);
		if (!((Object)val2 == (Object)null))
		{
				bool hasInfoBoxWidthOverride = enemyData != null && enemyData.infoBoxSize.x > 0f;
				Vector2 infoBoxOffset = enemyData != null ? enemyData.infoBoxOffset : Vector2.zero;
				float healthBarWidth = enemyData != null && enemyData.healthBarWidth > 0f
					? enemyData.healthBarWidth
					: hasInfoBoxWidthOverride ? enemyData.infoBoxSize.x : 0f;
				if (!preserveLayout)
				{
					val2.anchorMin = new Vector2(0.5f, 0.5f);
					val2.anchorMax = new Vector2(0.5f, 0.5f);
					val2.pivot = new Vector2(0.5f, 0.5f);
					val2.anchoredPosition = new Vector2(0f, -56f) + infoBoxOffset;
					val2.sizeDelta = new Vector2(healthBarWidth > 0f ? healthBarWidth : 150f, val2.sizeDelta.y > 0f ? val2.sizeDelta.y : 14f);
				}
				else if (healthBarWidth > 0f)
				{
					val2.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, healthBarWidth);
				}


			SetupFillImage(enemyHealthFill, new Color(0.82f, 0.05f, 0.04f, 1f), 1);

			enemyHealthBufferFill = CreateHealthFillLayer(val2, "HealthBufferFill", Color.white, 0);
			enemyShieldFill = CreateHealthFillLayer(val2, "ShieldFill", new Color(0.2f, 0.55f, 1f, 1f), 2);
				SetHealthLayerOrder(enemyHealthBufferFill, enemyHealthFill, enemyShieldFill);
					enemyShieldText = FindEnemyShieldText(enemyView);
					if ((Object)enemyShieldText == (Object)null)
						enemyShieldText = CreateEnemyShieldText(val2);
		            if (!preserveLayout)
					HealthBarUI.PositionHealthTextRightOfBar(enemyHealthText, val2, EnemyHealthTextWidth);

		}
		}

		private TMP_Text FindEnemyShieldText(RectTransform enemyView)
		{
			TMP_Text[] texts = ((Component)enemyView).GetComponentsInChildren<TMP_Text>(true);
			for (int i = 0; i < texts.Length; i++)
			{
				if (((Object)texts[i]).name == "ShieldText")
					return texts[i];
			}
			return null;
		}

		private TMP_Text CreateEnemyShieldText(RectTransform barBack)
		{
			TMP_Text text = new GameObject("ShieldText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
			text.transform.SetParent((Transform)barBack, false);
			RectTransform rect = ((Component)text).GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0f);
			rect.anchorMax = new Vector2(0.5f, 0f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, -4f);
			rect.sizeDelta = new Vector2(56f, 24f);
			return text;
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
			HealthBarUI.SetupHealthText(text);
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
			HealthBarUI.SetHealthTextColor(enemyHealthText, false);
				enemyHealthNumberTween = UpdateHealthText(enemyHealthText, enemyShieldText, displayedEnemyHealth, enemyModel.CurrentHealth, enemyModel.Data.maxHealth, enemyModel.Shield, instant, delegate(int healthValue)

			{
				displayedEnemyHealth = healthValue;
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

			private Tween UpdateHealthText(TMP_Text text, TMP_Text shieldText, int displayedHealth, int currentHealth, int maxHealth, int shield, bool instant, Action<int> setDisplayedHealth)

		{
			if ((Object)text == (Object)null)
			{
				return null;
			}
			if (instant)
			{
				setDisplayedHealth(Mathf.Max(0, currentHealth));
					text.text = HealthBarUI.GetHealthTextValue(currentHealth, maxHealth, shield);
					HealthBarUI.ApplyShieldText(shieldText, shield);

				return null;
			}
			return (Tween)TweenSettingsExtensions.SetTarget<Tweener>(TweenSettingsExtensions.SetEase<Tweener>(DOVirtual.Int(displayedHealth, Mathf.Max(0, currentHealth), enemyHealthTextDuration, (TweenCallback<int>)delegate(int value)
			{
					setDisplayedHealth(value);
					text.text = HealthBarUI.GetHealthTextValue(value, maxHealth, shield);
					HealthBarUI.ApplyShieldText(shieldText, shield);

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
				for (int i = 0; i < componentsInChildren.Length; i++)
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
			((Transform)state.viewRect).localScale = GetEnemyViewScale(state.model, visibleCount);
			JuicyMotion motion = ((Component)state.viewRect).GetComponent<JuicyMotion>();
			if ((Object)motion != (Object)null)
				motion.SetBaseScale(((Transform)state.viewRect).localScale, applyImmediately: true);

        }
    }

    private Vector3 GetEnemyViewScale(EnemyModel model, int visibleCount)
    {
        float scale = visibleCount >= 3 ? 0.88f : 1f;
        if (model != null && model.IsMinion)
            scale *= 0.6f;
        return Vector3.one * scale;
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
		((Transform)val).localScale = GetEnemyViewScale(model, count);
		if ((Object)enemyMotion != (Object)null)
			enemyMotion.SetBaseScale(((Transform)val).localScale, applyImmediately: true);
		EnemyViewState enemyViewState = new EnemyViewState();
		enemyViewState.model = model;
		enemyViewState.viewRect = val;
        enemyViewState.viewUI = ((Component)val).GetComponent<EnemyViewUI>();
        if ((Object)enemyViewState.viewUI != (Object)null)
        {
            enemyViewState.viewUI.CacheMissingReferences();
            enemyViewState.motionRoot = enemyViewState.viewUI.MotionRoot;
            enemyViewState.nameText = enemyViewState.viewUI.NameText;
            enemyViewState.healthFill = enemyViewState.viewUI.HealthFill;
            enemyViewState.healthBufferFill = enemyViewState.viewUI.HealthBufferFill;
            enemyViewState.shieldFill = enemyViewState.viewUI.ShieldFill;
	            enemyViewState.healthText = enemyViewState.viewUI.HealthText;
	            enemyViewState.shieldText = enemyViewState.viewUI.ShieldText;
	            enemyViewState.bodyImage = enemyViewState.viewUI.BodyImage;

            enemyViewState.focusMarker = enemyViewState.viewUI.FocusMarker;
		    enemyViewState.buffRoot = enemyViewState.viewUI.BuffRoot;
		    enemyViewState.buffPopupEffect = enemyViewState.viewUI.BuffPopupEffect;
		    enemyViewState.intentRoot = enemyViewState.viewUI.IntentRoot;

        }
		Image[] componentsInChildren = ((Component)val).GetComponentsInChildren<Image>(true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (((Object)componentsInChildren[i]).name == "HealthFill" && (Object)enemyViewState.healthFill == (Object)null)
			{
				enemyViewState.healthFill = componentsInChildren[i];
			}
			else if (((Object)componentsInChildren[i]).name == "IntentIcon" && (Object)enemyViewState.intentIcon == (Object)null)
			{
				enemyViewState.intentIcon = componentsInChildren[i];
			}
			else if (((Object)componentsInChildren[i]).name == "Body" && (Object)enemyViewState.bodyImage == (Object)null)
			{
				enemyViewState.bodyImage = componentsInChildren[i];
			}
		}
			if ((Object)enemyViewState.bodyImage != (Object)null)
			{
				EnemySpriteAnimatorUI bodyAnimator = (Object)enemyViewState.viewUI != (Object)null ? enemyViewState.viewUI.BodyAnimator : null;
            if ((Object)bodyAnimator == (Object)null)
                bodyAnimator = ((Component)enemyViewState.bodyImage).GetComponent<EnemySpriteAnimatorUI>();
			if ((Object)bodyAnimator == (Object)null)
				bodyAnimator = ((Component)enemyViewState.bodyImage).gameObject.AddComponent<EnemySpriteAnimatorUI>();

			bodyAnimator.Bind(model.Data);
			enemyViewState.bodyBaseColor = enemyViewState.bodyImage.color;
		}
		enemyModel = model;
		enemyViewRect = val;
		enemyHealthFill = enemyViewState.healthFill;
		enemyHealthBufferFill = enemyViewState.healthBufferFill;
		enemyShieldFill = enemyViewState.shieldFill;
			enemyHealthText = enemyViewState.healthText;
			enemyShieldText = enemyViewState.shieldText;
			enemyIntentIcon = enemyViewState.intentIcon;

		enemyBodyImage = enemyViewState.bodyImage;
		enemyBodyBaseColor = enemyViewState.bodyBaseColor;
		EnsureEnemyIntentView(val);
		enemyViewState.intentIcon = enemyIntentIcon;
		CreateEnemyHealthView(val, model.Data, (Object)enemyViewState.viewUI != (Object)null);
		if ((Object)enemyHealthBufferFill != (Object)null)
			enemyViewState.healthBufferFill = enemyHealthBufferFill;
		if ((Object)enemyShieldFill != (Object)null)
			enemyViewState.shieldFill = enemyShieldFill;
	        if ((Object)enemyHealthText != (Object)null)
	            enemyViewState.healthText = enemyHealthText;
			if ((Object)enemyShieldText != (Object)null)
				enemyViewState.shieldText = enemyShieldText;
		        enemyViewState.displayedHealth = Mathf.Max(0, model.CurrentHealth);


		EnemyViewClickHandler enemyViewClickHandler = ((Component)val).GetComponent<EnemyViewClickHandler>();
		if ((Object)enemyViewClickHandler == (Object)null)
		{
			enemyViewClickHandler = ((Component)val).gameObject.AddComponent<EnemyViewClickHandler>();
		}
		enemyViewClickHandler.Bind(this, model);
		if ((Object)enemyViewState.focusMarker == (Object)null)
			enemyViewState.focusMarker = EnsureFocusMarker(val);
		if ((Object)enemyViewState.buffRoot == (Object)null)
			enemyViewState.buffRoot = EnsureBuffRoot(val, new Vector2(0f, -82f));
        if ((Object)enemyViewState.viewUI != (Object)null)
            enemyViewState.viewUI.ApplyDataLayout(model.Data);
			RebuildEnemyIntentViews(enemyViewState);
			BindEnemyBuffPopup(enemyViewState);
			enemyViewStates.Add(enemyViewState);

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
		Transform target = ((Component)this).transform.Find("PlayerArea/PlayerCastAnimator");
		if ((Object)(object)target == (Object)null)
			target = ((Component)this).transform.Find("PlayerCastAnimator");
		playerCastAnimator = ((Object)(object)target != (Object)null) ? ((Component)target).GetComponent<PlayerCastAnimatorUI>() : null;
		if (!((Object)playerCastAnimator == (Object)null))
		{
            playerCastAnimator.Initialize();
            playerCastAnimator.SetReleaseHandler(HandleCastReleaseFrame);
            RegisterPlayerAnimationHoverRelays();
            RefreshPlayerAnimationState();
        }
    }

    private void RegisterPlayerAnimationHoverRelays()
    {
        if ((Object)playerCastAnimator == (Object)null)
            return;

        if ((Object)endTurnButton != (Object)null)
            BindPlayerAnimationHoverRelay(((Component)endTurnButton).transform);
    }

    private void BindPlayerAnimationHoverRelay(Transform target)
    {
        if ((Object)target == (Object)null)
            return;

        PlayerAnimationHoverRelayUI relay = ((Component)target).GetComponent<PlayerAnimationHoverRelayUI>();
        if ((Object)relay == (Object)null)
            relay = ((Component)target).gameObject.AddComponent<PlayerAnimationHoverRelayUI>();
        relay.Bind(playerCastAnimator);
    }

    private void RefreshPlayerAnimationState()
    {
        if ((Object)playerCastAnimator == (Object)null)
            return;

        bool hasMaterialInPlayZone = playerState != null && playerState.PlayZone.Count > 0;
        playerCastAnimator.SetMagicSelectionActive(selectedCards.Count > 0 || hasMaterialInPlayZone);
    }

	    private void OnPlayerBuffAdded(BuffEnum buffType, int stack)
	    {
	        if (stack <= 0)
	            return;

	        if ((Object)playerBuffPopupEffect == (Object)null)
	            playerBuffPopupEffect = ((Component)this).transform.Find("PlayerArea/PlayerCastAnimator/BuffEffectRoot")?.GetComponent<BuffPopupEffectController>();
	        if ((Object)playerBuffPopupEffect != (Object)null)
	            playerBuffPopupEffect.Play(buffType);

	        if (BuffModel.GetKind(buffType) != BuffKindEnum.DeBuff)
	            return;

	        if ((Object)playerCastAnimator == (Object)null)
	            CreatePlayerCastAnimator();
	        if ((Object)playerCastAnimator != (Object)null)
	            playerCastAnimator.PlayNegativeStatus();
	    }



	private void BindEnemyBuffPopup(EnemyViewState state)
	{
		if (state == null || state.model == null)
			return;

		state.model.BuffAdded -= HandleEnemyBuffAdded;
		state.model.BuffAdded += HandleEnemyBuffAdded;
	}

	private void HandleEnemyBuffAdded(EnemyModel enemy, BuffEnum buffType, int stack)
	{
		if (enemy == null || stack <= 0)
			return;

		for (int i = 0; i < enemyViewStates.Count; i++)
		{
			EnemyViewState state = enemyViewStates[i];
			if (state == null || state.model != enemy)
				continue;

			if ((Object)state.buffPopupEffect == (Object)null)
				state.buffPopupEffect = state.viewUI != null ? state.viewUI.BuffPopupEffect : null;
			if ((Object)state.buffPopupEffect != (Object)null)
				state.buffPopupEffect.Play(buffType);
			return;
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
        AudioManager.Instance.PlaySfx(GameSfxId.HitPitch, pitch);
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
        if (castReleaseHandled)
            return;

        castReleaseHandled = true;
		int shakeCount = pendingCastShakeCount;
		pendingCastShakeCount = 0;
        PlayPlayerCastSwingSfx(shakeCount);
		PlayCastScreenShake(shakeCount);
		PlayPendingCastParticle();
	}

    private void ArmCastReleaseFallback(float releaseWait)
    {
        castReleaseToken++;
        castReleaseHandled = false;
        if (castReleaseFallbackRoutine != null)
            StopCoroutine(castReleaseFallbackRoutine);
        castReleaseFallbackRoutine = StartCoroutine(CastReleaseFallbackRoutine(castReleaseToken, Mathf.Max(0f, releaseWait)));
    }

    private IEnumerator CastReleaseFallbackRoutine(int token, float releaseWait)
    {
        if (releaseWait > 0f)
            yield return new WaitForSeconds(releaseWait);

        if (token == castReleaseToken && !castReleaseHandled)
            HandleCastReleaseFrame();
        if (token == castReleaseToken)
            castReleaseFallbackRoutine = null;
    }

	private void PlayCastScreenShake(int continuousCastCount)
	{
		if (battleManager == null)
			return;

		if (continuousCastCount <= 0)
            return;

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
		pendingCastShakeCount = 0;
		if (!magicView.Magic.Data.playPlayerCastAnimation)
		{
			pendingCastShakeCount = 0;
			return 0f;
		}

		bool animationStarted = PlayPlayerCastAnimation();
		float releaseWait = GetCastReleaseWait();
        float impactWait = 0f;
        bool targetsAllEnemies = MagicTargetsAllEnemiesForCast(magicView.Magic);
        bool shouldTryCastParticle = targetsAllEnemies || GetMagicEffectTargetType(magicView.Magic) == SpellEffectTarget.Enemy;
        if (shouldTryCastParticle && spellCastEffect != null)
        {
            if (targetsAllEnemies)
            {
                CollectAliveEnemyTargetRects(castParticleTargetBuffer);
                if (castParticleTargetBuffer.Count > 0)
                {
                    QueueCastParticles(magicView.Magic, castParticleTargetBuffer, onImpact);
                    pendingCastShakeCount = battleManager != null ? battleManager.ContinuousCastCount + 1 : 1;
                    impactWait = GetCastParticleImpactStartWait();
                }
            }
            else
            {
				RectTransform magicEffectTarget = GetMagicEffectTarget(magicView.Magic, targetEnemy);
				if ((Object)magicEffectTarget != (Object)null)
                {
					QueueCastParticle(magicView.Magic, magicEffectTarget, onImpact);
                    pendingCastShakeCount = battleManager != null ? battleManager.ContinuousCastCount + 1 : 1;
                    impactWait = GetCastParticleImpactStartWait();
                }
            }
        }

		if (!animationStarted)
		{
            castReleaseToken++;
            castReleaseHandled = false;
			HandleCastReleaseFrame();
			return impactWait;
		}
        ArmCastReleaseFallback(releaseWait);
		return releaseWait + impactWait;
	}

	private IEnumerator FinishBattleRoutine()
	{
		HideContinuousCastCounterUI();
		yield return PlayPendingEnemyDeaths();
        if (battleManager != null && battleManager.KillAliveMinionsForVictory())
        {
            RefreshEnemyUI((RectTransform)null, false);
            yield return PlayPendingEnemyDeaths();
        }
		List<HandCardView> views = new List<HandCardView>(cardViews);
		List<MaterialModel> battleEndRemovedTemporaryCards = new List<MaterialModel>();
		battleManager?.FinishBattleRules(battleEndRemovedTemporaryCards);
        TutorialManager?.EndTutorialBattle();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameplayMusic();
		ResetMagicHighlights();
		GetUIManager().PlayArea?.HideResolveIndicator();
		bool returnDone = false;
		AnimateReturningViews(views, battleEndRemovedTemporaryCards, GetDiscardPileArea(), (TweenCallback)delegate
		{
			returnDone = true;
		});
		while (!returnDone)
			yield return null;

			RestoreBattleDeckState();
			RebuildCards(animateFromCurrent: true);
			refreshUsedThisTurn = false;
            if (debugBattleActive)
            {
                debugBattleActive = false;
                currentLevel = null;
                busy = false;
                SetButtonsInteractable(true);
                yield break;
            }
			busy = true;
			SetButtonsInteractable(interactable: false);
	        if (ShouldCompleteRunAfterCurrentBattle())
        {
            TutorialManager?.CompleteTutorial(playerState, mapNodes, currentMapNodeIndex, activeChapter ?? GetActiveChapter(), currentLevel);
            ShowVictoryPanel();
            yield break;
        }
		ShowRewardPanel();
	}

    private bool ShouldCompleteRunAfterCurrentBattle()
    {
        return currentChapterMapBossLevel && IsFinalChapter(activeChapter ?? GetActiveChapter());
    }

    private bool IsFinalChapter(ChapterData chapter)
    {
        if (chapter == null)
            return false;

        int lastChapterNumericId = 0;
        foreach (ChapterData data in GameDataDatabase.ChapterData.Values)
        {
            if (data != null && data.numericId > lastChapterNumericId)
                lastChapterNumericId = data.numericId;
        }
        return chapter.numericId >= lastChapterNumericId;
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
        if (runEnded || !TryBuildRunCheckpointKey(out string checkpointKey) || checkpointKey == lastRunCheckpointKey)
            return;

        RunSaveSystem.SaveCurrentRun(playerState, mapNodes, currentMapNodeIndex, activeChapter ?? GetActiveChapter(), currentLevel, GetCurrentRunPlaySeconds(), battleManager, currentEvent);
        lastRunCheckpointKey = checkpointKey;
    }

    private bool TryBuildRunCheckpointKey(out string checkpointKey)
    {
        checkpointKey = null;
        if (playerState == null || mapNodes == null || mapNodes.Count == 0)
            return false;

        if (currentLevel == null)
        {
            if (chapterMapMoveInProgress)
                return false;

            RunMapGridModel grid = ChapterMapGrid;
            int mapX = grid != null ? grid.playerX : 0;
            int mapY = grid != null ? grid.playerY : 0;
            bool bossMapActive = grid != null && grid.bossMapActive;
            checkpointKey = $"Map:{currentMapNodeIndex}:{mapX}:{mapY}:{bossMapActive}";
            return true;
        }

        if (!IsCurrentLevelContentReadyForSave())
            return false;

        checkpointKey = $"Node:{currentMapNodeIndex}:{currentLevel.numericId}";
        return true;
    }

    private bool IsCurrentLevelContentReadyForSave()
    {
        if (currentLevel == null)
            return false;

        if (currentLevel.levelType == LevelType.Battle || currentLevel.levelType == LevelType.Elite)
            return battleManager != null && battleManager.Enemies != null && battleManager.Enemies.Count > 0;

        if (currentLevel.levelType == LevelType.Shop)
            return uiManager != null && uiManager.ShopPanel != null && uiManager.ShopPanel.gameObject.activeInHierarchy;

        if (IsEventLikeLevel(currentLevel.levelType) || currentLevel.levelType == LevelType.Rest)
            return currentEvent != null && eventPanel != null;

        return false;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveRunProgress();
        else
            ApplyLetterboxCameraSettings();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused)
            ApplyLetterboxCameraSettings();
    }

    private void OnApplicationQuit()
    {
        SaveRunProgress();
    }

    private void ApplyLetterboxCameraSettings()
    {
        Camera targetCamera = cachedMainCamera != null ? cachedMainCamera : Camera.main;
        if (targetCamera == null)
            return;

        cachedMainCamera = targetCamera;
        targetCamera.rect = DefaultWideViewportRect;
        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor = LetterboxColor;
    }

    private void ShowMagicModifierSelection(int choiceCount, Action completed = null)
    {
        ShowMagicModifierSelection(GetMagicModifierChoices(choiceCount), completed ?? FinishRestLevel);
    }

    private void ShowMagicModifierSelection(IReadOnlyList<MagicModifierData> choices, Action completed)
    {
        busy = true;
        SetButtonsInteractable(interactable: false);
        pendingMagicModifier = null;
        MagicModifierSelectionPanelUI panel = GetUIManager().MagicModifierSelectionPanel;
        if (panel != null)
            panel.Show(choices, completed);
        else
            completed?.Invoke();
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
            MagicModifierData data = pool[NextRunRandomInt(0, pool.Count)];
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

	private void ResetBattleDeckState()
	{
		consumedBattleDeckCards.Clear();
		playerState.Hand.Clear();
		playerState.PlayZone.Clear();
		playerState.DrawPile.Clear();
        playerState.DiscardPile.Clear();
        playerState.ConsumedPile.Clear();
		playerState.DrawPile.AddRange(playerState.Deck);
		RefreshMaterialListPanel();
	}

	private void RestoreBattleDeckState()
	{
		consumedBattleDeckCards.Clear();
		playerState.Hand.Clear();
		playerState.PlayZone.Clear();
		playerState.DrawPile.Clear();
        playerState.DiscardPile.Clear();
        playerState.ConsumedPile.Clear();
		playerState.DrawPile.AddRange(playerState.Deck);
		RefreshMaterialListPanel();
	}

	private void MarkBattleDeckCardConsumed(MaterialModel card)
	{
		if (card != null && playerState != null && playerState.Deck.Contains(card))
		{
			consumedBattleDeckCards.Add(card);
            playerState.AddConsumedCard(card);
			RefreshMaterialListPanel();
		}
	}

	public List<MagicData> GetRewardMagicChoices()
    {
        return GetRewardMagicChoices(3);
    }

	public List<MagicData> GetRewardMagicChoices(int choiceCount)
	{
		if (TutorialManager != null && TutorialManager.MainTutorialRunning)
			return GetTutorialRewardMagicChoices(choiceCount);

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
		while (list2.Count < choiceCount && list.Count > 0 && num < 30)
		{
			num++;
			MagicData item = list[NextRunRandomInt(0, list.Count)];
			if (!list2.Contains(item))
			{
				list2.Add(item);
			}
		}
		return list2;
	}

	private List<MagicData> GetTutorialRewardMagicChoices(int choiceCount)
	{
		List<MagicData> choices = new List<MagicData>();
		foreach (MagicData data in GameDataDatabase.MagicData.Values)
		{
			if (data != null)
			{
				choices.Add(data);
				if (choices.Count >= choiceCount)
					break;
			}
		}
		return choices;
	}

		public void SelectPendingRewardMagic(MagicData rewardMagic)
		{
	        HideRewardMagicConfirmPanel(false);
			pendingRewardMagic = rewardMagic;
	        RefreshPlayerAnimationState();
	        if (rewardMagic != null)
	            ClearPendingShopMagic();
	        else
	            GetUIManager().HideSlotSelect();
			if (rewardMagic != null)
			{
				TutorialManager?.OnRewardMagicSelected();
				GetUIManager().HideSlotSelect();
			}
		}

	    public void SelectPendingShopMagic(MagicData magicData, Action<int> onSlotChosen)
	    {
	        pendingShopMagic = magicData;
	        pendingShopMagicSlotChosen = onSlotChosen;
	        RefreshPlayerAnimationState();
        if (magicData != null)
        {
            pendingRewardMagic = null;
            GetUIManager().HideSlotSelect();
        }

	        else
	        {
	            GetUIManager().HideSlotSelect();
	        }
	    }

	    public void ClearPendingShopMagic()
	    {
	        pendingShopMagic = null;
	        pendingShopMagicSlotChosen = null;
	        GetUIManager().HideSlotSelect();
	        RefreshPlayerAnimationState();
	    }

    public bool TryPlacePendingShopMagic(int slotIndex)
    {
        if (pendingShopMagic == null)
            return false;

        if (ShouldConfirmRewardMagicOnMobile() && ShowShopMagicConfirmPanel(pendingShopMagic, slotIndex))
            return true;

        Action<int> slotChosen = pendingShopMagicSlotChosen;
        ClearPendingShopMagic();
        slotChosen?.Invoke(slotIndex);
        return true;
    }

	public bool TryPlacePendingRewardMagic(int slotIndex)
	{
		if (pendingRewardMagic == null)
			return false;

        MagicData rewardMagic = pendingRewardMagic;
        RectTransform sourceRect = GetUIManager().RewardPanel != null ? GetUIManager().RewardPanel.SelectedMagicRect : null;
        if (ShouldConfirmRewardMagicOnMobile() && ShowRewardMagicConfirmPanel(rewardMagic, slotIndex, sourceRect))
            return true;

        pendingRewardMagic = null;
        RefreshPlayerAnimationState();
        StartCoroutine(SetRewardMagicAtSlotAnimatedRoutine(rewardMagic, slotIndex, sourceRect));
		return true;
	}

    private bool ShouldConfirmRewardMagicOnMobile()
    {
        return ShouldUseMobileInteraction();
    }


    public bool ShouldUseMobileInteraction()
    {
        if (Application.isMobilePlatform)
            return true;
#if UNITY_EDITOR
        return simulateMobileInteractionInEditor;
#else
        return false;
#endif
    }


    private bool ShowShopMagicConfirmPanel(MagicData magicData, int slotIndex)

    {
        EnsureRewardMagicConfirmPanel();
        if (rewardMagicConfirmPanel == null)
            return false;

        rewardMagicConfirmSlotIndex = slotIndex;
        rewardMagicConfirmSourceRect = null;
        BindRewardMagicConfirmView(rewardMagicConfirmExistingRoot, playerState.GetMagicAtSlot(slotIndex));
        BindRewardMagicConfirmView(rewardMagicConfirmNewRoot, MagicFactory.Create(magicData, slotIndex));

        if (rewardMagicConfirmButton != null)
        {
            rewardMagicConfirmButton.onClick.RemoveAllListeners();
            rewardMagicConfirmButton.onClick.AddListener(ConfirmShopMagicPlacement);
        }
        if (rewardMagicConfirmCancelButton != null)
        {
            rewardMagicConfirmCancelButton.onClick.RemoveAllListeners();
            rewardMagicConfirmCancelButton.onClick.AddListener(CancelShopMagicPlacementConfirm);
        }

        rewardMagicConfirmPanel.gameObject.SetActive(true);
        rewardMagicConfirmPanel.SetAsLastSibling();
        return true;
    }

	    private bool ShowRewardMagicConfirmPanel(MagicData rewardMagic, int slotIndex, RectTransform sourceRect)
    {
        EnsureRewardMagicConfirmPanel();
        if (rewardMagicConfirmPanel == null)
            return false;

        rewardMagicConfirmSlotIndex = slotIndex;
        rewardMagicConfirmSourceRect = sourceRect;
        BindRewardMagicConfirmView(rewardMagicConfirmExistingRoot, playerState.GetMagicAtSlot(slotIndex));
        BindRewardMagicConfirmView(rewardMagicConfirmNewRoot, MagicFactory.Create(rewardMagic, slotIndex));

        if (rewardMagicConfirmButton != null)
        {
            rewardMagicConfirmButton.onClick.RemoveAllListeners();
            rewardMagicConfirmButton.onClick.AddListener(ConfirmRewardMagicPlacement);
        }
        if (rewardMagicConfirmCancelButton != null)
        {
            rewardMagicConfirmCancelButton.onClick.RemoveAllListeners();
            rewardMagicConfirmCancelButton.onClick.AddListener(CancelRewardMagicPlacementConfirm);
        }

        rewardMagicConfirmPanel.gameObject.SetActive(true);
        rewardMagicConfirmPanel.SetAsLastSibling();
        return true;
    }

    private void ConfirmRewardMagicPlacement()
    {
        if (pendingRewardMagic == null || rewardMagicConfirmSlotIndex < 0)
        {
            HideRewardMagicConfirmPanel(false);
            return;
        }

        MagicData rewardMagic = pendingRewardMagic;
        int slotIndex = rewardMagicConfirmSlotIndex;
        RectTransform sourceRect = rewardMagicConfirmSourceRect;
        HideRewardMagicConfirmPanel(false);
        pendingRewardMagic = null;
        RefreshPlayerAnimationState();
        StartCoroutine(SetRewardMagicAtSlotAnimatedRoutine(rewardMagic, slotIndex, sourceRect));
    }

    private void ConfirmShopMagicPlacement()
    {
        if (pendingShopMagic == null || rewardMagicConfirmSlotIndex < 0)
        {
            HideRewardMagicConfirmPanel(false);
            return;
        }

        Action<int> slotChosen = pendingShopMagicSlotChosen;
        int slotIndex = rewardMagicConfirmSlotIndex;
        HideRewardMagicConfirmPanel(false);
        ClearPendingShopMagic();
        slotChosen?.Invoke(slotIndex);
    }

	    private void CancelRewardMagicPlacementConfirm()
	    {
	        HideRewardMagicConfirmPanel(false);
	    }

    private void CancelShopMagicPlacementConfirm()
    {
        HideRewardMagicConfirmPanel(false);
    }

	    private void HideRewardMagicConfirmPanel(bool clearPendingReward)
    {
        if (clearPendingReward)
        {
            pendingRewardMagic = null;
            RefreshPlayerAnimationState();
        }

        rewardMagicConfirmSlotIndex = -1;
        rewardMagicConfirmSourceRect = null;
        if (rewardMagicConfirmPanel != null)
            rewardMagicConfirmPanel.gameObject.SetActive(false);
    }

    private void EnsureRewardMagicConfirmPanel()
    {
        if (rewardMagicConfirmPanel != null)
            return;

        RectTransform existingPanel = UIManager.FindChildRecursive(transform, "RewardMagicConfirmPanel") as RectTransform;
        if (existingPanel != null)
        {
            rewardMagicConfirmPanel = existingPanel;
            CacheRewardMagicConfirmPanelReferences();
            rewardMagicConfirmPanel.gameObject.SetActive(false);
            return;
        }

        RectTransform parent = transform as RectTransform;
        if (parent == null)
            return;

        TMP_Text overlayBlocker = new GameObject("RewardMagicConfirmPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        rewardMagicConfirmPanel = overlayBlocker.rectTransform;
        rewardMagicConfirmPanel.SetParent(parent, false);
        rewardMagicConfirmPanel.anchorMin = Vector2.zero;
        rewardMagicConfirmPanel.anchorMax = Vector2.one;
        rewardMagicConfirmPanel.offsetMin = Vector2.zero;
        rewardMagicConfirmPanel.offsetMax = Vector2.zero;
        overlayBlocker.text = string.Empty;
        overlayBlocker.color = Color.white;
        overlayBlocker.raycastTarget = true;
        PopupLayerUtility.ApplyTo(rewardMagicConfirmPanel);

        RectTransform window = CreateRewardMagicConfirmWindow(rewardMagicConfirmPanel);
        RectTransform content = GetPopupContent(window);
        CreateRewardMagicConfirmText(content, "Title", "确认替换道具？", 28, FontStyles.Bold, new Vector2(0f, 112f), new Vector2(420f, 42f));
        CreateRewardMagicConfirmText(content, "Hint", "确认后才会覆盖；取消后可以重新选择道具槽。", 16, FontStyles.Normal, new Vector2(0f, 76f), new Vector2(560f, 28f));
        CreateRewardMagicConfirmText(content, "ExistingLabel", "已有道具", 18, FontStyles.Bold, new Vector2(-160f, 36f), new Vector2(160f, 28f));
        CreateRewardMagicConfirmText(content, "NewLabel", "新道具", 18, FontStyles.Bold, new Vector2(160f, 36f), new Vector2(160f, 28f));
        Vector2 cellSize = GetRewardMagicConfirmCellSize();
        rewardMagicConfirmExistingRoot = CreateRewardMagicConfirmRoot(content, "ExistingMagic", new Vector2(-160f, -34f), cellSize);
        rewardMagicConfirmNewRoot = CreateRewardMagicConfirmRoot(content, "NewMagic", new Vector2(160f, -34f), cellSize);
        rewardMagicConfirmCancelButton = CreateRewardMagicConfirmButton(content, "CancelButton", "取消", new Vector2(-90f, -130f), new Vector2(130f, 44f), new Color(0.09f, 0.09f, 0.14f, 1f));
        rewardMagicConfirmButton = CreateRewardMagicConfirmButton(content, "ConfirmButton", "确认", new Vector2(90f, -130f), new Vector2(130f, 44f), new Color(0.1f, 0.95f, 0.25f, 1f));
        rewardMagicConfirmPanel.gameObject.SetActive(false);
    }

    private void CacheRewardMagicConfirmPanelReferences()
    {
        rewardMagicConfirmExistingRoot = UIManager.FindChildRecursive(rewardMagicConfirmPanel, "ExistingMagic") as RectTransform;
        rewardMagicConfirmNewRoot = UIManager.FindChildRecursive(rewardMagicConfirmPanel, "NewMagic") as RectTransform;
        rewardMagicConfirmButton = GetRewardMagicConfirmButton("ConfirmButton");
        rewardMagicConfirmCancelButton = GetRewardMagicConfirmButton("CancelButton");
        ApplyRewardMagicConfirmButtonColor(rewardMagicConfirmButton, new Color(0.1f, 0.95f, 0.25f, 1f));
        ApplyRewardMagicConfirmButtonColor(rewardMagicConfirmCancelButton, new Color(0.09f, 0.09f, 0.14f, 1f));
    }

    private Button GetRewardMagicConfirmButton(string name)
    {
        Transform child = UIManager.FindChildRecursive(rewardMagicConfirmPanel, name);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static void ApplyRewardMagicConfirmButtonColor(Button button, Color color)
    {
        if (button == null)
            return;

        Graphic targetGraphic = button.targetGraphic;
        if (targetGraphic != null)
            targetGraphic.color = color;
    }

    private RectTransform CreateRewardMagicConfirmWindow(RectTransform parent)
    {
        RectTransform prefab = GetPopupDragonWindowBlankPrefab();
        RectTransform window;
        if (prefab != null)
        {
            window = Object.Instantiate(prefab, parent);
            window.name = "PopupDragonWindowBlank";
        }
        else
        {
            Image image = new GameObject("PopupDragonWindowBlank", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.color = new Color(0.02f, 0.02f, 0.04f, 1f);
            image.raycastTarget = true;
            window = image.rectTransform;
            window.SetParent(parent, false);
        }

        window.anchorMin = new Vector2(0.5f, 0.5f);
        window.anchorMax = new Vector2(0.5f, 0.5f);
        window.pivot = new Vector2(0.5f, 0.5f);
        window.anchoredPosition = Vector2.zero;
        window.sizeDelta = new Vector2(680f, 380f);
        window.localScale = Vector3.one;
        window.SetAsLastSibling();
        return window;
    }

    private RectTransform GetPopupContent(RectTransform window)
    {
        Transform contentTransform = UIManager.FindChildRecursive(window, "Content");
        RectTransform content = contentTransform as RectTransform;
        if (content != null)
            return content;
        return window;
    }

    private RectTransform GetPopupDragonWindowBlankPrefab()
    {
        PrefabReferenceLibrary library = GetComponentInParent<PrefabReferenceLibrary>();
        return library != null ? library.PopupDragonWindowBlankPrefab : null;
    }

    private TMP_Text CreateRewardMagicConfirmText(RectTransform parent, string name, string text, int fontSize, FontStyles fontStyle, Vector2 anchoredPosition, Vector2 size)
    {
        TMP_Text label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.transform.SetParent(parent, false);
        label.font = GetDefaultFont();
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = TextAlignmentOptions.Center;
        label.color = fontStyle == FontStyles.Bold ? new Color(1f, 0.9f, 0.55f, 1f) : new Color(0.86f, 0.88f, 0.94f, 1f);
        label.text = text;
        label.raycastTarget = false;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return label;
    }

    private RectTransform CreateRewardMagicConfirmRoot(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = anchoredPosition;
        root.sizeDelta = size;
        return root;
    }

    private Vector2 GetRewardMagicConfirmCellSize()
    {
        return new Vector2(Mathf.Max(1f, rewardMagicConfirmCellSize.x), Mathf.Max(1f, rewardMagicConfirmCellSize.y));
    }

    private Button CreateRewardMagicConfirmButton(RectTransform parent, string name, string text, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(JuicyMotion)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        TMP_Text label = CreateRewardMagicConfirmText(rect, "Text", text, 18, FontStyles.Bold, Vector2.zero, size);
        label.color = Color.white;
        return image.GetComponent<Button>();
    }

    private void BindRewardMagicConfirmView(RectTransform root, MagicModel magic)
    {
        if (root == null)
            return;

        Vector2 cellSize = GetRewardMagicConfirmCellSize();
        root.sizeDelta = cellSize;
        if (magicViewPrefab == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
            Object.Destroy(root.GetChild(i).gameObject);

        RectTransform viewRect = Object.Instantiate(magicViewPrefab, root);
        viewRect.gameObject.SetActive(true);
        viewRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewRect.pivot = new Vector2(0.5f, 0.5f);
        viewRect.anchoredPosition = Vector2.zero;
        viewRect.sizeDelta = cellSize;
        MagicItemView view = viewRect.GetComponent<MagicItemView>();
        if (view != null)
            view.Bind(magic);
        UIManager.RemoveJuicyMotion(viewRect.transform);
    }

    public void SelectPendingMagicModifier(MagicModifierData modifierData)
    {
        pendingMagicModifier = modifierData;
        RefreshPlayerAnimationState();
    }

	public bool HasPendingRewardMagic => pendingRewardMagic != null;

    public bool HasPendingShopMagic => pendingShopMagic != null;

    public bool HasPendingMagicModifier => pendingMagicModifier != null;

    public bool HasPendingMaterialModifier => pendingMaterialModifier != null;

    public bool TryApplyPendingMagicModifier(int slotIndex)
    {
        if (pendingMagicModifier == null)
            return false;

        MagicModel magic = playerState.GetMagicAtSlot(slotIndex);
        MagicModifierSelectionPanelUI panel = GetUIManager().MagicModifierSelectionPanel;
        if (magic == null)
        {
            panel?.ShowPopup(LocalizationSystem.GetText("ui.magic_modifier.empty_slot", "这个道具槽是空的！"));
            return false;
        }

        MagicModifierModel modifier = MagicModifierFactory.Create(pendingMagicModifier);
        if (modifier == null || !magic.AddModifier(modifier))
        {
            panel?.ShowPopup(LocalizationSystem.GetText("ui.magic_modifier.not_applicable", "这个强化不能用于该道具！"));
            return false;
        }

        pendingMagicModifier = null;
        RefreshPlayerAnimationState();
        CreateMagicViews();
        panel?.CompleteSelection();
        return true;
    }

    public bool TryApplyPendingMaterialModifierToSelectedHandCard(int handCardIndex)
    {
        if (pendingMaterialModifier == null || playerState == null)
            return false;

        if (handCardIndex < 0 || handCardIndex >= playerState.Hand.Count)
            return false;

        MaterialModel target = playerState.Hand[handCardIndex];
        bool applied = TryApplyPendingMaterialModifier(target);
        if (applied)
            GetUIManager().MagicModifierSelectionPanel?.CompleteSelection();
        return applied;
    }

	public void ShowSlotSelect(MagicData rewardMagic)
	{
		SelectPendingRewardMagic(rewardMagic);
	}

	public void SetRewardMagicAtSlot(MagicData rewardMagic, int slotIndex)
	{
		if (rewardMagic == null)
			return;

        HideRewardMagicConfirmPanel(false);
        undoRewardMagicSlotIndex = slotIndex;
        undoRewardPreviousMagic = playerState.GetMagicAtSlot(slotIndex);
        undoRewardAvailable = true;
		playerState.SetMagicAtSlot(MagicFactory.Create(rewardMagic, slotIndex), slotIndex);
		CreateMagicViews();
		GetUIManager().RewardPanel?.CompleteMagicRewardSelection();
		TutorialManager?.OnRewardMagicEquipped(playerState, mapNodes, currentMapNodeIndex, activeChapter ?? GetActiveChapter(), currentLevel);
	}

    private bool TryUndoRewardMagicClaim()
    {
        if (!undoRewardAvailable || currentLevel == null || GetUIManager().RewardPanel == null || undoRewardMagicSlotIndex < 0)
            return false;

        if (undoRewardPreviousMagic != null)
            playerState.SetMagicAtSlot(undoRewardPreviousMagic, undoRewardMagicSlotIndex);
        else
            playerState.ClearMagicSlot(undoRewardMagicSlotIndex);
        undoRewardMagicSlotIndex = -1;
        undoRewardPreviousMagic = null;
        undoRewardAvailable = false;
        CreateMagicViews();
        GetUIManager().RewardPanel.UndoMagicRewardClaim();
        RefreshStaticUI();
        SaveRunProgress();
        return true;
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

    public void ApplyRewardHeal(int amount)
    {
        if (amount <= 0 || playerState == null)
            return;

        int healthBefore = playerState.CurrentHealth;
        playerState.Heal(amount);
        int healed = playerState.CurrentHealth - healthBefore;
        if (healed > 0)
        {
            PlayPlayerCornerFeedback(new Color(0.1f, 0.95f, 0.25f, 0.48f));
            ShowPlayerFloatingText("+" + healed, FloatingTextType.Heal);
        }
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
        AddShopMaterialAnimated(material, null, sourceRect, onComplete);
    }

    public void AddShopMaterialAnimated(MaterialEnum material, MaterialModifierData modifierData, RectTransform sourceRect, Action onComplete)
    {
        StartCoroutine(AddShopMaterialAnimatedRoutine(material, modifierData, sourceRect, onComplete));
    }

    private IEnumerator AddShopMaterialAnimatedRoutine(MaterialEnum material, MaterialModifierData modifierData, RectTransform sourceRect, Action onComplete)
    {
        yield return PlayMaterialAcquireAnimation(material, sourceRect);
        AddShopMaterial(material, modifierData);
        onComplete?.Invoke();
    }

    private IEnumerator SetRewardMagicAtSlotAnimatedRoutine(MagicData rewardMagic, int slotIndex, RectTransform sourceRect)
    {
        yield return PlayMagicAcquireAnimation(rewardMagic, slotIndex, sourceRect);
        SetRewardMagicAtSlot(rewardMagic, slotIndex);
    }

    public void AddShopMaterial(MaterialEnum material)
    {
        AddShopMaterial(material, null);
    }

    public void AddShopMaterial(MaterialEnum material, MaterialModifierData modifierData)
    {
        if (material == MaterialEnum.None)
            return;

        playerState.AddDeckMaterial(material, MaterialModifierFactory.Create(modifierData));
        RefreshMaterialListPanel();
        RefreshStaticUI();
        SaveRunProgress();
    }

    public int CountShopMaterialModifierTargets()
    {
        if (playerState == null)
            return 0;

        int count = 0;
        for (int i = 0; i < playerState.Deck.Count; i++)
        {
            if (CanSelectDisabledMaterialForNonBattleAction(playerState.Deck[i]))
                count++;
        }
        return count;
    }

    public bool IsShopMaterialModifierTargetSelectable(MaterialModel materialModel)
    {
        return CanSelectDisabledMaterialForNonBattleAction(materialModel);
    }

    public bool ApplyShopMaterialModifier(MaterialModel target, MaterialModifierData modifierData)
    {
        if (!IsShopMaterialModifierTargetSelectable(target))
            return false;

        MaterialModifierModel modifier = MaterialModifierFactory.Create(modifierData);
        if (modifier == null)
            return false;

        target.AddModifier(modifier);
        RefreshMaterialListPanel();
        RebuildCards(animateFromCurrent: true);
        RefreshStaticUI();
        SaveRunProgress();
        return true;
    }

    public void RefreshShopUndoUI()
    {
        RefreshMaterialListPanel();
        RebuildCards(animateFromCurrent: true);
        RefreshStaticUI();
        SaveRunProgress();
    }

    public void CreateMagicViewsForShopUndo()
    {
        CreateMagicViews();
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
        bool finishedBossMapLevel = currentChapterMapBossLevel;
        HideRewardMagicConfirmPanel(false);
        undoRewardAvailable = false;
        undoRewardMagicSlotIndex = -1;
        undoRewardPreviousMagic = null;
		pendingRewardMagic = null;
        pendingShopMagic = null;
        pendingShopMagicSlotChosen = null;
        pendingMagicModifier = null;
        pendingMaterialModifier = null;
        playerState.ClearCombatState();
        ClearOrphanedCardViews();
		GetUIManager().HideRewardPanel();
        GetUIManager().HideShopPanel();
		GetUIManager().RewardGridPanel?.Hide();
		GetUIManager().HideSlotSelect();
        GetUIManager().MagicModifierSelectionPanel?.Hide();
		GetUIManager().MaterialSelectionPanel?.EndSelectionMode();

		enemyModels.Clear();
		battleManager.ClearEnemies();
		enemyViewStates.Clear();
		currentEvent = null;
		currentLevel = null;
        currentChapterMapBossLevel = false;
        eliteMagicModifierRewardResolved = false;
		runManager?.ClearCurrentLevel();
		GameLog.Data($"Finish reward node={currentMapNodeIndex + 1}/{mapNodes.Count}");
        if (ChapterMapGrid != null && ChapterMapGrid.CellCount > 0)
        {
            if (finishedBossMapLevel)
            {
                ShowVictoryPanel();
                return;
            }

            currentMapNodeIndex++;
            runManager?.SetCurrentMapNodeIndex(currentMapNodeIndex);
            RefreshChapterProgressUI();
            SaveRunProgress();
            ShowLevelSelect();
            return;
        }
		if (mapNodes.Count != 0)
		{
			int num = currentMapNodeIndex;
            runManager?.AdvanceMapNode();
            currentMapNodeIndex = runManager != null ? runManager.CurrentMapNodeIndex : currentMapNodeIndex + 1;
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

    public RewardOptionKind RollEliteExtraRewardKind()
    {
        if (!ShouldShowEliteExtraReward())
            return RewardOptionKind.None;

        bool canClaimMagicModifier = HasAnyMagicModifierChoice();
        bool canClaimArrowModifier = HasAnyArrowModifierChoice();
        if (canClaimMagicModifier && canClaimArrowModifier)
            return NextRunRandomInt(0, 2) == 0 ? RewardOptionKind.MagicModifier : RewardOptionKind.ArrowModifier;
        if (canClaimArrowModifier)
            return RewardOptionKind.ArrowModifier;
        return canClaimMagicModifier ? RewardOptionKind.MagicModifier : RewardOptionKind.None;
    }

    public bool CanClaimEliteMagicModifierReward()
    {
        return ShouldShowEliteExtraReward() && HasAnyMagicModifierChoice();
    }

    public void ClaimEliteMagicModifierReward(Action completed)
    {
        if (!ShouldShowEliteExtraReward())
        {
            completed?.Invoke();
            return;
        }

        eliteMagicModifierRewardResolved = true;
        List<MagicModifierData> choices = GetMagicModifierChoices(1);
        if (choices.Count == 0)
        {
            completed?.Invoke();
            return;
        }

        ShowMagicModifierSelection(choices, delegate
        {
            RefreshStaticUI();
            SaveRunProgress();
            completed?.Invoke();
        });
    }

    public void ClaimEliteArrowModifierReward(Action completed)
    {
        if (!ShouldShowEliteExtraReward())
        {
            completed?.Invoke();
            return;
        }

        eliteMagicModifierRewardResolved = true;
        List<MaterialModifierData> choices = GetArrowModifierChoices(3);
        if (choices.Count == 0)
        {
            completed?.Invoke();
            return;
        }

        ShowArrowModifierRewardSelection(choices, delegate
        {
            RefreshStaticUI();
            SaveRunProgress();
            completed?.Invoke();
        });
    }

    private IEnumerator ShowEventMaterialModifierRoutine(string modifierId)
    {
        MaterialModifierData data = GetMaterialModifierDataById(modifierId);
        if (data == null || CountSelectableArrowModifierTargets() == 0)
            yield break;

        bool completed = false;
        ShowArrowModifierRewardSelection(new List<MaterialModifierData> { data }, delegate { completed = true; });
        while (!completed)
            yield return null;
    }

    private MaterialModifierData GetMaterialModifierDataById(string modifierId)
    {
        if (string.IsNullOrEmpty(modifierId))
            return null;

        DataTable<MaterialModifierData> table = GameDataReader.LoadTable<MaterialModifierData>("MaterialModifierData");
        for (int i = 0; table != null && table.items != null && i < table.items.Count; i++)
        {
            MaterialModifierData data = table.items[i];
            if (data != null && data.id == modifierId)
                return data;
        }
        return null;
    }

    private bool ShouldShowEliteExtraReward()
    {
        return !eliteMagicModifierRewardResolved && currentLevel != null && currentLevel.levelType == LevelType.Elite && (HasAnyMagicModifierChoice() || HasAnyArrowModifierChoice());
    }

    private bool ShouldShowEliteMagicModifierReward()
    {
        return ShouldShowEliteExtraReward() && HasAnyMagicModifierChoice();
    }

    private bool HasAnyMagicModifierChoice()
    {
        foreach (MagicModifierData data in GameDataDatabase.MagicModifierData.Values)
        {
            if (data != null && data.weight != 0 && CanAnyMagicAcceptModifier(data))
                return true;
        }
        return false;
    }

    private bool HasAnyArrowModifierChoice()
    {
        if (playerState == null || CountSelectableArrowModifierTargets() == 0)
            return false;

        DataTable<MaterialModifierData> table = GameDataReader.LoadTable<MaterialModifierData>("MaterialModifierData");
        for (int i = 0; table != null && table.items != null && i < table.items.Count; i++)
        {
            if (IsEliteArrowModifierRewardData(table.items[i]))
                return true;
        }
        return false;
    }

    private List<MaterialModifierData> GetArrowModifierChoices(int count)
    {
        DataTable<MaterialModifierData> table = GameDataReader.LoadTable<MaterialModifierData>("MaterialModifierData");
        List<MaterialModifierData> pool = new List<MaterialModifierData>();
        for (int i = 0; table != null && table.items != null && i < table.items.Count; i++)
        {
            MaterialModifierData data = table.items[i];
            if (IsEliteArrowModifierRewardData(data))
                pool.Add(data);
        }

        List<MaterialModifierData> result = new List<MaterialModifierData>();
        int attempts = 0;
        while (result.Count < count && pool.Count > 0 && attempts < 40)
        {
            attempts++;
            MaterialModifierData data = pool[NextRunRandomInt(0, pool.Count)];
            if (!result.Contains(data))
                result.Add(data);
        }
        return result;
    }

    private bool IsEliteArrowModifierRewardData(MaterialModifierData data)
    {
        return data != null && data.inArrowModifierRewardPool && !string.IsNullOrEmpty(data.script) && MaterialModifierFactory.Create(data) != null;
    }

    private int CountSelectableArrowModifierTargets()
    {
        if (playerState == null)
            return 0;

        int count = 0;
        for (int i = 0; i < playerState.Deck.Count; i++)
        {
            if (IsArrowModifierTargetSelectable(playerState.Deck[i]))
                count++;
        }
        return count;
    }

    private bool IsArrowModifierTargetSelectable(MaterialModel materialModel)
    {
        return materialModel != null && playerState != null && playerState.Deck.Contains(materialModel) && materialModel.material != MaterialEnum.None;
    }

    private void ShowArrowModifierRewardSelection(IReadOnlyList<MaterialModifierData> choices, Action completed)
    {
        busy = true;
        SetButtonsInteractable(interactable: false);
        pendingMaterialModifier = null;
        MagicModifierSelectionPanelUI panel = GetUIManager().MagicModifierSelectionPanel;
        if (panel != null)
            panel.ShowMaterialModifierChoices(choices, selected => StartArrowModifierTargetSelection(selected, completed), completed);
        else
            completed?.Invoke();
    }

    private void StartArrowModifierTargetSelection(MaterialModifierData selectedModifier, Action completed)
    {
        if (selectedModifier == null)
            return;

        pendingMaterialModifier = selectedModifier;
        MagicModifierSelectionPanelUI modifierPanel = GetUIManager().MagicModifierSelectionPanel;
        modifierPanel?.Hide();
        RefreshMaterialListPanel();
        RebuildCards(animateFromCurrent: true);
        RefreshPlayerAnimationState();
        busy = false;
        SetButtonsInteractable(interactable: true);

        MaterialListPanelUI materialListPanel = GetUIManager().MaterialSelectionPanel;
        if ((Object)materialListPanel == (Object)null)
        {
            pendingMaterialModifier = null;
            completed?.Invoke();
            return;
        }

        materialListPanel.BeginSelection(1, IsArrowModifierTargetSelectable, selectedMaterials =>
        {
            MaterialModel target = selectedMaterials != null && selectedMaterials.Count > 0 ? selectedMaterials[0] : null;
            if (TryApplyPendingMaterialModifier(target))
                GetUIManager().MagicModifierSelectionPanel?.CompleteSelection();
        }, null, "选择要附魔的箭头");
    }

    private bool TryApplyPendingMaterialModifier(MaterialModel target)
    {
        if (pendingMaterialModifier == null || !IsArrowModifierTargetSelectable(target))
            return false;

        MaterialModifierModel modifier = MaterialModifierFactory.Create(pendingMaterialModifier);
        if (modifier == null)
            return false;

        target.AddModifier(modifier);
        pendingMaterialModifier = null;
        RefreshMaterialListPanel();
        RebuildCards(animateFromCurrent: true);
        RefreshStaticUI();
        SaveRunProgress();
        return true;
    }

    private IEnumerator ShowEliteMagicModifierRewardRoutine()
    {
        List<MagicModifierData> choices = GetMagicModifierChoices(1);
        if (choices.Count == 0)
        {
            FinishReward();
            yield break;
        }

        GetUIManager().HideRewardPanel();
        GetUIManager().HideSlotSelect();
        bool completed = false;
        ShowMagicModifierSelection(choices, delegate { completed = true; });
        while (!completed)
            yield return null;

        RefreshStaticUI();
        SaveRunProgress();
        FinishReward();
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
        battleManager?.BeginEnemyAction(enemy);

		for (int i = 0; i < enemy.CurrentIntents.Count; i++)
		{
			EnemyIntentData intent = enemy.CurrentIntents[i];
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
			int hitCount = enemy.GetIntentHitCount(intent);
			for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
			{
				yield return PlayEnemyIntentPerformance(state, intent);
				BattleActionResult intentResult = hitCount > 1
					? (battleManager != null ? battleManager.ResolveEnemyIntentHitAt(enemy, i, hitIndex) : null)
					: (battleManager != null ? battleManager.ResolveEnemyIntentAt(enemy, i) : null);
				PlayPlayerDamageFeedbackIfNeeded(intentResult != null ? intentResult.PlayerHealthBefore : playerState.CurrentHealth, intentResult != null ? intentResult.PlayerShieldBefore : playerState.Shield);
				PlayEnemyShieldFeedbackIfNeeded(state, enemy, intentResult != null ? intentResult.EnemyShieldBefore : enemy.Shield);
				RefreshStaticUI();
				RefreshEnemyUI(state, false);
				if (hitIndex + 1 < hitCount)
					yield return new WaitForSeconds(enemy.GetIntentHitInterval(intent));
			}

			Tween fadeOut = intentView != null ? intentView.PlayFadeOut(enemyIntentFadeOutDuration) : null;
			if (fadeOut != null)
				yield return fadeOut.WaitForCompletion();
			yield return new WaitForSeconds(enemyIntentBetweenDelay);
		}

        battleManager?.EndEnemyAction(enemy);
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

		if (intent.actionType == EnemyActionType.Attack || intent.actionType == EnemyActionType.AttackAll)
		{
			yield return PlayEnemyAttackPerformance(state);
		}
		else if (intent.actionType == EnemyActionType.GainShield)
		{
			yield return PlayEnemyDefendPerformance(state);
		}
	}

	private IEnumerator PlayEnemyAttackPerformance(EnemyViewState state)
	{
		if (state.bodyImage == null)
			yield break;

		RectTransform body = (Object)state.motionRoot != (Object)null ? state.motionRoot : state.bodyImage.rectTransform;
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

		RectTransform parent = (Object)state.motionRoot != (Object)null ? state.motionRoot : state.viewRect;
		Image icon = new GameObject("DefendBurst", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
		icon.transform.SetParent(parent, false);
		icon.sprite = Resources.Load<Sprite>("Images/Intent/defend");
		icon.color = new Color(0.35f, 0.65f, 1f, 1f);
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
		return GetEnemyCastTargetRect(FindEnemyViewState(firstAliveEnemy));
	}

    private RectTransform GetEnemyCastTargetRect(EnemyViewState state)
    {
        if (state == null)
            return null;
        if ((Object)state.viewUI != (Object)null && (Object)state.viewUI.BodyRoot != (Object)null)
            return state.viewUI.BodyRoot;
        if ((Object)state.motionRoot != (Object)null)
            return state.motionRoot;
        return state.viewRect;
    }

    private void CollectAliveEnemyTargetRects(List<RectTransform> results)
    {
        if (results == null)
            return;

        results.Clear();
        for (int i = 0; i < enemyViewStates.Count; i++)
        {
            EnemyViewState state = enemyViewStates[i];
            RectTransform targetRect = GetEnemyCastTargetRect(state);
            if (state != null && state.model != null && !state.model.IsDead && (Object)targetRect != (Object)null)
                results.Add(targetRect);
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

        ChapterData chapter = activeChapter ?? GetActiveChapter();
        bool tutorialVictory = victory && chapter != null && chapter.numericId == TutorialManagerUI.TutorialChapterNumericId;
        float playSeconds = GetCurrentRunPlaySeconds();
        List<string> magicNames = victory ? GetVictoryMagicNames() : null;
        RunSaveSystem.RecordRunEndAndClearCurrentRun(victory ? RunHistoryResultType.Victory : RunHistoryResultType.Defeat, playerState, mapNodes, currentMapNodeIndex, chapter, currentLevel, playSeconds);
		runEnded = true;
		busy = true;
		SetButtonsInteractable(interactable: false);
		ResetMagicHighlights();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameplayMusic();
		GetUIManager().HideLevelSelect();
		GetUIManager().HideMapPanel();
        HideRewardMagicConfirmPanel(false);
		GetUIManager().HideRewardPanel();
		GetUIManager().RewardGridPanel?.Hide();
		GetUIManager().HideSlotSelect();
		ResetContinuousCastCounterUI();
		if (victory)
            GetUIManager().ShowVictoryPanel(playSeconds, magicNames, tutorialVictory);
		else
			GetUIManager().ShowDefeatPanel(playerState?.LastDamageSourceEnemy?.Name);
	}

    public void SaveCurrentRunAndReturnToStart(string startSceneName)
    {
        SaveRunProgress();
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithTransition(startSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(startSceneName);
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

		for (int i = 0; i < magicViews.Count; i++)
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

	private bool HasActingEnemyAfter(int index)
	{
        return battleManager != null && battleManager.HasActingEnemyAfter(index);
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

		int shardColumns = Mathf.Max(1, enemyDeathShardColumns);
		int shardRows = Mathf.Max(1, enemyDeathShardRows);
		int shardCount = shardColumns * shardRows;
		RectTransform[] shardRects = new RectTransform[shardCount];
		Material[] shardMaterials = new Material[shardCount];
		Vector2[] startPositions = new Vector2[shardCount];
		Vector2[] targetPositions = new Vector2[shardCount];
		float[] startRotations = new float[shardCount];
		float[] targetRotations = new float[shardCount];
		Vector3[] startScales = new Vector3[shardCount];
		Vector3[] targetScales = new Vector3[shardCount];
		Vector2 shardSize = new Vector2(bodySize.x / shardColumns, bodySize.y / shardRows);
		Vector2 sourceAnchoredPosition = sourceRect.anchoredPosition;
		Vector3 sourceScale = ((Transform)sourceRect).localScale;
		float sourceRotation = ((Transform)sourceRect).localEulerAngles.z;
		int sourceSiblingIndex = ((Transform)sourceRect).GetSiblingIndex();
		int index = 0;

		for (int row = 0; row < shardRows; row++)
		{
			for (int column = 0; column < shardColumns; column++)
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

				float normalizedX = (column + 0.5f) / shardColumns;
				float normalizedY = (row + 0.5f) / shardRows;
				Vector2 offset = new Vector2(normalizedX * bodySize.x - sourceRect.pivot.x * bodySize.x, normalizedY * bodySize.y - sourceRect.pivot.y * bodySize.y);
				Vector2 startPosition = sourceAnchoredPosition + offset;
				shardRect.anchoredPosition = startPosition;

				Material shardMaterial = new Material(enemyDeathExplosionMaterialTemplate);
				shardMaterial.SetFloat("_Explosion", 0f);
				shardMaterial.SetFloat("_ShardIndex", index);
				shardMaterial.SetVector("_ShardRect", new Vector4((float)column / shardColumns, (float)row / shardRows, 1f / shardColumns, 1f / shardRows));
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
				float explosionDistance = Mathf.Max(0f, enemyDeathExplosionDistance);
				float minDistance = Mathf.Min(explosionDistance, explosionDistance * Mathf.Max(0f, enemyDeathExplosionDistanceMinMultiplier));
				Vector2 scatter = direction * Random.Range(minDistance, explosionDistance);
				scatter += Random.insideUnitCircle * Mathf.Max(0f, enemyDeathExplosionRandomness);
				float minScale = Mathf.Min(enemyDeathShardScaleRange.x, enemyDeathShardScaleRange.y);
				float maxScale = Mathf.Max(enemyDeathShardScaleRange.x, enemyDeathShardScaleRange.y);
				float rotationRange = Mathf.Max(0f, enemyDeathShardRotationRange);

				shardRects[index] = shardRect;
				shardMaterials[index] = shardMaterial;
				startPositions[index] = startPosition;
				targetPositions[index] = startPosition + scatter;
				startRotations[index] = sourceRotation;
				targetRotations[index] = sourceRotation + Random.Range(-rotationRange, rotationRange);
				startScales[index] = sourceScale;
				targetScales[index] = sourceScale * Random.Range(minScale, maxScale);
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
			return GetEnemyCastTargetRect(FindEnemyViewState(targetEnemy)) ?? GetAliveEnemyTargetRect();
		}
		return GetUIManager().PlayerFeedback?.PlayerVirtualTarget;
	}

    private bool MagicTargetsAllEnemiesForCast(MagicModel magic)
    {
        return magic != null && (magic.CastParticleTargetsAllEnemies || (playerState != null && playerState.GetBuffStack(BuffEnum.MagicAttackAll) > 0 && magic.EffectType == MagicEffectType.Damage));
    }

	private SpellEffectTarget GetMagicEffectTargetType(MagicModel magic)
	{
		MagicEffectType effectType = magic.EffectType;
		if (magic.CastParticleTargetsPlayer || effectType == MagicEffectType.GainShield || effectType == MagicEffectType.Heal || effectType == MagicEffectType.DrawNextTurn)
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
			if (result.enemyDamageHits.Count > 0)
				StartCoroutine(PlayMagicDamageFeedbackRoutine(result.enemyDamageHits));
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

	private IEnumerator PlayMagicDamageFeedbackRoutine(IReadOnlyList<MagicDamageHitResult> hits)
	{
		int lastStepIndex = -1;
		for (int i = 0; hits != null && i < hits.Count; i++)
		{
			MagicDamageHitResult hit = hits[i];
			if (hit == null)
				continue;

			if (lastStepIndex >= 0 && hit.stepIndex != lastStepIndex)
				yield return new WaitForSeconds(magicDamageHitInterval);

			PlayEnemyDamageFeedback(hit);
			lastStepIndex = hit.stepIndex;
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

	private void ShowFloatingText(RectTransform anchor, string text, FloatingTextType type, bool blocked = false, float durationMultiplier = 1f)
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
		floatingText.Play(text, type, blocked, floatingTextYOffset, floatingTextDuration * durationMultiplier, floatingTextMoveEase, floatingTextFadeEase);
	}

	private void ShowPlayerFloatingText(string text, FloatingTextType type, bool blocked = false)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		ShowFloatingText(GetUIManager().PlayerFeedback?.PlayerFloatingTextTarget, text, type, blocked, 2f);
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
		if ((Object)state.nameText != (Object)null)
		{
			state.nameText.text = state.model.Name;
		}
		else
		{
			TMP_Text[] componentsInChildren = ((Component)state.viewRect).GetComponentsInChildren<TMP_Text>(true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (((Object)componentsInChildren[i]).name == "NameText")
				{
					componentsInChildren[i].text = state.model.Name;
					state.nameText = componentsInChildren[i];
				}
			}
		}
		if ((Object)state.intentIcon != (Object)null)
		{
			state.intentIcon.color = Color.white;
		}
		UpdateHealthBar(state.healthFill, state.healthBufferFill, state.shieldFill, state.model.CurrentHealth, state.model.Data.maxHealth, state.model.Shield, instant);
		RefreshBuffRoot(state.buffRoot, state.model.Buffs, null);
		if ((Object)state.focusMarker != (Object)null)
		{
			((Component)state.focusMarker).gameObject.SetActive(battleManager != null && battleManager.FocusTarget == state.model);
		}
		if (!suppressEnemyIntentRefresh)
			RebuildEnemyIntentViews(state);
        if ((Object)state.viewUI != (Object)null)
            state.viewUI.ApplyDataLayout(state.model.Data);
		Tween healthNumberTween = state.healthNumberTween;
		if (healthNumberTween != null)
		{
			TweenExtensions.Kill(healthNumberTween, false);
		}
			HealthBarUI.SetHealthTextColor(state.healthText, false);
				state.healthNumberTween = UpdateHealthText(state.healthText, state.shieldText, state.displayedHealth, state.model.CurrentHealth, state.model.Data.maxHealth, state.model.Shield, instant, delegate(int healthValue)

			{
				state.displayedHealth = healthValue;
			});

	}

	private void RebuildEnemyIntentViews(EnemyViewState state)
	{
		if (state == null || state.viewRect == null || state.model == null)
			return;

		RectTransform root = (Object)state.intentRoot != (Object)null ? state.intentRoot : EnsureIntentRoot(state.viewRect);
        state.intentRoot = root;
		IReadOnlyList<EnemyIntentData> intents = state.model.CurrentIntents;
		while (state.intentViews.Count < intents.Count)
			state.intentViews.Add(CreateEnemyIntentView(root));

		float intentSpacing = 8f;
        float intentY = (Object)state.viewUI != (Object)null ? 0f : 38f;
		int totalIntentCount = GetTotalVisibleIntentCount();
		int phaseStart = GetIntentPhaseStartIndex(state.model);
        float totalWidth = 0f;
		for (int i = 0; i < state.intentViews.Count; i++)
		{
			EnemyIntentView view = state.intentViews[i];
			bool visible = i < intents.Count;
			view.gameObject.SetActive(visible);
			if (!visible)
				continue;

            view.Bind(this, state.model, intents[i], playerState, phaseStart + i, totalIntentCount);
            totalWidth += view.LayoutWidth;
            if (i < intents.Count - 1)
                totalWidth += intentSpacing;
		}

        float cursor = -totalWidth * 0.5f;
		for (int i = 0; i < intents.Count; i++)
		{
            EnemyIntentView view = state.intentViews[i];
            float width = view.LayoutWidth;
            view.SetBaseAnchoredPosition(new Vector2(cursor + width * 0.5f, intentY));
            cursor += width + intentSpacing;
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
        if ((Object)enemyIntentViewPrefab != (Object)null)
        {
            RectTransform prefabInstance = Object.Instantiate<RectTransform>(enemyIntentViewPrefab, (Transform)parent, false);
            prefabInstance.gameObject.SetActive(true);
            EnemyIntentView prefabView = ((Component)prefabInstance).GetComponent<EnemyIntentView>();
            if ((Object)prefabView != (Object)null)
                return prefabView;
            Object.Destroy((Object)((Component)prefabInstance).gameObject);
        }

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
			int value = model != null ? model.GetIntentAttackValue(intent, playerState) : intent.value;
			int times = intent.times > 0 ? intent.times : 1;
			return times > 1 ? value + "x" + times : value.ToString();
		}
		if (intent.actionType == EnemyActionType.GainShield)
		{
			return (model != null ? model.GetIntentShieldValue(intent) : intent.value).ToString();
		}
		if (intent.actionType == EnemyActionType.Summon)
		{
			return intent.summonCount > 1 ? "×" + intent.summonCount : string.Empty;
		}
		if (intent.actionType == EnemyActionType.Special)
		{
			return model != null ? model.GetSpecialIntentDisplayValue(intent, playerState) : string.Empty;
		}
		if (intent.actionType == EnemyActionType.ApplyBuff || intent.actionType == EnemyActionType.ApplyDebuff || intent.actionType == EnemyActionType.Stunned)
		{
			return string.Empty;
		}
		return string.Empty;
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
        EnemyIntentData intent = currentIntents[0];
        switch (intent.actionType)
        {
            case EnemyActionType.Attack:
            case EnemyActionType.AttackAll:
                return new Color(0.95f, 0.18f, 0.14f, 1f);
            case EnemyActionType.GainShield:
                return new Color(0.25f, 0.55f, 1f, 1f);
            case EnemyActionType.ApplyBuff:
                return new Color(0.75f, 0.35f, 1f, 1f);
            case EnemyActionType.ApplyDebuff:
                return new Color(0.25f, 0.8f, 0.35f, 1f);
            case EnemyActionType.Summon:
                return new Color(1f, 0.62f, 0.16f, 1f);
            case EnemyActionType.Special:
                return new Color(0.92f, 0.82f, 0.28f, 1f);
            case EnemyActionType.Stunned:
                return new Color(0.55f, 0.55f, 0.6f, 1f);
            default:
                return Color.gray;
        }
	}

    private int NextRunRandomInt(int minInclusive, int maxExclusive)
    {
        return runManager != null ? runManager.NextRandomInt(minInclusive, maxExclusive) : Random.Range(minInclusive, maxExclusive);
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

	private void MoveIndicatorToReadStep(List<MaterialModel> cards, ArrowReadStep step, bool instant)
	{
		if (step == null)
			return;

		MoveIndicatorToSourceCard(cards, step.SourceCardIndex, step.SourceCard, instant);
	}

	private void MoveIndicatorToTokenRange(List<MaterialModel> cards, IReadOnlyList<ArrowReadToken> tokens, int startIndex, int count, bool instant)
	{
		if (tokens == null || count <= 0 || startIndex < 0 || startIndex + count > tokens.Count)
			return;

		ArrowReadToken firstToken = tokens[startIndex];
		ArrowReadToken lastToken = tokens[startIndex + count - 1];
		HandCardView first = FindView(firstToken?.SourceCard);
		HandCardView last = FindView(lastToken?.SourceCard);
		GetUIManager().PlayArea?.MoveIndicatorToCardRange(first?.RectTransform, last?.RectTransform, playArea, layoutDuration, layoutEase, instant);
	}

	private void MoveIndicatorToSourceCard(List<MaterialModel> cards, int sourceCardIndex, MaterialModel sourceCard, bool instant)
	{
		if (cards != null && sourceCardIndex >= 0 && sourceCardIndex < cards.Count)
			MoveIndicatorToCardRange(cards, sourceCardIndex, 1, instant);
		else
		{
			HandCardView view = FindView(sourceCard);
			GetUIManager().PlayArea?.MoveIndicatorToCardRange(view?.RectTransform, view?.RectTransform, playArea, layoutDuration, layoutEase, instant);
		}
	}

	private void ResetMagicHighlights()
	{
		for (int i = 0; i < magicViews.Count; i++)
		{
			magicViews[i].ResetRecipeHighlights();
		}
	}

    private void OnDiscardPileShuffledIntoDrawPile(IReadOnlyList<MaterialModel> cards)
    {
        RefreshStaticUI();
        RefreshMaterialListPanel();
        PlayDiscardPileShuffleAnimation(cards);
    }

    private void PlayDiscardPileShuffleAnimation(IReadOnlyList<MaterialModel> cards)
    {
        RectTransform sourceArea = GetDiscardPileArea();
        RectTransform animationRoot = transform as RectTransform;
        if (cards == null || cards.Count == 0 || cardPrefab == null || deckPileArea == null || sourceArea == null || animationRoot == null)
            return;

        int count = Mathf.Min(cards.Count, DiscardShuffleAnimationMaxCards);
        Vector3 sourcePosition = GetAreaCenterWorldPosition(sourceArea);
        Vector3 targetPosition = GetAreaCenterWorldPosition(deckPileArea);
        Vector3 sourceScale = GetScaleRelativeToParent(sourceArea, animationRoot);
        Vector3 targetScale = GetScaleRelativeToParent(deckPileArea, animationRoot);
        Vector2 sourceSize = sourceArea.rect.size;
        Vector2 targetSize = deckPileArea.rect.size;

        for (int i = 0; i < count; i++)
        {
            RectTransform clone = Object.Instantiate(cardPrefab, animationRoot);
            clone.gameObject.SetActive(true);
            clone.SetAsLastSibling();
            HandCardView view = clone.GetComponent<HandCardView>();
            if (view != null)
                view.Bind(cards[i], false);
            UIManager.RemoveJuicyMotion(clone.transform);
            DisableGraphicRaycasts(clone);

            clone.position = sourcePosition;
            clone.rotation = sourceArea.rotation;
            clone.localScale = sourceScale;
            clone.sizeDelta = sourceSize;
            RectTransform animatedClone = clone;
            Sequence sequence = DOTween.Sequence().SetTarget(this).SetDelay(i * DiscardShuffleAnimationStagger);
            sequence.Join(animatedClone.DOMove(targetPosition, layoutDuration).SetEase(layoutEase));
            sequence.Join(animatedClone.DORotateQuaternion(deckPileArea.rotation, layoutDuration).SetEase(layoutEase));
            sequence.Join(animatedClone.DOScale(targetScale, layoutDuration).SetEase(layoutEase));
            sequence.Join(animatedClone.DOSizeDelta(targetSize, layoutDuration).SetEase(layoutEase));
            sequence.OnComplete(() =>
            {
                if (animatedClone != null)
                    Object.Destroy(animatedClone.gameObject);
            });
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
		List<HandCardView> returningViews = new List<HandCardView>();
		for (int i = 0; i < views.Count; i++)
		{
			HandCardView view = views[i];
			if ((Object)view != (Object)null && (view.Card == null || !playerState.Hand.Contains(view.Card)))
				returningViews.Add(view);
		}
		if (removedTemporaryCards == null || removedTemporaryCards.Count == 0)
		{
			AnimateViewsToArea(returningViews, returnArea, onComplete);
			return;
		}
		List<HandCardView> list = new List<HandCardView>();
		List<HandCardView> consumedViews = new List<HandCardView>();
		List<HandCardView> temporaryViews = new List<HandCardView>();
		for (int i = 0; i < returningViews.Count; i++)
		{
			HandCardView handCardView = returningViews[i];
			if (!((Object)handCardView == (Object)null))
			{
				if (removedTemporaryCards.Contains(handCardView.Card))
				{
					if (playerState.Deck.Contains(handCardView.Card))
					{
						MarkBattleDeckCardConsumed(handCardView.Card);
						consumedViews.Add(handCardView);
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
            AnimateViewsToArea(consumedViews, GetConsumedPileArea(), (TweenCallback)delegate
            {
			    AnimateTemporaryViewsDissolve(temporaryViews, onComplete);
            });
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
	        buttonsInteractable = interactable;
			if ((Object)refreshButton != (Object)null)
			{
				bool canRefresh = playerState != null && (!refreshUsedThisTurn || playerState.ExtraRefreshChancesThisTurn > 0);
				refreshButton.interactable = interactable && canRefresh;
			}
	        RefreshRefreshChanceUI();
	        RefreshEndTurnButtonInteractable(HasSelectedArrowCard());

	}


	private RectTransform GetDiscardPileArea()
	{
		return discardPileArea != null ? discardPileArea : deckPileArea;
	}

	private RectTransform GetConsumedPileArea()
	{
		return consumedPileArea != null ? consumedPileArea : GetDiscardPileArea();
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
