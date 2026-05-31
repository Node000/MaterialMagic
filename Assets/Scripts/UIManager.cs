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
    [SerializeField] private LevelSelectPanelUI levelSelectPanelUI;
    [SerializeField] private SettingsPanelUI settingsPanelUI;
    [SerializeField] private MaterialListPanelUI materialListPanelUI;
    [SerializeField] private RewardPanelUI rewardPanelUI;
    [SerializeField] private RewardGridPanelUI rewardGridPanelUI;
    [SerializeField] private ShopPanelUI shopPanelUI;
    [SerializeField] private MagicModifierSelectionPanelUI magicModifierSelectionPanelUI;
    [SerializeField] private SlotSelectPanelUI slotSelectPanelUI;
    [SerializeField] private BuffTooltipUI buffTooltipUI;
    [SerializeField] private PlayerStatusUI playerStatusUI;
    [SerializeField] private PlayAreaUI playAreaUI;
    [SerializeField] private PlayerFeedbackUI playerFeedbackUI;
    [SerializeField] private ChapterProgressUI chapterProgressUI;
    [SerializeField] private TurnBannerUI turnBannerUI;
    [SerializeField] private RunResultPanelUI runResultPanelUI;
    [SerializeField] private TutorialManagerUI tutorialManagerUI;

    public MapPanelUI MapPanel => mapPanelUI;
    public LevelSelectPanelUI LevelSelectPanel => levelSelectPanelUI;
    public SettingsPanelUI SettingsPanel => settingsPanelUI;
    public MaterialListPanelUI MaterialListPanel => materialListPanelUI;
    public RewardPanelUI RewardPanel => rewardPanelUI;
    public RewardGridPanelUI RewardGridPanel => rewardGridPanelUI;
    public ShopPanelUI ShopPanel => shopPanelUI;
    public MagicModifierSelectionPanelUI MagicModifierSelectionPanel => magicModifierSelectionPanelUI;
    public SlotSelectPanelUI SlotSelectPanel => slotSelectPanelUI;
    public BuffTooltipUI BuffTooltip => buffTooltipUI;
    public PlayerStatusUI PlayerStatus => playerStatusUI;
    public PlayAreaUI PlayArea => playAreaUI;
    public PlayerFeedbackUI PlayerFeedback => playerFeedbackUI;
    public ChapterProgressUI ChapterProgress => chapterProgressUI;
    public TurnBannerUI TurnBanner => turnBannerUI;
    public RunResultPanelUI RunResultPanel => runResultPanelUI;
    public TutorialManagerUI TutorialManager => tutorialManagerUI;

    public void Initialize(HandSystemUI owner, Transform root)
    {
        mapPanelUI = GetOrAddPanel<MapPanelUI>(root, "MapPanel", mapPanelUI);
        levelSelectPanelUI = GetOrAddPanel<LevelSelectPanelUI>(root, "LevelSelectPanel", levelSelectPanelUI);
        settingsPanelUI = GetOrAddPanel<SettingsPanelUI>(root, "SettingsPanel", settingsPanelUI);
        materialListPanelUI = GetOrAddPanelInChildren<MaterialListPanelUI>(root, "MaterialListPanel", materialListPanelUI);
        rewardPanelUI = GetOrAddPanel<RewardPanelUI>(root, "RewardPanel", rewardPanelUI);
        rewardGridPanelUI = GetOrAddPanel<RewardGridPanelUI>(root, "RewardGridPanel", rewardGridPanelUI);
        shopPanelUI = GetOrAddPanel<ShopPanelUI>(root, "ShopPanel", shopPanelUI);
        magicModifierSelectionPanelUI = GetOrAddPanel<MagicModifierSelectionPanelUI>(root, "MagicModifierSelectionPanel", magicModifierSelectionPanelUI);
        slotSelectPanelUI = GetOrAddPanel<SlotSelectPanelUI>(root, "SlotSelectPanel", slotSelectPanelUI);
        buffTooltipUI = GetOrAddPanel<BuffTooltipUI>(root, "BuffTooltip", buffTooltipUI);
        playerStatusUI = GetOrAddPanelInChildren<PlayerStatusUI>(root, "PlayerStatus", playerStatusUI);
        playAreaUI = GetOrAddPanelInChildren<PlayAreaUI>(root, "PlayArea", playAreaUI);
        playerFeedbackUI = GetOrAddOnRoot(root, playerFeedbackUI);
        chapterProgressUI = GetOrAddPanelInChildren<ChapterProgressUI>(root, "ChapterProgress", chapterProgressUI);
        turnBannerUI = GetOrAddPanelInChildren<TurnBannerUI>(root, "TurnBanner", turnBannerUI);
        runResultPanelUI = GetOrAddPanel<RunResultPanelUI>(root, "RunResultPanel", runResultPanelUI);
        tutorialManagerUI = GetOrAddPanelInChildren<TutorialManagerUI>(root, "TutorialRoot", tutorialManagerUI);

        mapPanelUI?.Initialize(owner);
        levelSelectPanelUI?.Initialize(owner);
        settingsPanelUI?.Initialize(owner);
        materialListPanelUI?.Initialize(owner);
        rewardPanelUI?.Initialize(owner);
        rewardGridPanelUI?.Initialize(owner);
        shopPanelUI?.Initialize(owner);
        magicModifierSelectionPanelUI?.Initialize(owner);
        slotSelectPanelUI?.Initialize(owner);
        buffTooltipUI?.Initialize(owner);
        playerStatusUI?.Initialize(owner);
        playAreaUI?.Initialize(owner);
        playerFeedbackUI?.Initialize(owner, root);
        chapterProgressUI?.Initialize();
        turnBannerUI?.Initialize();
        runResultPanelUI?.Initialize(owner);
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
        RectTransform topBar = FindChildRect(root, "TopBar");
        if (topBar == null)
            return;

        Button settingsButton = FindChildComponent<Button>(topBar, "SettingsButton");
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(ToggleSettingsPanel);
        }

        Button mapButton = FindChildComponent<Button>(topBar, "MapButton");
        if (mapButton != null)
        {
            mapButton.onClick.RemoveAllListeners();
            mapButton.onClick.AddListener(ToggleMapPanel);
        }
    }

    public void ToggleMapPanel()
    {
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

    public void ShowLevelSelect(IReadOnlyList<RunMapNodeModel> nodes, int currentNodeIndex)
    {
        levelSelectPanelUI?.Show(nodes, currentNodeIndex);
    }

    public void HideLevelSelect()
    {
        levelSelectPanelUI?.Hide();
    }

    public void HideLevelSelectAnimated()
    {
        levelSelectPanelUI?.HideAnimated();
    }

    public void ToggleSettingsPanel()
    {
        settingsPanelUI?.Toggle();
    }

    public void ToggleMaterialListPanel()
    {
        materialListPanelUI?.Toggle();
    }

    public void RefreshMaterialListPanel()
    {
        materialListPanelUI?.Refresh();
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

    public void ShowVictoryPanel()
    {
        runResultPanelUI?.ShowVictory();
    }

    public void ShowDefeatPanel()
    {
        runResultPanelUI?.ShowDefeat();
    }

    public void ShowBuffTooltip(BuffSlotView slot, BuffModel buff)
    {
        buffTooltipUI?.Show(slot, buff);
    }

    public void HideBuffTooltip(BuffSlotView slot)
    {
        buffTooltipUI?.Hide(slot);
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
            LevelType.Event => null,
            LevelType.Rest => Resources.Load<Sprite>("Images/UI/shop"),
            LevelType.Reward => null,
            _ => Resources.Load<Sprite>("Images/UI/normal"),
        };
    }

    internal static string GetLevelTypeName(LevelType type)
    {
        return type switch
        {
            LevelType.Shop => "商店",
            LevelType.Event => "事件",
            LevelType.Rest => "休息",
            LevelType.Reward => "奖励",
            _ => "战斗",
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
