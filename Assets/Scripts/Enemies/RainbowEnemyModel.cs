public class RainbowEnemyModel : EnemyModel
{
    public RainbowEnemyModel(EnemyData data) : base(data)
    {
    }

    public override string GetSpecialIntentDisplayValue(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null)
            return string.Empty;

        switch (intent.value)
        {
            case 1:
                return "3";
            case 2:
                return "2";
            case 3:
                return "1";
            case 4:
                return "3";
            case 5:
                return "?";
            default:
                return string.Empty;
        }
    }

    protected override string GetSpecialIntentTooltipTitle(EnemyIntentData intent)
    {
        return intent != null && intent.value == 5 ? "意图：随机特殊效果" : "意图：方向强化";
    }

    protected override string GetSpecialIntentTooltipDescription(EnemyIntentData intent, PlayerState playerState)
    {
        if (intent == null)
            return string.Empty;

        switch (intent.value)
        {
            case 1:
                return "玩家将获得" + FormatBuffStack(BuffEnum.DirectionDamageBonus, 3);
            case 2:
                return "玩家将获得" + FormatBuffStack(BuffEnum.DirectionWeakBonus, 2);
            case 3:
                return "玩家将获得" + FormatBuffStack(BuffEnum.DirectionExtraDraw, 1);
            case 4:
                return "玩家将获得" + FormatBuffStack(BuffEnum.DirectionShieldBonus, 3);
            case 5:
                return "这个敌人将随机造成14点伤害、获得10点护盾、施加5层虚弱或施加6层易伤";
            default:
                return base.GetSpecialIntentTooltipDescription(intent, playerState);
        }
    }

    protected override void ProcessSpecialIntent(int value, PlayerState playerState)
    {
        if (playerState == null)
            return;

        switch (value)
        {
            case 1:
                playerState.AddBuff(BuffEnum.DirectionDamageBonus, 3);
                break;
            case 2:
                playerState.AddBuff(BuffEnum.DirectionWeakBonus, 2);
                break;
            case 3:
                playerState.AddBuff(BuffEnum.DirectionExtraDraw, 1);
                break;
            case 4:
                playerState.AddBuff(BuffEnum.DirectionShieldBonus, 3);
                break;
            case 5:
                ResolveRandomFollowup(playerState);
                break;
        }
    }

    private void ResolveRandomFollowup(PlayerState playerState)
    {
        int roll = playerState is PlayerStatus status ? status.NextRunRandomInt(0, 4) : UnityEngine.Random.Range(0, 4);
        switch (roll)
        {
            case 0:
                playerState.TakeDamage(14, new CombatantModel(this));
                break;
            case 1:
                GainShield(10);
                break;
            case 2:
                playerState.AddBuff(BuffEnum.Weak, 5);
                break;
            default:
                playerState.AddBuff(BuffEnum.Vulnerable, 6);
                break;
        }
    }
}
