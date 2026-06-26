public class DeerManEnemyModel : EnemyModel
{
    private MaterialEnum pendingDisabledMaterial;
    private MaterialEnum lastDisabledMaterial;
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

    protected override string GetSpecialIntentTooltipTitle(EnemyIntentData intent)
    {
        if (intent != null && intent.value == 1)
            return "意图：特殊防御";
        if (intent != null && intent.value == 2)
            return "意图：特殊攻击";
        return base.GetSpecialIntentTooltipTitle(intent);
    }

    protected override string GetSpecialIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null)
            return string.Empty;

        if (intent.value == 1)
        {
            string displayValue = GetSpecialIntentDisplayValue(intent, playerState);
            string disabledBuff = FormatBuffStack(BuffEnum.AttributeDisabled, (int)GetPendingDisabledMaterial(playerState));
            return !string.IsNullOrEmpty(displayValue) ? $"这个敌人将获得{displayValue}点护盾，并施加{disabledBuff}" : $"这个敌人将获得护盾，并施加{disabledBuff}";
        }

        if (intent.value == 2)
        {
            string displayValue = GetSpecialIntentDisplayValue(intent, playerState);
            return !string.IsNullOrEmpty(displayValue) ? $"这个敌人将造成{displayValue}点伤害，伤害基于玩家最多的基础素材数量" : "这个敌人将造成特殊伤害，伤害基于玩家最多的基础素材数量";
        }

        return base.GetSpecialIntentTooltipDescription(intent, playerState);
    }

    public override System.Collections.Generic.IReadOnlyList<BuffStackData> GetIntentTooltipBuffs(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent != null && intent.value == 1 && playerState != null)
            return new[] { new BuffStackData { buffType = BuffEnum.AttributeDisabled, stack = (int)GetPendingDisabledMaterial(playerState) } };
        return base.GetIntentTooltipBuffs(intent, playerState);
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
            pendingDisabledMaterial = SelectRandomBasicMaterial(playerState, lastDisabledMaterial);
            lastDisabledMaterial = pendingDisabledMaterial;
            pendingDisabledActionIndex = ActionIndex;
            pendingDisabledPhase = Phase;
        }
        return pendingDisabledMaterial;
    }

    private static MaterialEnum SelectRandomBasicMaterial(PlayerState playerState, MaterialEnum excludeMaterial)
    {
        int materialCount = 4;
        int first = playerState is PlayerStatus status ? status.NextRunRandomInt(1, materialCount + 1) : UnityEngine.Random.Range(1, materialCount + 1);
        MaterialEnum result = (MaterialEnum)first;
        if (excludeMaterial == MaterialEnum.None || materialCount <= 1 || result != excludeMaterial)
            return result;

        int rerolled = first % materialCount + 1;
        return (MaterialEnum)rerolled;
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
