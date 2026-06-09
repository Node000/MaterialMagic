public class BlueEyeEnemyModel : EnemyModel
{
    private int summonCount = 1;

    public BlueEyeEnemyModel(EnemyData data) : base(data)
    {
    }

    public override string GetSpecialIntentDisplayValue(EnemyIntentData intent, PlayerState playerState)
    {
        return intent != null && intent.value == 1 && summonCount > 1 ? "×" + summonCount : string.Empty;
    }

    protected override string GetSpecialIntentTooltipTitle(EnemyIntentData intent)
    {
        return intent != null && intent.value == 1 ? "意图：召唤" : base.GetSpecialIntentTooltipTitle(intent);
    }

    protected override string GetSpecialIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null || intent.value != 1)
            return base.GetSpecialIntentTooltipDescription(intent, playerState);

        string summonText = summonCount > 1 ? summonCount + "个爪牙" : "爪牙";
        return $"这个敌人将召唤{summonText}，并获得{FormatBuffStack(BuffEnum.DefensePower, 2)}";
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (value != 1)
            return;

        BattleManager manager = BattleManager.Instance;
        if (manager != null)
        {
            for (int i = 0; i < summonCount; i++)
                manager.SpawnMinion(22, this, i, summonCount);
        }
        summonCount++;
        AddBuff(BuffEnum.DefensePower, 2);
    }
}
