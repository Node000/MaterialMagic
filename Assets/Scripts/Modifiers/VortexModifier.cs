public class VortexModifier : MaterialModifierModel
{
    public override void OnTokenInvoke(ArrowReadToken token, ArrowReadStep step)
    {
        MaterialModifierContext context = Context;
        if (context == null)
            return;

        if (context.BattleManager != null && context.BattleManager.AddRandomDebuffToRandomEnemy(1))
            context.EnemyBuffChanged = true;
        if (context.PlayerState != null)
            context.PlayerState.DrawCardsToPlayZoneTail(1);
    }
}
