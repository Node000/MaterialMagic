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

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (value != 1)
            return;

        BattleManager manager = BattleManager.Instance;
        if (manager != null)
        {
            for (int i = 0; i < summonCount; i++)
                manager.SpawnEnemy(22);
        }
        summonCount++;
        AddBuff(BuffEnum.DefensePower, 2);
    }
}
