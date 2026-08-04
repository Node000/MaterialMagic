using System;
using System.Collections.Generic;
using UnityEngine;

public enum ArrowUpgradeDirection
{
    Up,
    Down,
    Left,
    Right
}

[Serializable]
public class ArrowUpgradeState
{
    [SerializeField] private List<string> unlockedNodeIds = new List<string>();
    [SerializeField] private int pendingNextTurnShield;

    public IReadOnlyList<string> UnlockedNodeIds => unlockedNodeIds;
    public int PendingNextTurnShield => pendingNextTurnShield;

    public bool IsUnlocked(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && unlockedNodeIds.Contains(nodeId);
    }

    public bool Unlock(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || unlockedNodeIds.Contains(nodeId))
            return false;

        unlockedNodeIds.Add(nodeId);
        return true;
    }

    public void Restore(IEnumerable<string> nodeIds, int nextTurnShield)
    {
        unlockedNodeIds.Clear();
        if (nodeIds != null)
        {
            foreach (string nodeId in nodeIds)
            {
                if (!string.IsNullOrEmpty(nodeId) && !unlockedNodeIds.Contains(nodeId))
                    unlockedNodeIds.Add(nodeId);
            }
        }
        pendingNextTurnShield = Mathf.Max(0, nextTurnShield);
    }

    public void AddPendingNextTurnShield(int amount)
    {
        pendingNextTurnShield += Mathf.Max(0, amount);
    }

    public int ConsumePendingNextTurnShield()
    {
        int value = pendingNextTurnShield;
        pendingNextTurnShield = 0;
        return value;
    }
}

public sealed class ArrowUpgradeNodeDefinition
{
    public readonly string Id;
    public readonly string ParentId;
    public readonly ArrowUpgradeDirection? PageDirection;
    public readonly ArrowUpgradeDirection[] Requirement;
    public readonly string Description;
    public readonly bool IsBodyNode;

    public ArrowUpgradeNodeDefinition(string id, string parentId, ArrowUpgradeDirection? pageDirection, ArrowUpgradeDirection[] requirement, string description, bool isBodyNode = false)
    {
        Id = id;
        ParentId = parentId;
        PageDirection = pageDirection;
        Requirement = requirement ?? Array.Empty<ArrowUpgradeDirection>();
        Description = description;
        IsBodyNode = isBodyNode;
    }
}

public static class ArrowUpgradeSystem
{
    private const string ConfigResourcePath = "Config/ArrowUpgradeConfig";
    private static ArrowUpgradeConfig config;
    private static readonly ArrowUpgradeDirection[] AllDirections =
    {
        ArrowUpgradeDirection.Up,
        ArrowUpgradeDirection.Down,
        ArrowUpgradeDirection.Left,
        ArrowUpgradeDirection.Right
    };

