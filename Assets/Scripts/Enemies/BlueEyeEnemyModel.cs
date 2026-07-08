public class BlueEyeEnemyModel : EnemyModel
{
    private int summonCount = 1;

    public BlueEyeEnemyModel(EnemyData data) : base(data)
    {
    }

    public override string GetSpecialIntentDisplayValue(EnemyIntentData intent, PlayerState playerState)
    {
        return intent != null && intent.value == 1 && summonCount > 1 ? "×" + summonCount : string.Empty;
    }

    protected override string GetSpecialIntentTooltipTitle(EnemyIntentData intent)
    {
        return intent != null && intent.value == 1 ? GetLocalizedText("enemy.intent.title.summon") : base.GetSpecialIntentTooltipTitle(intent);
    }

    protected override string GetSpecialIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null || intent.value != 1)
            return base.GetSpecialIntentTooltipDescription(intent, playerState);

        string summonText = summonCount > 1 ? FormatLocalizedText("enemy.intent.blue_eye.minion_multi", summonCount) : GetLocalizedText("enemy.intent.blue_eye.minion_single");
        return FormatLocalizedText("enemy.intent.blue_eye.summon_desc", summonText, FormatBuffStack(BuffEnum.DefensePower, 2));
    }

    public override System.Collections.Generic.IReadOnlyList<BuffStackData> GetIntentTooltipBuffs(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent != null && intent.value == 1)
            return new[] { new BuffStackData { buffType = BuffEnum.DefensePower, stack = 2 } };
        return base.GetIntentTooltipBuffs(intent, playerState);
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (value != 1)
            return;

        BattleManager manager = BattleManager.Instance;
        if (manager != null)
        {
            for (int i = 0; i < summonCount; i++)
                manager.SpawnMinion(22, this, i, summonCount);
        }
        summonCount++;
        AddBuff(BuffEnum.DefensePower, 2);
    }
}
