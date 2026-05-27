public class VortexModifier : MaterialModifierModel
{
    public override void OnJoin()
    {
        MaterialModifierContext context = Context;
        if (context != null && context.BattleManager != null)
            context.BattleManager.AddRandomDebuffToRandomEnemy(1);
    }

    public override void OnEnd()
    {
        MaterialModifierContext context = Context;
        if (context != null && context.PlayerState != null)
            context.PlayerState.DrawCardsToPlayZoneTail(1);
    }
}
