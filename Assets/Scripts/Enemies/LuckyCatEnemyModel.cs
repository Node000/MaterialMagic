public class LuckyCatEnemyModel : EnemyModel
{
    public LuckyCatEnemyModel(EnemyData data) : base(data)
    {
    }

    public override string GetSpecialIntentDisplayValue(EnemyIntentData intent, PlayerState playerState)
    {
        return intent != null && intent.value == 1 ? "5" : string.Empty;
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (value == 1)
            playerState?.AddGold(5);
    }
}
