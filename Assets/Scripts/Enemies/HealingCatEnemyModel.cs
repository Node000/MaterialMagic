public class HealingCatEnemyModel : EnemyModel
{
    public HealingCatEnemyModel(EnemyData data) : base(data)
    {
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (value == 1)
            playerState?.Heal(6);
    }
}
