public class LuckyCatEnemyModel : EnemyModel
{
    public LuckyCatEnemyModel(EnemyData data) : base(data)
    {
    }

    public override string GetSpecialIntentDisplayValue(EnemyIntentData intent, PlayerState playerState)
    {
        return intent != null && intent.value == 1 ? "5" : string.Empty;
    }

    protected override string GetSpecialIntentTooltipTitle(EnemyIntentData intent)
    {
        return intent != null && intent.value == 1 ? GetLocalizedText("enemy.intent.lucky_cat.title") : base.GetSpecialIntentTooltipTitle(intent);
    }

    protected override string GetSpecialIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        return intent != null && intent.value == 1 ? FormatLocalizedText("enemy.intent.lucky_cat.desc", 5) : base.GetSpecialIntentTooltipDescription(intent, playerState);
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (value == 1)
            playerState?.AddGold(5);
    }
}
