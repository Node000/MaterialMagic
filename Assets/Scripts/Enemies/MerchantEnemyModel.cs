public class MerchantEnemyModel : EnemyModel
{
    public MerchantEnemyModel(EnemyData data) : base(data)
    {
    }

    protected override string GetSpecialIntentTooltipTitle(EnemyIntentData intent)
    {
        return intent != null && intent.value == 1 ? "意图：净化" : base.GetSpecialIntentTooltipTitle(intent);
    }

    protected override string GetSpecialIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        return intent != null && intent.value == 1 ? "这个敌人将清除自身所有负面状态" : base.GetSpecialIntentTooltipDescription(intent, playerState);
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (value != 1)
            return;

        ConsumeDebuff(BuffEnum.Weak);
        ConsumeDebuff(BuffEnum.Vulnerable);
        ConsumeDebuff(BuffEnum.Slow);
        ConsumeDebuff(BuffEnum.Arc);
        ConsumeDebuff(BuffEnum.Burning);
        ConsumeDebuff(BuffEnum.BurningNextTurn);
        ConsumeDebuff(BuffEnum.BurnOnAttack);
    }

    private void ConsumeDebuff(BuffEnum buffType)
    {
        int stack = GetBuffStack(buffType);
        if (stack > 0)
            ConsumeBuff(buffType, stack);
    }
}
