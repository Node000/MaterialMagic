public class MerchantEnemyModel : EnemyModel
{
    public MerchantEnemyModel(EnemyData data) : base(data)
    {
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
