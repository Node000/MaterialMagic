public class DemonEnemyModel : EnemyModel
{
    public DemonEnemyModel(EnemyData data) : base(data)
    {
    }

    protected override string GetSpecialIntentTooltipTitle(EnemyIntentData intent)
    {
        return intent != null && intent.value == 1 ? "意图：诅咒" : base.GetSpecialIntentTooltipTitle(intent);
    }

    protected override string GetSpecialIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        return intent != null && intent.value == 1 ? "这个敌人将对玩家施加" + FormatBuffStack(BuffEnum.Curse, 1) : base.GetSpecialIntentTooltipDescription(intent, playerState);
    }

    public override System.Collections.Generic.IReadOnlyList<BuffStackData> GetIntentTooltipBuffs(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent != null && intent.value == 1)
            return new[] { new BuffStackData { buffType = BuffEnum.Curse, stack = 1 } };
        return base.GetIntentTooltipBuffs(intent, playerState);
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (value == 1)
            playerState?.AddBuff(BuffEnum.Curse, 1);
    }
}