    private static readonly ArrowUpgradeNodeDefinition[] nodes =
    {
        new ArrowUpgradeNodeDefinition("up_root", null, ArrowUpgradeDirection.Up, Array.Empty<ArrowUpgradeDirection>(), "激活上箭头强化树。"),
        new ArrowUpgradeNodeDefinition("up_up", "up_root", ArrowUpgradeDirection.Up, new[] { ArrowUpgradeDirection.Up, ArrowUpgradeDirection.Up }, "读取时额外造成2点伤害。"),
        new ArrowUpgradeNodeDefinition("up_down", "up_root", ArrowUpgradeDirection.Up, new[] { ArrowUpgradeDirection.Up, ArrowUpgradeDirection.Down }, "读取时施加1层燃烧。"),
        new ArrowUpgradeNodeDefinition("up_left", "up_root", ArrowUpgradeDirection.Up, new[] { ArrowUpgradeDirection.Up, ArrowUpgradeDirection.Left }, "抽到时造成2点伤害。"),
        new ArrowUpgradeNodeDefinition("up_right", "up_root", ArrowUpgradeDirection.Up, new[] { ArrowUpgradeDirection.Up, ArrowUpgradeDirection.Right }, "抽到时获得2点护盾。"),

        new ArrowUpgradeNodeDefinition("down_root", null, ArrowUpgradeDirection.Down, Array.Empty<ArrowUpgradeDirection>(), "激活下箭头强化树。"),
        new ArrowUpgradeNodeDefinition("down_up", "down_root", ArrowUpgradeDirection.Down, new[] { ArrowUpgradeDirection.Down, ArrowUpgradeDirection.Up }, "进入弃牌堆时造成2点伤害。"),
        new ArrowUpgradeNodeDefinition("down_down", "down_root", ArrowUpgradeDirection.Down, new[] { ArrowUpgradeDirection.Down, ArrowUpgradeDirection.Down }, "读取时随机施加1层电荷、燃烧或易损。"),
        new ArrowUpgradeNodeDefinition("down_left", "down_root", ArrowUpgradeDirection.Down, new[] { ArrowUpgradeDirection.Down, ArrowUpgradeDirection.Left }, "抽到时施加1层易损。"),
        new ArrowUpgradeNodeDefinition("down_right", "down_root", ArrowUpgradeDirection.Down, new[] { ArrowUpgradeDirection.Down, ArrowUpgradeDirection.Right }, "进入弃牌堆时施加1层易损。"),

        new ArrowUpgradeNodeDefinition("left_root", null, ArrowUpgradeDirection.Left, Array.Empty<ArrowUpgradeDirection>(), "激活左箭头强化树。"),
        new ArrowUpgradeNodeDefinition("left_up", "left_root", ArrowUpgradeDirection.Left, new[] { ArrowUpgradeDirection.Left, ArrowUpgradeDirection.Up }, "抽到时造成2点伤害。"),
        new ArrowUpgradeNodeDefinition("left_down", "left_root", ArrowUpgradeDirection.Left, new[] { ArrowUpgradeDirection.Left, ArrowUpgradeDirection.Down }, "抽到时施加1层易损。"),
        new ArrowUpgradeNodeDefinition("left_left", "left_root", ArrowUpgradeDirection.Left, new[] { ArrowUpgradeDirection.Left, ArrowUpgradeDirection.Left }, "抽到时立刻再抽1张箭头。"),
        new ArrowUpgradeNodeDefinition("left_right", "left_root", ArrowUpgradeDirection.Left, new[] { ArrowUpgradeDirection.Left, ArrowUpgradeDirection.Right }, "抽到时获得2点护盾。"),

        new ArrowUpgradeNodeDefinition("right_root", null, ArrowUpgradeDirection.Right, Array.Empty<ArrowUpgradeDirection>(), "激活右箭头强化树。"),
        new ArrowUpgradeNodeDefinition("right_up", "right_root", ArrowUpgradeDirection.Right, new[] { ArrowUpgradeDirection.Right, ArrowUpgradeDirection.Up }, "读取时自己受到1点伤害，并造成4点伤害。"),
        new ArrowUpgradeNodeDefinition("right_down", "right_root", ArrowUpgradeDirection.Right, new[] { ArrowUpgradeDirection.Right, ArrowUpgradeDirection.Down }, "读取时获得2层荆棘。"),
        new ArrowUpgradeNodeDefinition("right_left", "right_root", ArrowUpgradeDirection.Right, new[] { ArrowUpgradeDirection.Right, ArrowUpgradeDirection.Left }, "抽到时获得保留；回合结束时未打出则获得1点护盾。"),
        new ArrowUpgradeNodeDefinition("right_right", "right_root", ArrowUpgradeDirection.Right, new[] { ArrowUpgradeDirection.Right, ArrowUpgradeDirection.Right }, "读取时安排下回合开始获得2点护盾。"),

        new ArrowUpgradeNodeDefinition("body_draw_1", null, null, AllDirections, "每回合抽牌数+1。", true),
        new ArrowUpgradeNodeDefinition("body_refresh", "body_draw_1", null, AllDirections, "本局总换牌次数+1。", true),
        new ArrowUpgradeNodeDefinition("body_draw_2", "body_refresh", null, AllDirections, "每回合抽牌数+1。", true)
    };

