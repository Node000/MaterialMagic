public class SturdyModifier : MaterialModifierModel
{
    public override void OnTokenInvoke(ArrowReadToken token, ArrowReadStep step)
    {
        MaterialModifierContext context = Context;
        if (context != null && context.PlayerState != null)
            context.PlayerState.GainShield(1);
    }

}
