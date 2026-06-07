public class DeerManEnemyModel : EnemyModel
{
    private MaterialEnum pendingDisabledMaterial;
    private int pendingDisabledActionIndex = -1;
    private int pendingDisabledPhase = -1;

    public DeerManEnemyModel(EnemyData data) : base(data)
    {
    }

    public override string GetSpecialIntentDisplayValue(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null || playerState == null)
            return string.Empty;

        if (intent.value == 1)
        {
            MaterialEnum material = GetPendingDisabledMaterial(playerState);
            int count = CountDeckMaterial(playerState, material);
            return GetSpecialShieldPreviewValue(count * 2).ToString();
        }

        if (intent.value == 2)
        {
            int count = CountMostCommonBasicMaterial(playerState);
            return GetSpecialDamagePreviewValue(count * 3, playerState).ToString();
        }

        return string.Empty;
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (playerState == null)
            return;

        if (value == 1)
        {
            MaterialEnum material = GetPendingDisabledMaterial(playerState);
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

    private MaterialEnum GetPendingDisabledMaterial(PlayerState playerState)
    {
        if (pendingDisabledActionIndex != ActionIndex || pendingDisabledPhase != Phase || pendingDisabledMaterial == MaterialEnum.None)
        {
            pendingDisabledMaterial = SelectRandomBasicMaterial(playerState);
            pendingDisabledActionIndex = ActionIndex;
            pendingDisabledPhase = Phase;
        }
        return pendingDisabledMaterial;
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
