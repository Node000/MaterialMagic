using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DebugBattlePanelUI : MonoBehaviour
{
    [SerializeField] private HandSystemUI handSystem;
    [SerializeField] private TMP_Dropdown levelDropdown;
    [SerializeField] private Button startBattleButton;
    [SerializeField] private Button damageButton;
    [SerializeField] private Button killTargetButton;
    [SerializeField] private Button drawCardButton;
    [SerializeField] private Button startRestButton;
    [SerializeField] private TMP_Dropdown eventDropdown;
    [SerializeField] private Button startEventButton;
    [SerializeField] private TMP_Dropdown magicDropdown;
    [SerializeField] private Button addMagicButton;
    [SerializeField] private Button removeLastMagicButton;
    [SerializeField] private TMP_Dropdown shopDropdown;
    [SerializeField] private Button openShopButton;
    [SerializeField] private Button arrowUpgradeButton;
    [SerializeField] private ArrowUpgradePanelUI arrowUpgradePanel;
    [SerializeField] private Button closeButton;

    private readonly List<int> battleLevelIds = new List<int>();
    private readonly List<int> eventIds = new List<int>();
    private readonly List<int> magicIds = new List<int>();
    private readonly List<int> shopLevelIds = new List<int>();

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        RegisterListeners();
        PopulateBattleDropdown();
        PopulateEventDropdown();
        PopulateMagicDropdown();
        PopulateShopDropdown();
    }

    private void RegisterListeners()
    {
        startBattleButton?.onClick.RemoveListener(StartSelectedBattle);
        damageButton?.onClick.RemoveListener(DealDamageToTarget);
        killTargetButton?.onClick.RemoveListener(KillTargetEnemy);
        drawCardButton?.onClick.RemoveListener(DrawCard);
        startRestButton?.onClick.RemoveListener(StartSelectedRest);
        startEventButton?.onClick.RemoveListener(StartSelectedEvent);
        addMagicButton?.onClick.RemoveListener(AddSelectedMagic);
        removeLastMagicButton?.onClick.RemoveListener(RemoveLastMagic);
        openShopButton?.onClick.RemoveListener(OpenSelectedShop);
        arrowUpgradeButton?.onClick.RemoveListener(OpenArrowUpgradePanel);
        closeButton?.onClick.RemoveListener(Hide);

        startBattleButton?.onClick.AddListener(StartSelectedBattle);
        damageButton?.onClick.AddListener(DealDamageToTarget);
        killTargetButton?.onClick.AddListener(KillTargetEnemy);
        drawCardButton?.onClick.AddListener(DrawCard);
        startRestButton?.onClick.AddListener(StartSelectedRest);
        startEventButton?.onClick.AddListener(StartSelectedEvent);
        addMagicButton?.onClick.AddListener(AddSelectedMagic);
        removeLastMagicButton?.onClick.AddListener(RemoveLastMagic);
        openShopButton?.onClick.AddListener(OpenSelectedShop);
        arrowUpgradeButton?.onClick.AddListener(OpenArrowUpgradePanel);
        closeButton?.onClick.AddListener(Hide);
    }

    private void OnDestroy()
    {
        startBattleButton?.onClick.RemoveListener(StartSelectedBattle);
        damageButton?.onClick.RemoveListener(DealDamageToTarget);
        killTargetButton?.onClick.RemoveListener(KillTargetEnemy);
        drawCardButton?.onClick.RemoveListener(DrawCard);
        startRestButton?.onClick.RemoveListener(StartSelectedRest);
        startEventButton?.onClick.RemoveListener(StartSelectedEvent);
        addMagicButton?.onClick.RemoveListener(AddSelectedMagic);
        removeLastMagicButton?.onClick.RemoveListener(RemoveLastMagic);
        openShopButton?.onClick.RemoveListener(OpenSelectedShop);
        arrowUpgradeButton?.onClick.RemoveListener(OpenArrowUpgradePanel);
        closeButton?.onClick.RemoveListener(Hide);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }

    private void CacheReferences()
    {
        if (handSystem == null)
            handSystem = GetComponentInParent<HandSystemUI>(true);
        if (levelDropdown == null)
            levelDropdown = transform.Find("LevelDropdown")?.GetComponent<TMP_Dropdown>();
        if (startBattleButton == null)
            startBattleButton = transform.Find("StartBattleButton")?.GetComponent<Button>();
        if (damageButton == null)
            damageButton = transform.Find("DamageButton")?.GetComponent<Button>();
        if (killTargetButton == null)
            killTargetButton = transform.Find("KillTargetButton")?.GetComponent<Button>();
        if (drawCardButton == null)
            drawCardButton = transform.Find("DrawCardButton")?.GetComponent<Button>();
        if (startRestButton == null)
            startRestButton = transform.Find("StartRestButton")?.GetComponent<Button>();
        if (eventDropdown == null)
            eventDropdown = transform.Find("EventDropdown")?.GetComponent<TMP_Dropdown>();
        if (startEventButton == null)
            startEventButton = transform.Find("StartEventButton")?.GetComponent<Button>();
        if (magicDropdown == null)
            magicDropdown = transform.Find("MagicDropdown")?.GetComponent<TMP_Dropdown>();
        if (addMagicButton == null)
            addMagicButton = transform.Find("AddMagicButton")?.GetComponent<Button>();
        if (removeLastMagicButton == null)
            removeLastMagicButton = transform.Find("RemoveLastMagicButton")?.GetComponent<Button>();
        if (shopDropdown == null)
            shopDropdown = transform.Find("ShopDropdown")?.GetComponent<TMP_Dropdown>();
        if (openShopButton == null)
            openShopButton = transform.Find("OpenShopButton")?.GetComponent<Button>();
        if (arrowUpgradeButton == null)
            arrowUpgradeButton = transform.Find("ArrowUpgradeButton")?.GetComponent<Button>();
        if (arrowUpgradePanel == null)
            arrowUpgradePanel = GetComponentInParent<ArrowUpgradePanelUI>(true);
        if (arrowUpgradePanel == null && transform.parent != null)
            arrowUpgradePanel = transform.parent.Find("ArrowUpgradePanel")?.GetComponent<ArrowUpgradePanelUI>();
        if (closeButton == null)
            closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
    }

    private void PopulateBattleDropdown()
    {
        PopulateLevelDropdown(levelDropdown, startBattleButton, battleLevelIds, IsDebugBattleLevel, BuildBattleOptionText, "没有可用战斗关卡");
    }

    private void PopulateEventDropdown()
    {
        eventIds.Clear();
        List<EventData> events = new List<EventData>(GameDataDatabase.EventData.Values);
        events.Sort((left, right) => left.numericId.CompareTo(right.numericId));
        List<string> options = new List<string>(events.Count);
        for (int i = 0; i < events.Count; i++)
        {
            EventData data = events[i];
            if (data == null)
                continue;

            eventIds.Add(data.numericId);
            options.Add($"{data.numericId} {GetLocalizedName(data.titleKey, data.id)}");
        }
        ApplyOptions(eventDropdown, startEventButton, options, "没有可用事件");
    }

    private void PopulateMagicDropdown()
    {
        magicIds.Clear();
        List<MagicData> magics = new List<MagicData>(GameDataDatabase.MagicData.Values);
        magics.Sort((left, right) => left.numericId.CompareTo(right.numericId));
        List<string> options = new List<string>(magics.Count);
        for (int i = 0; i < magics.Count; i++)
        {
            MagicData data = magics[i];
            if (data == null)
                continue;

            magicIds.Add(data.numericId);
            options.Add($"{data.numericId} {GetLocalizedName(data.nameKey, data.id)}");
        }
        ApplyOptions(magicDropdown, addMagicButton, options, "没有可用魔法");
    }

    private void PopulateShopDropdown()
    {
        PopulateLevelDropdown(shopDropdown, openShopButton, shopLevelIds, IsShopLevel, BuildShopOptionText, "没有可用商店关卡");
    }

    private static void PopulateLevelDropdown(TMP_Dropdown dropdown, Button actionButton, List<int> ids, System.Predicate<LevelData> predicate, System.Func<LevelData, string> buildText, string emptyText)
    {
        ids.Clear();
        List<LevelData> levels = new List<LevelData>(GameDataDatabase.LevelData.Values);
        levels.Sort((left, right) => left.numericId.CompareTo(right.numericId));
        List<string> options = new List<string>(levels.Count);
        for (int i = 0; i < levels.Count; i++)
        {
            LevelData level = levels[i];
            if (!predicate(level))
                continue;

            ids.Add(level.numericId);
            options.Add(buildText(level));
        }
        ApplyOptions(dropdown, actionButton, options, emptyText);
    }

    private static void ApplyOptions(TMP_Dropdown dropdown, Button actionButton, List<string> options, string emptyText)
    {
        if (dropdown == null)
            return;

        bool hasOptions = options.Count > 0;
        if (!hasOptions)
            options.Add(emptyText);
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();
        if (actionButton != null)
            actionButton.interactable = hasOptions;
    }

    private static bool IsDebugBattleLevel(LevelData level)
    {
        if (level == null || (level.levelType != LevelType.Battle && level.levelType != LevelType.Elite))
            return false;

        return level.enemyIds?.Length > 0 || level.enemies?.Length > 0 || level.randomEnemyGroups?.Length > 0;
    }

    private static bool IsShopLevel(LevelData level)
    {
        return level != null && level.levelType == LevelType.Shop;
    }

    private static string BuildBattleOptionText(LevelData level)
    {
        return $"{level.numericId} {UIManager.GetLevelTypeName(level.levelType)} {GetLocalizedName(level.titleKey, level.id)}：{BuildEnemySummary(level)}";
    }

    private static string BuildShopOptionText(LevelData level)
    {
        return $"{level.numericId} {GetLocalizedName(level.titleKey, level.id)}";
    }

    private static string BuildEnemySummary(LevelData level)
    {
        if (level.randomEnemyGroups != null && level.randomEnemyGroups.Length > 0)
        {
            List<string> groups = new List<string>(level.randomEnemyGroups.Length);
            for (int i = 0; i < level.randomEnemyGroups.Length; i++)
                groups.Add(BuildEnemyGroupText(level.randomEnemyGroups[i]?.enemies));
            return "随机[" + string.Join(" / ", groups) + "]";
        }

        if (level.enemies != null && level.enemies.Length > 0)
            return BuildEnemyGroupText(level.enemies);
        if (level.enemyIds != null && level.enemyIds.Length > 0)
            return BuildEnemyIdGroupText(level.enemyIds);
        return "无敌人";
    }

    private static string BuildEnemyGroupText(LevelEnemyData[] enemies)
    {
        if (enemies == null || enemies.Length == 0)
            return "无敌人";

        List<string> names = new List<string>(enemies.Length);
        for (int i = 0; i < enemies.Length; i++)
            names.Add(GetEnemyName(enemies[i] != null ? enemies[i].enemyId : 0));
        return string.Join(" + ", names);
    }

    private static string BuildEnemyIdGroupText(int[] enemyIds)
    {
        List<string> names = new List<string>(enemyIds.Length);
        for (int i = 0; i < enemyIds.Length; i++)
            names.Add(GetEnemyName(enemyIds[i]));
        return string.Join(" + ", names);
    }

    private static string GetEnemyName(int enemyId)
    {
        if (GameDataDatabase.TryGetEnemyData(enemyId, out EnemyData data))
            return GetLocalizedName(data.nameKey, data.Id);
        return enemyId > 0 ? enemyId.ToString() : "未知敌人";
    }

    private static string GetLocalizedName(string key, string fallback)
    {
        return !string.IsNullOrEmpty(key) ? LocalizationSystem.GetText(key, fallback) : fallback;
    }

    private void StartSelectedBattle()
    {
        if (handSystem == null || levelDropdown == null || levelDropdown.value < 0 || levelDropdown.value >= battleLevelIds.Count)
            return;

        if (GameDataDatabase.TryGetLevelData(battleLevelIds[levelDropdown.value], out LevelData level))
            handSystem.DebugStartBattleLevel(level);
    }

    private void DealDamageToTarget()
    {
        handSystem?.DebugDealDamageToTarget(10);
    }

    private void KillTargetEnemy()
    {
        handSystem?.DebugKillTargetEnemy();
    }

    private void DrawCard()
    {
        handSystem?.DebugDrawCards(1);
    }

    private void StartSelectedRest()
    {
        foreach (LevelData level in GameDataDatabase.LevelData.Values)
        {
            if (level != null && level.levelType == LevelType.Rest)
            {
                handSystem?.DebugStartRestLevel(level);
                return;
            }
        }
    }

    private void StartSelectedEvent()
    {
        if (handSystem == null || eventDropdown == null || eventDropdown.value < 0 || eventDropdown.value >= eventIds.Count)
            return;

        if (GameDataDatabase.TryGetEventData(eventIds[eventDropdown.value], out EventData data))
            handSystem.DebugStartEvent(data);
    }

    private void AddSelectedMagic()
    {
        if (handSystem == null || magicDropdown == null || magicDropdown.value < 0 || magicDropdown.value >= magicIds.Count)
            return;

        if (GameDataDatabase.TryGetMagicData(magicIds[magicDropdown.value], out MagicData data))
            handSystem.DebugAddMagic(data);
    }

    private void RemoveLastMagic()
    {
        handSystem?.DebugRemoveLastMagic();
    }

    private void OpenSelectedShop()
    {
        if (handSystem == null || shopDropdown == null || shopDropdown.value < 0 || shopDropdown.value >= shopLevelIds.Count)
            return;

        if (GameDataDatabase.TryGetLevelData(shopLevelIds[shopDropdown.value], out LevelData level))
            handSystem.DebugStartShop(level);
    }

    private void OpenArrowUpgradePanel()
    {
        if (arrowUpgradePanel == null)
            return;

        arrowUpgradePanel.Show(handSystem != null ? handSystem.PlayerState : BattleManager.Instance?.PlayerState);
    }
}

