public class HalfArrowModifier : MaterialModifierModel
{
    public override bool SuppressesDefaultWildBehavior()
    {
        return true;
    }

    public override bool UsesArrowBaseEffect(bool usesBaseEffect)
    {
        return false;
    }
}