    public static IReadOnlyList<ArrowUpgradeNodeDefinition> Nodes => nodes;

    public static ArrowUpgradeNodeDefinition GetNode(string nodeId)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].Id == nodeId)
                return nodes[i];
        }
        return null;
    }

    public static int GetEffectValue(string nodeId, int fallback)
    {
        if (config == null)
            config = Resources.Load<ArrowUpgradeConfig>(ConfigResourcePath);
        return config != null ? config.GetPrimaryValue(nodeId, fallback) : fallback;
    }

    public static int GetSecondaryEffectValue(string nodeId, int fallback)
    {
        if (config == null)
            config = Resources.Load<ArrowUpgradeConfig>(ConfigResourcePath);
        return config != null ? config.GetSecondaryValue(nodeId, fallback) : fallback;
    }

    public static bool TryGetDirection(MaterialEnum material, out ArrowUpgradeDirection direction)
    {
        switch (material)
        {
            case MaterialEnum.Fire: direction = ArrowUpgradeDirection.Up; return true;
            case MaterialEnum.Water: direction = ArrowUpgradeDirection.Down; return true;
            case MaterialEnum.Wind: direction = ArrowUpgradeDirection.Left; return true;
            case MaterialEnum.Earth: direction = ArrowUpgradeDirection.Right; return true;
            default: direction = default; return false;
        }
    }

    public static bool IsNodeAvailable(PlayerState player, ArrowUpgradeNodeDefinition node)
    {
        if (player == null || node == null || player.ArrowUpgrades.IsUnlocked(node.Id))
            return false;
        if (!string.IsNullOrEmpty(node.ParentId) && !player.ArrowUpgrades.IsUnlocked(node.ParentId))
            return false;
        if (node.IsBodyNode)
            return true;
        if (!node.PageDirection.HasValue || node.Requirement.Length == 0)
            return true;

        string prefix = node.PageDirection.Value.ToString().ToLowerInvariant() + "_";
        for (int i = 0; i < nodes.Length; i++)
        {
            ArrowUpgradeNodeDefinition sibling = nodes[i];
            if (sibling.Id != node.Id && sibling.Id.StartsWith(prefix, StringComparison.Ordinal) && sibling.Requirement.Length > 0 && player.ArrowUpgrades.IsUnlocked(sibling.Id))
                return false;
        }
        return true;
    }

    public static bool MeetsRequirement(ArrowUpgradeNodeDefinition node, IReadOnlyList<MagicModel> selectedMagics)
    {
        if (node == null)
            return false;
        if (node.Requirement.Length == 0)
            return true;

        int[] required = new int[4];
        int[] provided = new int[4];
        for (int i = 0; i < node.Requirement.Length; i++)
            required[(int)node.Requirement[i]]++;
        for (int i = 0; selectedMagics != null && i < selectedMagics.Count; i++)
        {
            MaterialEnum[] recipe = selectedMagics[i]?.Data?.recipe;
            for (int recipeIndex = 0; recipe != null && recipeIndex < recipe.Length; recipeIndex++)
            {
                if (TryGetDirection(recipe[recipeIndex], out ArrowUpgradeDirection direction))
                    provided[(int)direction]++;
            }
        }
        for (int i = 0; i < required.Length; i++)
        {
            if (provided[i] < required[i])
                return false;
        }
        return true;
    }

    public static bool TryUnlock(PlayerState player, string nodeId, IReadOnlyList<MagicModel> selectedMagics)
    {
        ArrowUpgradeNodeDefinition node = GetNode(nodeId);
        if (!IsNodeAvailable(player, node) || !MeetsRequirement(node, selectedMagics))
            return false;

        if (node.Requirement.Length > 0 && !player.CanConsumeArrowUpgradeMagics(selectedMagics))
            return false;

        if (!player.ArrowUpgrades.Unlock(node.Id))
            return false;

        if (node.Requirement.Length > 0)
            player.ConsumeArrowUpgradeMagics(selectedMagics);

        if (node.Id == "body_draw_1" || node.Id == "body_draw_2")
            player.DrawCount += GetEffectValue(node.Id, 1);
        else if (node.Id == "body_refresh")
            player.AddPermanentRefreshChance(GetEffectValue(node.Id, 1));
        return true;
    }

    public static bool IsDirectionRootUnlocked(PlayerState player, MaterialEnum material, out ArrowUpgradeDirection direction)
    {
        if (player != null && TryGetDirection(material, out direction))
            return player.ArrowUpgrades.IsUnlocked(direction.ToString().ToLowerInvariant() + "_root");

        direction = default;
        return false;
    }

    public static string GetDirectionText(ArrowUpgradeDirection direction)
    {
        switch (direction)
        {
            case ArrowUpgradeDirection.Up: return LocalizationSystem.GetText("arrow_upgrade.direction.up", "上");
            case ArrowUpgradeDirection.Down: return LocalizationSystem.GetText("arrow_upgrade.direction.down", "下");
            case ArrowUpgradeDirection.Left: return LocalizationSystem.GetText("arrow_upgrade.direction.left", "左");
            default: return LocalizationSystem.GetText("arrow_upgrade.direction.right", "右");
        }
    }

    public static string GetNodeDescription(ArrowUpgradeNodeDefinition node)
    {
        if (node == null)
            return string.Empty;

        string description = LocalizationSystem.GetText("arrow_upgrade." + node.Id + ".description", node.Description);
        return string.Format(description, GetEffectValue(node.Id, 1), GetSecondaryEffectValue(node.Id, 0));
    }

    public static void TriggerOnDraw(PlayerState player, MaterialModel card, BattleManager battleManager)
    {
        if (player == null || card == null || !TryGetDirection(card.material, out ArrowUpgradeDirection direction))
            return;

        switch (direction)
        {
            case ArrowUpgradeDirection.Up:
                if (player.ArrowUpgrades.IsUnlocked("up_left"))
                    DealDamage(player, battleManager, GetEffectValue("up_left", 2));
                else if (player.ArrowUpgrades.IsUnlocked("up_right"))
                    player.GainShield(GetEffectValue("up_right", 2));
                break;
            case ArrowUpgradeDirection.Down:
                if (player.ArrowUpgrades.IsUnlocked("down_left"))
                    ApplyBuff(player, battleManager, BuffEnum.Vulnerable, GetEffectValue("down_left", 1));
                break;
            case ArrowUpgradeDirection.Left:
                if (player.ArrowUpgrades.IsUnlocked("left_up"))
                    DealDamage(player, battleManager, GetEffectValue("left_up", 2));
                else if (player.ArrowUpgrades.IsUnlocked("left_down"))
                    ApplyBuff(player, battleManager, BuffEnum.Vulnerable, GetEffectValue("left_down", 1));
                else if (player.ArrowUpgrades.IsUnlocked("left_left"))
                    player.DrawArrowUpgradeBonusCard(GetEffectValue("left_left", 1));
                else if (player.ArrowUpgrades.IsUnlocked("left_right"))
                    player.GainShield(GetEffectValue("left_right", 2));
                break;
            case ArrowUpgradeDirection.Right:
                if (player.ArrowUpgrades.IsUnlocked("right_left") && !card.HasModifier<ArrowUpgradeRetainedModifier>())
                {
                    ArrowUpgradeRetainedModifier modifier = new ArrowUpgradeRetainedModifier();
                    modifier.MarkRemoveAfterBattle();
                    card.AddModifier(modifier);
                }
                break;
        }
    }

    public static void TriggerOnDiscard(PlayerState player, MaterialModel card, BattleManager battleManager)
    {
        if (player == null || card == null || !TryGetDirection(card.material, out ArrowUpgradeDirection direction) || direction != ArrowUpgradeDirection.Down)
            return;

        if (player.ArrowUpgrades.IsUnlocked("down_up"))
            DealDamage(player, battleManager, GetEffectValue("down_up", 2));
        else if (player.ArrowUpgrades.IsUnlocked("down_right"))
            ApplyBuff(player, battleManager, BuffEnum.Vulnerable, GetEffectValue("down_right", 1));
    }

    public static void TriggerOnRead(PlayerState player, MaterialModel card, BattleManager battleManager)
    {
        if (player == null || card == null || !TryGetDirection(card.material, out ArrowUpgradeDirection direction))
            return;

        switch (direction)
        {
            case ArrowUpgradeDirection.Up:
                if (player.ArrowUpgrades.IsUnlocked("up_up"))
                    DealDamage(player, battleManager, GetEffectValue("up_up", 2));
                else if (player.ArrowUpgrades.IsUnlocked("up_down"))
                    ApplyBuff(player, battleManager, BuffEnum.Burning, GetEffectValue("up_down", 1));
                break;
            case ArrowUpgradeDirection.Down:
                if (player.ArrowUpgrades.IsUnlocked("down_down"))
                {
                    BuffEnum[] options = { BuffEnum.Arc, BuffEnum.Burning, BuffEnum.Vulnerable };
                    int index = player is PlayerStatus status ? status.NextRunRandomInt(0, options.Length) : UnityEngine.Random.Range(0, options.Length);
                    ApplyBuff(player, battleManager, options[index], GetEffectValue("down_down", 1));
                }
                break;
            case ArrowUpgradeDirection.Right:
                if (player.ArrowUpgrades.IsUnlocked("right_up"))
                {
                    player.TakeDirectDamage(GetEffectValue("right_up", 1));
                    DealDamage(player, battleManager, GetSecondaryEffectValue("right_up", 4));
                }
                else if (player.ArrowUpgrades.IsUnlocked("right_down"))
                {
                    player.AddBuff(BuffEnum.Thorns, GetEffectValue("right_down", 2));
                }
                else if (player.ArrowUpgrades.IsUnlocked("right_right"))
                {
                    player.ArrowUpgrades.AddPendingNextTurnShield(GetEffectValue("right_right", 2));
                }
                break;
        }
    }

    public static void TriggerTurnStart(PlayerState player)
    {
        if (player == null)
            return;

        int shield = player.ArrowUpgrades.ConsumePendingNextTurnShield();
        if (shield > 0)
            player.GainShield(shield);
    }

    public static void TriggerEndTurn(PlayerState player)
    {
        if (player == null || !player.ArrowUpgrades.IsUnlocked("right_left"))
            return;

        for (int i = 0; i < player.Hand.Count; i++)
        {
            MaterialModel card = player.Hand[i];
            if (card != null && card.HasModifier<ArrowUpgradeRetainedModifier>())
                player.GainShield(GetEffectValue("right_left", 1));
        }
    }

    private static void DealDamage(PlayerState player, BattleManager battleManager, int damage)
    {
        EnemyModel target = battleManager != null ? battleManager.GetTargetEnemy() : null;
        if (target == null || damage <= 0)
            return;

        CombatantModel targetCombatant = new CombatantModel(target);
        CombatantModel source = new CombatantModel(player);
        int attackValue = damage;
        player.TriggerOnAttack(targetCombatant, ref attackValue);
        CombatDamageResult result = target.TakeDamageResult(attackValue, source);
        int attackResult = result.HealthDamage;
        player.TriggerAfterAttack(targetCombatant, ref attackResult);
    }

    private static void ApplyBuff(PlayerState player, BattleManager battleManager, BuffEnum buff, int stack)
    {
        EnemyModel target = battleManager != null ? battleManager.GetTargetEnemy() : null;
        if (target != null && stack > 0)
            target.AddBuff(buff, stack, new CombatantModel(player));
    }
}
