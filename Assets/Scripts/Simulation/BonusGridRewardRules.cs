using System;

/// <summary>Committed player values for feedback; configured reward amount can differ from actual gain.</summary>
public sealed class BonusGridRewardResult
{
    public BonusGridReward Reward { get; }
    public int GoldBefore { get; }
    public int GoldAfter { get; }
    public int HealthBefore { get; }
    public int HealthAfter { get; }
    public int GoldGained => GoldAfter - GoldBefore;
    public int HealthGained => HealthAfter - HealthBefore;
    internal PlayerState Player { get; }

    internal BonusGridRewardResult(BonusGridReward reward, PlayerState player, int goldBefore, int healthBefore)
    {
        Reward = reward;
        Player = player;
        GoldBefore = goldBefore;
        GoldAfter = player.Gold;
        HealthBefore = healthBefore;
        HealthAfter = player.CurrentHealth;
    }
}

/// <summary>Reward submission without a UI or coroutine. Feedback must never apply the reward again.</summary>
public static class BonusGridRewardRules
{
    /// <summary>
    /// Commits a step's newly collected reward at most once. Gold is difficulty-adjusted once, then
    /// AddGold(..., false) applies the existing relic bonus once for the whole gain, not per flying coin.
    /// A modifier can be injected for deterministic tools/tests; null uses the current run difficulty.
    /// allowNonPositiveGold preserves the legacy ownerless UI fallback; normal reward UI ignores it.
    /// Disable playGoldFeedback when a view will play the single committed gain at its original cue.
    /// </summary>
    public static BonusGridRewardResult Apply(BonusGridStepResult step, PlayerState player,
        Func<int, int> modifyGoldGain = null, bool allowNonPositiveGold = false, bool playGoldFeedback = true)
    {
        if (step == null) throw new ArgumentNullException(nameof(step));
        if (player == null) throw new ArgumentNullException(nameof(player));
        using (player.Rules.Enter()) return ApplyCore(step, player, modifyGoldGain, allowNonPositiveGold, playGoldFeedback);
    }

    private static BonusGridRewardResult ApplyCore(BonusGridStepResult step, PlayerState player,
        Func<int, int> modifyGoldGain, bool allowNonPositiveGold, bool playGoldFeedback)
    {
        if (step.AppliedReward != null)
        {
            if (!ReferenceEquals(step.AppliedReward.Player, player))
                throw new InvalidOperationException("A bonus-grid step cannot reward two different players.");
            return step.AppliedReward;
        }

        if (step.IsApplyingReward)
            throw new InvalidOperationException("Cannot reenter bonus-grid reward submission for the same step.");

        int goldBefore = player.Gold;
        int healthBefore = player.CurrentHealth;
        BonusGridReward reward = step.CollectedReward;
        step.IsApplyingReward = true;
        try
        {
            if (reward != null)
            {
                switch (reward.RewardType)
                {
                    case BonusRewardType.Gold:
                        int amount = reward.Amount;
                        if (amount > 0)
                            amount = modifyGoldGain != null ? modifyGoldGain(amount) : DifficultyUpgradeSystem.ModifyGoldGain(amount);
                        if (amount > 0 || allowNonPositiveGold)
                            player.AddGold(amount, false, playFeedback: playGoldFeedback);
                        break;
                    case BonusRewardType.Heal:
                        player.Heal(reward.Amount);
                        break;
                }
            }
        }
        finally
        {
            // Even if a host observer throws after mutation, this collected cell cannot pay twice.
            step.AppliedReward = new BonusGridRewardResult(reward, player, goldBefore, healthBefore);
            step.IsApplyingReward = false;
        }
        return step.AppliedReward;
    }
}
