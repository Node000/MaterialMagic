public class DemonEnemyModel : EnemyModel
{
    public DemonEnemyModel(EnemyData data) : base(data)
    {
    }

    protected override string GetSpecialIntentTooltipTitle(EnemyIntentData intent)
    {
        return intent != null && intent.value == 1 ? GetLocalizedText("enemy.intent.demon.title") : base.GetSpecialIntentTooltipTitle(intent);
    }

    protected override string GetSpecialIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        return intent != null && intent.value == 1 ? FormatLocalizedText("enemy.intent.demon.desc", FormatBuffStack(BuffEnum.Curse, 1)) : base.GetSpecialIntentTooltipDescription(intent, playerState);
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
