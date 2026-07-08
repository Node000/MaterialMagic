public class HealingCatEnemyModel : EnemyModel
{
    public HealingCatEnemyModel(EnemyData data) : base(data)
    {
    }

    public override string GetSpecialIntentDisplayValue(EnemyIntentData intent, PlayerState playerState)
    {
        return intent != null && intent.value == 1 ? "6" : string.Empty;
    }

    protected override string GetSpecialIntentTooltipTitle(EnemyIntentData intent)
    {
        return intent != null && intent.value == 1 ? GetLocalizedText("enemy.intent.healing_cat.title") : base.GetSpecialIntentTooltipTitle(intent);
    }

    protected override string GetSpecialIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        return intent != null && intent.value == 1 ? FormatLocalizedText("enemy.intent.healing_cat.desc", 6) : base.GetSpecialIntentTooltipDescription(intent, playerState);
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (value == 1)
            playerState?.Heal(6);
    }
}
