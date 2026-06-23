public class VortexModifier : MaterialModifierModel
{
    public override void OnTokenInvoke(ArrowReadToken token, ArrowReadStep step)
    {
        MaterialModifierContext context = Context;
        if (context != null && context.BattleManager != null && context.BattleManager.AddRandomDebuffToRandomEnemy(1))
            context.EnemyBuffChanged = true;
    }

    public override void OnEnd()
    {
        MaterialModifierContext context = Context;
        if (context != null && context.PlayerState != null)
            context.PlayerState.DrawCardsToPlayZoneTail(1);
    }
}
