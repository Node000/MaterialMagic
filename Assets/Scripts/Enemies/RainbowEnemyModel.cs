public class RainbowEnemyModel : EnemyModel
{
    private const int DirectionDamageIntentValue = 1;
    private const int DirectionWeakIntentValue = 2;
    private const int DirectionDrawIntentValue = 3;
    private const int DirectionShieldIntentValue = 4;
    private const int RandomFollowupIntentValue = 5;

    private int randomFollowupDisplayType;

    public RainbowEnemyModel(EnemyData data) : base(data)
    {
    }

    protected override void OnCurrentIntentsUpdated()
    {
        randomFollowupDisplayType = 0;
        for (int i = 0; i < CurrentIntents.Count; i++)
        {
            EnemyIntentData intent = CurrentIntents[i];
            if (intent != null && intent.actionType == EnemyActionType.Special && intent.value == RandomFollowupIntentValue)
            {
                randomFollowupDisplayType = NextRandomInt(1, 5);
                intent.displayType = GetRandomFollowupDisplayType(randomFollowupDisplayType);
            }
        }
    }

    public override string GetSpecialIntentDisplayValue(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null)
            return string.Empty;

        switch (intent.value)
        {
            case DirectionDamageIntentValue:
                return "3";
            case DirectionWeakIntentValue:
                return "2";
            case DirectionDrawIntentValue:
                return "1";
            case DirectionShieldIntentValue:
                return "3";
            case RandomFollowupIntentValue:
                return GetRandomFollowupDisplayValue();
            default:
                return string.Empty;
        }
    }

    protected override string GetSpecialIntentTooltipTitle(EnemyIntentData intent)
    {
        return intent != null && intent.value == RandomFollowupIntentValue ? GetRandomFollowupTooltipTitle() : GetLocalizedText("enemy.intent.rainbow.direction_title");
    }

    protected override string GetSpecialIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null)
            return string.Empty;

        switch (intent.value)
        {
            case DirectionDamageIntentValue:
                return FormatLocalizedText("enemy.intent.rainbow.player_gain", FormatBuffStack(BuffEnum.DirectionDamageBonus, 3));
            case DirectionWeakIntentValue:
                return FormatLocalizedText("enemy.intent.rainbow.player_gain", FormatBuffStack(BuffEnum.DirectionWeakBonus, 2));
            case DirectionDrawIntentValue:
                return FormatLocalizedText("enemy.intent.rainbow.player_gain", FormatBuffStack(BuffEnum.DirectionExtraDraw, 1));
            case DirectionShieldIntentValue:
                return FormatLocalizedText("enemy.intent.rainbow.player_gain", FormatBuffStack(BuffEnum.DirectionShieldBonus, 3));
            case RandomFollowupIntentValue:
                return GetRandomFollowupTooltipDescription();
            default:
                return base.GetSpecialIntentTooltipDescription(intent, playerState);
        }
    }

    public override System.Collections.Generic.IReadOnlyList<BuffStackData> GetIntentTooltipBuffs(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null)
            return base.GetIntentTooltipBuffs(intent, playerState);

        switch (intent.value)
        {
            case DirectionDamageIntentValue:
                return new[] { new BuffStackData { buffType = BuffEnum.DirectionDamageBonus, stack = 3 } };
            case DirectionWeakIntentValue:
                return new[] { new BuffStackData { buffType = BuffEnum.DirectionWeakBonus, stack = 2 } };
            case DirectionDrawIntentValue:
                return new[] { new BuffStackData { buffType = BuffEnum.DirectionExtraDraw, stack = 1 } };
            case DirectionShieldIntentValue:
                return new[] { new BuffStackData { buffType = BuffEnum.DirectionShieldBonus, stack = 3 } };
            case RandomFollowupIntentValue:
                if (randomFollowupDisplayType == 3)
                    return new[] { new BuffStackData { buffType = BuffEnum.Weak, stack = 5 } };
                if (randomFollowupDisplayType == 4)
                    return new[] { new BuffStackData { buffType = BuffEnum.Vulnerable, stack = 6 } };
                break;
        }
        return base.GetIntentTooltipBuffs(intent, playerState);
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (playerState == null)
            return;

        switch (value)
        {
            case DirectionDamageIntentValue:
                playerState.AddBuff(BuffEnum.DirectionDamageBonus, 3);
                break;
            case DirectionWeakIntentValue:
                playerState.AddBuff(BuffEnum.DirectionWeakBonus, 2);
                break;
            case DirectionDrawIntentValue:
                playerState.AddBuff(BuffEnum.DirectionExtraDraw, 1);
                break;
            case DirectionShieldIntentValue:
                playerState.AddBuff(BuffEnum.DirectionShieldBonus, 3);
                break;
            case RandomFollowupIntentValue:
                ResolveRandomFollowup(playerState);
                break;
        }
    }

    private void ResolveRandomFollowup(PlayerState playerState)
    {
        if (randomFollowupDisplayType == 0)
            randomFollowupDisplayType = NextRandomInt(1, 5);

        switch (randomFollowupDisplayType)
        {
            case 1:
                playerState.TakeDamage(14, new CombatantModel(this));
                break;
            case 2:
                GainShield(10);
                break;
            case 3:
                playerState.AddBuff(BuffEnum.Weak, 5);
                break;
            default:
                playerState.AddBuff(BuffEnum.Vulnerable, 6);
                break;
        }

    }

    private string GetRandomFollowupDisplayValue()
    {
        switch (randomFollowupDisplayType)
        {
            case 1:
                return "14";
            case 2:
                return "10";
            case 3:
                return "5";
            case 4:
                return "6";
            default:
                return string.Empty;
        }
    }

    private string GetRandomFollowupDisplayType(int value)
    {
        switch (value)
        {
            case 1:
                return "bigAttack";
            case 2:
                return "bigDefend";
            case 3:
            case 4:
                return "debuff";
            default:
                return "spAttack";
        }
    }

    private string GetRandomFollowupTooltipTitle()
    {
        switch (randomFollowupDisplayType)
        {
            case 1:
                return GetLocalizedText("enemy.intent.title.attack");
            case 2:
                return GetLocalizedText("enemy.intent.title.defend");
            case 3:
            case 4:
                return GetLocalizedText("enemy.intent.title.debuff");
            default:
                return GetLocalizedText("enemy.intent.rainbow.random_title");
        }
    }

    private string GetRandomFollowupTooltipDescription()
    {
        switch (randomFollowupDisplayType)
        {
            case 1:
                return GetLocalizedText("enemy.intent.rainbow.random_attack_desc");
            case 2:
                return GetLocalizedText("enemy.intent.rainbow.random_defend_desc");
            case 3:
                return FormatLocalizedText("enemy.intent.rainbow.random_debuff_desc", FormatBuffStack(BuffEnum.Weak, 5));
            case 4:
                return FormatLocalizedText("enemy.intent.rainbow.random_debuff_desc", FormatBuffStack(BuffEnum.Vulnerable, 6));
            default:
                return GetLocalizedText("enemy.intent.rainbow.random_unknown_desc");
        }
    }
}
