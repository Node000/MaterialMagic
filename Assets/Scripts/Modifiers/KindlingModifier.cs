public class KindlingModifier : MaterialModifierModel
{
    public override void OnInvoke()
    {
        MaterialModifierContext context = Context;
        if (context != null && context.BattleManager != null)
            context.BattleManager.AddBurningToRandomEnemy(1);
    }
}
