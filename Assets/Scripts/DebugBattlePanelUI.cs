using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DebugBattlePanelUI : MonoBehaviour
{
    [SerializeField] private HandSystemUI handSystem;
    [SerializeField] private Button damageButton;
    [SerializeField] private Button healButton;
    [SerializeField] private Button shieldButton;
    [SerializeField] private Button drawCardButton;
    [SerializeField] private TMP_Dropdown levelDropdown;
    [SerializeField] private Button startBattleButton;
    [SerializeField] private Button closeButton;

    private readonly List<int> levelIds = new List<int>();
    private const int Amount = 10;

    private void Awake()
    {
        CacheReferences();
        damageButton?.onClick.AddListener(DealDamage);
        healButton?.onClick.AddListener(HealPlayer);
        shieldButton?.onClick.AddListener(GainShield);
        drawCardButton?.onClick.AddListener(DrawCard);
        startBattleButton?.onClick.AddListener(StartSelectedBattle);
        closeButton?.onClick.AddListener(Hide);
    }

    private void OnEnable()
    {
        CacheReferences();
        PopulateLevelDropdown();
        RefreshBattleOnlyControls();
    }

    private void OnDisable()
    {
        RefreshBattleOnlyControls(false);
    }

    private void OnDestroy()
    {
        damageButton?.onClick.RemoveListener(DealDamage);
        healButton?.onClick.RemoveListener(HealPlayer);
        shieldButton?.onClick.RemoveListener(GainShield);
        drawCardButton?.onClick.RemoveListener(DrawCard);
        startBattleButton?.onClick.RemoveListener(StartSelectedBattle);
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
        if (drawCardButton == null)
            drawCardButton = transform.Find("DrawCardButton")?.GetComponent<Button>();

        if (closeButton == null)
            closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
    }

    private void PopulateLevelDropdown()
    {
        if (levelDropdown == null)
            return;

        levelIds.Clear();
        levelDropdown.ClearOptions();

        List<LevelData> levels = GameDataDatabase.LevelData.Values
            .Where(IsDebugBattleLevel)
            .OrderBy(level => level.numericId)
            .ToList();

        List<string> options = new List<string>(levels.Count);
        for (int i = 0; i < levels.Count; i++)
        {
            LevelData level = levels[i];
            levelIds.Add(level.numericId);
            options.Add(BuildLevelOptionText(level));
        }

        if (options.Count == 0)
        {
            options.Add("没有可用战斗关卡");
            startBattleButton?.gameObject.SetActive(false);
        }
        else
        {
            startBattleButton?.gameObject.SetActive(true);
        }

        levelDropdown.AddOptions(options);
        levelDropdown.SetValueWithoutNotify(0);
        levelDropdown.RefreshShownValue();
    }

    private void RefreshBattleOnlyControls(bool battleOnlyInteractable = true)
    {
        bool inBattle = battleOnlyInteractable && BattleManager.Instance != null && BattleManager.Instance.CurrentPhase != BattlePhase.None && BattleManager.Instance.CurrentPhase != BattlePhase.Finished;
        if (drawCardButton != null)
            drawCardButton.interactable = inBattle;
    }

    private static bool IsDebugBattleLevel(LevelData level)
    {
        if (level == null || (level.levelType != LevelType.Battle && level.levelType != LevelType.Elite))
            return false;

        return level.enemyIds?.Length > 0 || level.enemies?.Length > 0 || level.randomEnemyGroups?.Length > 0;
    }

    private static string BuildLevelOptionText(LevelData level)
    {
        string title = !string.IsNullOrEmpty(level.titleKey) ? LocalizationSystem.GetText(level.titleKey, level.id) : level.id;
        string enemies = BuildEnemySummary(level);
        return $"{level.numericId} {UIManager.GetLevelTypeName(level.levelType)} {title}：{enemies}";
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
            return LocalizationSystem.GetText(data.nameKey, data.Id);
        return enemyId > 0 ? enemyId.ToString() : "未知敌人";
    }

    private void StartSelectedBattle()
    {
        if (levelDropdown == null || handSystem == null || levelDropdown.value < 0 || levelDropdown.value >= levelIds.Count)
            return;

        if (GameDataDatabase.TryGetLevelData(levelIds[levelDropdown.value], out LevelData level))
            handSystem.DebugStartBattleLevel(level);
    }

    private void DealDamage()
    {
        handSystem?.DebugDealDamageToTarget(Amount);
    }

    private void HealPlayer()
    {
        handSystem?.DebugHealPlayer(Amount);
    }

    private void GainShield()
    {
        handSystem?.DebugGainPlayerShield(Amount);
    }

    private void DrawCard()
    {
        if (BattleManager.Instance == null || BattleManager.Instance.CurrentPhase == BattlePhase.None || BattleManager.Instance.CurrentPhase == BattlePhase.Finished)
            return;

        handSystem?.PlayerState?.DrawCards(1);
    }
}

