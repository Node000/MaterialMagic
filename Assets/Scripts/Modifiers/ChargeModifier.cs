public class ChargeModifier : MaterialModifierModel
{
    public override void OnTokenInvoke(ArrowReadToken token, ArrowReadStep step)
    {
        MaterialModifierContext context = Context;
        if (context != null && context.BattleManager != null && context.BattleManager.AddArcToRandomEnemy(1))
            context.EnemyBuffChanged = true;
    }
}
