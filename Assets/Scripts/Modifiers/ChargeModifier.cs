public class ChargeModifier : MaterialModifierModel
{
    public override void OnJoin()
    {
        MaterialModifierContext context = Context;
        if (context != null && context.BattleManager != null)
            context.BattleManager.DamageRandomEnemy(1, context.PlayerState);
    }
}
