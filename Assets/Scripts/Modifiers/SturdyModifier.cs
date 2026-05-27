public class SturdyModifier : MaterialModifierModel
{
    public override void OnInvoke()
    {
        MaterialModifierContext context = Context;
        if (context != null && context.PlayerState != null)
            context.PlayerState.GainShield(1);
    }

    public override void OnDiscard()
    {
        model?.RemoveModifiers<SturdyModifier>();
    }
}
