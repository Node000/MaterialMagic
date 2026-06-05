public class DemonEnemyModel : EnemyModel
{
    public DemonEnemyModel(EnemyData data) : base(data)
    {
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (value == 1)
            playerState?.AddBuff(BuffEnum.Curse, 1);
    }
}
