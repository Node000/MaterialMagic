public class DeerManEnemyModel : EnemyModel
{
    public DeerManEnemyModel(EnemyData data) : base(data)
    {
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (playerState == null)
            return;

        if (value == 1)
        {
            MaterialEnum material = SelectRandomBasicMaterial(playerState);
            int count = CountDeckMaterial(playerState, material);
            playerState.AddBuff(BuffEnum.AttributeDisabled, (int)material);
            GainShield(count * 2);
        }
        else if (value == 2)
        {
            int count = CountMostCommonBasicMaterial(playerState);
            playerState.TakeDamage(count * 3, new CombatantModel(this));
        }
    }

    private static MaterialEnum SelectRandomBasicMaterial(PlayerState playerState)
    {
        int index = playerState is PlayerStatus status ? status.NextRunRandomInt(1, 5) : UnityEngine.Random.Range(1, 5);
        return (MaterialEnum)index;
    }

    private static int CountDeckMaterial(PlayerState playerState, MaterialEnum material)
    {
        int count = 0;
        for (int i = 0; i < playerState.Deck.Count; i++)
        {
            MaterialModel card = playerState.Deck[i];
            if (card != null && card.material == material)
                count++;
        }
        return count;
    }

    private static int CountMostCommonBasicMaterial(PlayerState playerState)
    {
        int max = 0;
        for (int i = 1; i <= 4; i++)
        {
            int count = CountDeckMaterial(playerState, (MaterialEnum)i);
            if (count > max)
                max = count;
        }
        return max;
    }
}
