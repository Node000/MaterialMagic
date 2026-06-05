public class LuckyCatEnemyModel : EnemyModel
{
    public LuckyCatEnemyModel(EnemyData data) : base(data)
    {
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (value == 1)
            playerState?.AddGold(5);
    }
}
