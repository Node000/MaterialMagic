public class KindlingModifier : MaterialModifierModel
{
    public override void OnTokenInvoke(ArrowReadToken token, ArrowReadStep step)
    {
        MaterialModifierContext context = Context;
        if (context != null && context.BattleManager != null)
            context.BattleManager.AddBurningToRandomEnemy(1);
    }
}
