using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    private static TMP_FontAsset cachedDefaultTMPFont;

    [SerializeField] private MapPanelUI mapPanelUI;
    [SerializeField] private ChapterGridPanelUI chapterGridPanelUI;
    [SerializeField] private LevelSelectPanelUI levelSelectPanelUI;
    [SerializeField] private SettingsPanelUI settingsPanelUI;
    [SerializeField] private MaterialListPanelUI materialListPanelUI;
    [SerializeField] private MaterialListPanelUI materialSelectionPanelUI;
    [SerializeField] private RewardPanelUI rewardPanelUI;
    [SerializeField] private RewardGridPanelUI rewardGridPanelUI;
    [SerializeField] private ShopPanelUI shopPanelUI;
    [SerializeField] private MagicModifierSelectionPanelUI magicModifierSelectionPanelUI;
    [SerializeField] private SlotSelectPanelUI slotSelectPanelUI;
    [SerializeField] private UnifiedDetailPopupUI unifiedDetailPopupUI;
    [SerializeField] private PlayerStatusUI playerStatusUI;
    [SerializeField] private PlayAreaUI playAreaUI;
    [SerializeField] private PlayerFeedbackUI playerFeedbackUI;
    [SerializeField] private ChapterProgressUI chapterProgressUI;
    [SerializeField] private GoldDisplayUI goldDisplayUI;
    [SerializeField] private TurnBannerUI turnBannerUI;
    [SerializeField] private RunResultPanelUI runResultPanelUI;
    [SerializeField] private RunResultPanelUI victoryResultPanelUI;
    [SerializeField] private RunResultPanelUI defeatResultPanelUI;
    [SerializeField] private TutorialManagerUI tutorialManagerUI;

    public MapPanelUI MapPanel => mapPanelUI;
    public ChapterGridPanelUI ChapterGridPanel => chapterGridPanelUI;
    public LevelSelectPanelUI LevelSelectPanel => levelSelectPanelUI;
    public SettingsPanelUI SettingsPanel => settingsPanelUI;
    public MaterialListPanelUI MaterialListPanel => materialListPanelUI;
    public MaterialListPanelUI MaterialSelectionPanel => materialSelectionPanelUI != null ? materialSelectionPanelUI : materialListPanelUI;
    public RewardPanelUI RewardPanel => rewardPanelUI;
    public RewardGridPanelUI RewardGridPanel => rewardGridPanelUI;
    public ShopPanelUI ShopPanel => shopPanelUI;
    public MagicModifierSelectionPanelUI MagicModifierSelectionPanel => magicModifierSelectionPanelUI;
    public SlotSelectPanelUI SlotSelectPanel => slotSelectPanelUI;
    public UnifiedDetailPopupUI UnifiedDetailPopup => unifiedDetailPopupUI;
    public PlayerStatusUI PlayerStatus => playerStatusUI;
    public PlayAreaUI PlayArea => playAreaUI;
    public PlayerFeedbackUI PlayerFeedback => playerFeedbackUI;
    public ChapterProgressUI ChapterProgress => chapterProgressUI;
    public GoldDisplayUI GoldDisplay => goldDisplayUI;
    public TurnBannerUI TurnBanner => turnBannerUI;
    public RunResultPanelUI RunResultPanel => runResultPanelUI;
    public RunResultPanelUI VictoryResultPanel => victoryResultPanelUI != null ? victoryResultPanelUI : runResultPanelUI;
    public RunResultPanelUI DefeatResultPanel => defeatResultPanelUI != null ? defeatResultPanelUI : runResultPanelUI;
    public TutorialManagerUI TutorialManager => tutorialManagerUI;

    public void Initialize(HandSystemUI owner, Transform root)
    {
        mapPanelUI = GetOrAddPanel<MapPanelUI>(root, "MapPanel", mapPanelUI);
        chapterGridPanelUI = GetOrAddPanel<ChapterGridPanelUI>(root, "MapPanel", chapterGridPanelUI);
        levelSelectPanelUI = GetOrAddPanel<LevelSelectPanelUI>(root, "LevelSelectPanel", levelSelectPanelUI);
        settingsPanelUI = GetOrAddPanel<SettingsPanelUI>(root, "SettingsPanel", settingsPanelUI);
        materialListPanelUI = GetOrAddPanelInChildren<MaterialListPanelUI>(root, "MaterialListPanel", materialListPanelUI);
        materialSelectionPanelUI = GetOrAddPanelInChildren<MaterialListPanelUI>(root, "SelectionShowPanel", materialSelectionPanelUI);
        rewardPanelUI = GetOrAddPanel<RewardPanelUI>(root, "RewardPanel", rewardPanelUI);
        rewardGridPanelUI = GetOrAddPanel<RewardGridPanelUI>(root, "RewardGridPanel", rewardGridPanelUI);
        shopPanelUI = GetOrAddPanel<ShopPanelUI>(root, "ShopPanel", shopPanelUI);
        magicModifierSelectionPanelUI = GetOrAddPanel<MagicModifierSelectionPanelUI>(root, "MagicModifierSelectionPanel", magicModifierSelectionPanelUI);
        slotSelectPanelUI = GetOrAddPanel<SlotSelectPanelUI>(root, "SlotSelectPanel", slotSelectPanelUI);
        unifiedDetailPopupUI = GetOrAddPanelInChildren<UnifiedDetailPopupUI>(root, "UnifiedDetailPopup", unifiedDetailPopupUI);
        playerStatusUI = GetOrAddPanelInChildren<PlayerStatusUI>(root, "PlayerStatus", playerStatusUI);
        playAreaUI = GetOrAddPanelInChildren<PlayAreaUI>(root, "PlayArea", playAreaUI);
        playerFeedbackUI = GetOrAddOnRoot(root, playerFeedbackUI);
        chapterProgressUI = GetOrAddPanelInChildren<ChapterProgressUI>(root, "ChapterProgress", chapterProgressUI);
        goldDisplayUI = GetOrAddPanelInChildren<GoldDisplayUI>(root, "GoldDisplay", goldDisplayUI);
        turnBannerUI = GetOrAddPanelInChildren<TurnBannerUI>(root, "TurnBanner", turnBannerUI);
        runResultPanelUI = GetOrAddPanel<RunResultPanelUI>(root, "RunResultPanel", runResultPanelUI);
        victoryResultPanelUI = GetOrAddPanel<RunResultPanelUI>(root, "VictoryResultPanel", victoryResultPanelUI);
        defeatResultPanelUI = GetOrAddPanel<RunResultPanelUI>(root, "DefeatResultPanel", defeatResultPanelUI);
        tutorialManagerUI = GetOrAddPanelInChildren<TutorialManagerUI>(root, "TutorialRoot", tutorialManagerUI);

        mapPanelUI?.Initialize(owner);
        chapterGridPanelUI?.Initialize(owner);
        levelSelectPanelUI?.Initialize(owner);
        settingsPanelUI?.Initialize(owner);
        materialListPanelUI?.Initialize(owner);
        materialSelectionPanelUI?.Initialize(owner);
        rewardPanelUI?.Initialize(owner);
        rewardGridPanelUI?.Initialize(owner);
        shopPanelUI?.Initialize(owner);
        magicModifierSelectionPanelUI?.Initialize(owner);
        slotSelectPanelUI?.Initialize(owner);
        unifiedDetailPopupUI?.Initialize();
        playerStatusUI?.Initialize(owner);
        playAreaUI?.Initialize(owner);
        playerFeedbackUI?.Initialize(owner, root);
        chapterProgressUI?.Initialize();
        goldDisplayUI?.Initialize();
        turnBannerUI?.Initialize();
        runResultPanelUI?.Initialize(owner);
        victoryResultPanelUI?.Initialize(owner);
        defeatResultPanelUI?.Initialize(owner);
        tutorialManagerUI?.Initialize(owner);

        BindTopBar(root);
    }

    private static T GetOrAddPanel<T>(Transform root, string name, T existing) where T : Component
    {
        if (existing != null)
            return existing;

        RectTransform rect = FindChildRect(root, name);
        if (rect == null)
            return null;

        T component = rect.GetComponent<T>();
        return component != null ? component : rect.gameObject.AddComponent<T>();
    }

    private static T GetOrAddPanelInChildren<T>(Transform root, string name, T existing) where T : Component
    {
        if (existing != null)
            return existing;

        Transform child = FindChildRecursive(root, name);
        if (child == null)
            return null;

        T component = child.GetComponent<T>();
        return component != null ? component : child.gameObject.AddComponent<T>();
    }

    private static PlayerFeedbackUI GetOrAddOnRoot(Transform root, PlayerFeedbackUI existing)
    {
        if (existing != null)
            return existing;

        PlayerFeedbackUI component = root.GetComponent<PlayerFeedbackUI>();
        return component != null ? component : root.gameObject.AddComponent<PlayerFeedbackUI>();
    }

    private void BindTopBar(Transform root)
    {
        RectTransform topBar = FindChildRecursive(root, "TopBar") as RectTransform;
        if (topBar == null)
            return;

        Button settingsButton = FindTopBarButton(topBar, "SettingsButton", "SettingsIconButton");
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(ToggleSettingsPanel);
            ConfigureTopBarTooltip(settingsButton, "ui.top_bar.settings.title", "ui.top_bar.settings.body", "设置", "打开设置菜单，调整音量或返回开始界面。");
        }

        Button mapButton = FindTopBarButton(topBar, "MapButton", "MapIconButton");
        if (mapButton != null)
        {
            mapButton.onClick.RemoveAllListeners();
            mapButton.onClick.AddListener(ToggleMapPanel);
            ConfigureTopBarTooltip(mapButton, "ui.top_bar.map.title", "ui.top_bar.map.body", "地图", "查看当前章节地图和当前位置。");
        }

        Button chapterProgressButton = FindTopBarButton(topBar, "ChapterProgressButton", "Step");
        if (chapterProgressButton != null)
            ConfigureTopBarTooltip(chapterProgressButton, "ui.top_bar.chapter_progress.title", "ui.top_bar.chapter_progress.body", "关卡进度", "显示当前关卡数与章节总关卡数。");
    }

    private Button FindTopBarButton(Transform topBar, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Button button = FindChildComponent<Button>(topBar, names[i]);
            if (button != null)
                return button;
        }
        return null;
    }

    private void ConfigureTopBarTooltip(Button button, string titleKey, string bodyKey, string titleFallback, string bodyFallback)
    {
        TopBarIconTooltipUI tooltip = button.GetComponent<TopBarIconTooltipUI>();
        if (tooltip == null)
            tooltip = button.gameObject.AddComponent<TopBarIconTooltipUI>();
        tooltip.Configure(this, titleKey, bodyKey, titleFallback, bodyFallback);
    }

    public void ToggleMapPanel()
    {
        RunManager currentRun = RunManager.Current;
        if (currentRun != null && currentRun.State == RunFlowState.MapSelection)
            return;

        if (chapterGridPanelUI != null)
        {
            chapterGridPanelUI.Toggle(currentRun != null ? currentRun.MapGrid : null);
            return;
        }
        mapPanelUI?.Toggle();
    }

    public void ShowMapPanel(bool focusCurrentNode, TweenCallback onComplete, bool animateMarker)
    {
        if (mapPanelUI == null)
        {
            onComplete?.Invoke();
            return;
        }

        mapPanelUI.Show(focusCurrentNode, onComplete, animateMarker);
    }

    public void SyncMapPanelToCurrentNode()
    {
        mapPanelUI?.SyncPlayerMarkerToCurrentNode();
    }

    public void HideMapPanel()
    {
        mapPanelUI?.Hide();
    }

    public Tween HideMapPanelAnimated()
    {
        return mapPanelUI != null ? mapPanelUI.HideAnimated() : null;
    }

    public void ShowLevelSelect(IReadOnlyList<RunMapNodeModel> nodes, int currentNodeIndex)
    {
        levelSelectPanelUI?.Show(nodes, currentNodeIndex);
    }

    public void ShowChapterGridPanel(RunMapGridModel grid)
    {
        chapterGridPanelUI?.Show(grid, true);
    }

    public void HideChapterGridPanel()
    {
        chapterGridPanelUI?.Hide();
    }

    public Tween HideChapterGridPanelAnimated()
    {
        return chapterGridPanelUI != null ? chapterGridPanelUI.HideAnimated() : null;
    }

    public void HideLevelSelect()
    {
        levelSelectPanelUI?.Hide();
    }

    public void HideLevelSelectAnimated(Action onComplete = null)
    {
        if (levelSelectPanelUI == null)
        {
            onComplete?.Invoke();
            return;
        }

        levelSelectPanelUI.HideAnimated(onComplete);
    }

    public void ToggleSettingsPanel()
    {
        settingsPanelUI?.Toggle();
    }

    public void ToggleMaterialListPanel()
    {
        materialListPanelUI?.Toggle(MaterialListPanelUI.DisplayMode.CombatPiles);
    }

    public void ToggleDiscardPilePanel()
    {
        materialListPanelUI?.Toggle(MaterialListPanelUI.DisplayMode.CombatPiles);
    }

    public void ToggleConsumedPilePanel()
    {
        materialListPanelUI?.Toggle(MaterialListPanelUI.DisplayMode.CombatPiles);
    }

    public void RefreshMaterialListPanel()
    {
        materialListPanelUI?.Refresh();
        materialSelectionPanelUI?.Refresh();
    }

    public void ShowRewardPanel()
    {
        rewardPanelUI?.Show();
    }

    public void HideRewardPanel()
    {
        rewardPanelUI?.Hide();
    }

    public void ShowShopPanel(LevelData level)
    {
        shopPanelUI?.Show(level);
    }

    public void ShowShopPanel(LevelData level, ShopNodeSaveData savedState)
    {
        shopPanelUI?.Show(level, savedState);
    }

    public void HideShopPanel()
    {
        shopPanelUI?.Hide();
    }

    public void ShowSlotSelect(MagicData rewardMagic)
    {
        slotSelectPanelUI?.Show(rewardMagic);
    }

    public void ShowSlotSelect(MagicData rewardMagic, Action<int> onSlotChosen)
    {
        slotSelectPanelUI?.Show(rewardMagic, onSlotChosen);
    }

    public void HideSlotSelect()
    {
        slotSelectPanelUI?.Hide();
    }

    public void ShowVictoryPanel(float playSeconds, IReadOnlyList<string> magicNames, bool tutorialVictory = false)
    {
        RunResultPanelUI panel = VictoryResultPanel;
        RunResultPanelUI otherPanel = DefeatResultPanel;
        if (otherPanel != null && otherPanel != panel)
            otherPanel.Hide();
        panel?.ShowVictory(playSeconds, magicNames, tutorialVictory);
    }

    public void ShowDefeatPanel(string defeatingEnemyName = null)
    {
        RunResultPanelUI panel = DefeatResultPanel;
        RunResultPanelUI otherPanel = VictoryResultPanel;
        if (otherPanel != null && otherPanel != panel)
            otherPanel.Hide();
        panel?.ShowDefeat(defeatingEnemyName);
    }

    public void ShowBuffTooltip(BuffSlotView slot, BuffModel buff)
    {
        if (buff == null)
            return;
        unifiedDetailPopupUI?.Show(slot, UnifiedDetailContentBuilder.Build(buff));
    }

    public void PinBuffTooltip(BuffSlotView slot, BuffModel buff)
    {
        if (buff == null)
            return;
        unifiedDetailPopupUI?.Pin(slot, UnifiedDetailContentBuilder.Build(buff));
    }

    public void HideBuffTooltip(BuffSlotView slot)
    {
        unifiedDetailPopupUI?.Hide(slot);
    }

    public void ShowEnemyIntentTooltip(EnemyIntentView view, EnemyModel enemy, EnemyIntentData intent, PlayerState playerState)
    {
        if (enemy == null || intent == null)
            return;
        unifiedDetailPopupUI?.Show(view, UnifiedDetailContentBuilder.Build(enemy, intent, playerState));
    }

    public void PinEnemyIntentTooltip(EnemyIntentView view, EnemyModel enemy, EnemyIntentData intent, PlayerState playerState)
    {
        if (enemy == null || intent == null)
            return;
        unifiedDetailPopupUI?.Pin(view, UnifiedDetailContentBuilder.Build(enemy, intent, playerState));
    }

    public void HideEnemyIntentTooltip(EnemyIntentView view)
    {
        unifiedDetailPopupUI?.Hide(view);
    }

    public void ShowUnifiedDetailPopup(object anchor, UnifiedDetailContent content)
    {
        unifiedDetailPopupUI?.Show(anchor, content);
    }

    public void PinUnifiedDetailPopup(object anchor, UnifiedDetailContent content)
    {
        unifiedDetailPopupUI?.Pin(anchor, content);
    }

    public void UnpinUnifiedDetailPopup()
    {
        unifiedDetailPopupUI?.Unpin();
    }

    public void HideUnifiedDetailPopup(object anchor)
    {
        unifiedDetailPopupUI?.Hide(anchor);
    }

    internal static RectTransform FindChildRect(Transform root, string name)
    {
        Transform child = root != null ? root.Find(name) : null;
        return child as RectTransform;
    }

    internal static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null)
            return null;

        Transform direct = root.Find(name);
        if (direct != null)
            return direct;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    internal static T FindChildComponent<T>(Transform root, string name) where T : Component
    {
        Transform child = root != null ? root.Find(name) : null;
        return child != null ? child.GetComponent<T>() : null;
    }

    internal static TMP_FontAsset GetDefaultTMPFont()
    {
        if (cachedDefaultTMPFont == null)
            cachedDefaultTMPFont = Resources.Load<TMP_FontAsset>("Fonts/FZG_CN SDF");
        return cachedDefaultTMPFont;
    }

    internal static Sprite LoadLevelTypeSprite(LevelType type)
    {
        return type switch
        {
            LevelType.Shop => Resources.Load<Sprite>("Images/UI/shop"),
            LevelType.Event => Resources.Load<Sprite>("Images/UI/Event"),
            LevelType.RemoveMaterial => Resources.Load<Sprite>("Images/UI/RemoveMaterial"),
            LevelType.AddMaterial => Resources.Load<Sprite>("Images/UI/AddMaterial"),
            LevelType.Rest => Resources.Load<Sprite>("Images/UI/Rest"),
            LevelType.Reward => Resources.Load<Sprite>("Images/UI/Reward"),
            LevelType.Elite => Resources.Load<Sprite>("Images/UI/hard"),
            _ => Resources.Load<Sprite>("Images/UI/normal"),
        };
    }

    internal static string GetLevelTypeName(LevelType type)
    {
        return type switch
        {
            LevelType.Shop => LocalizationSystem.GetText("ui.level_type.shop", "商店"),
            LevelType.Event => LocalizationSystem.GetText("ui.level_type.event", "事件"),
            LevelType.RemoveMaterial => LocalizationSystem.GetText("ui.level_type.remove_material", "删除"),
            LevelType.AddMaterial => LocalizationSystem.GetText("ui.level_type.add_material", "新增"),
            LevelType.Rest => LocalizationSystem.GetText("ui.level_type.rest", "休息"),
            LevelType.Reward => LocalizationSystem.GetText("ui.level_type.reward", "奖励"),
            LevelType.Elite => LocalizationSystem.GetText("ui.level_type.elite", "精英"),
            _ => LocalizationSystem.GetText("ui.level_type.battle", "战斗"),
        };
    }

    public static void AddJuicyMotion(Transform target)
    {
        if (target != null && target.GetComponent<JuicyMotion>() == null)
            target.gameObject.AddComponent<JuicyMotion>();
    }

    public static void RemoveJuicyMotion(Transform target)
    {
        if (target == null)
            return;

        JuicyMotion motion = target.GetComponent<JuicyMotion>();
        if (motion != null)
            Destroy(motion);
    }
}
