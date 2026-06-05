public class FlowModifier : MaterialModifierModel
{
    public override void OnRefresh()
    {
        MaterialModifierContext context = Context;
        if (context != null && context.PlayerState != null)
            context.PlayerState.DrawCardsForRefresh(1);
    }
}
