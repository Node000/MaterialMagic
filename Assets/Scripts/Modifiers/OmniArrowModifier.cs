public class OmniArrowModifier : MaterialModifierModel
{
    public override bool SuppressesDefaultWildBehavior()
    {
        return true;
    }

    public override bool CanActAs(MaterialEnum material)
    {
        return material == MaterialEnum.Fire || material == MaterialEnum.Water || material == MaterialEnum.Wind || material == MaterialEnum.Earth;
    }

    public override MaterialEnum GetArrowDisplayMaterial(MaterialEnum material)
    {
        return MaterialEnum.Wild;
    }

    public override bool UsesArrowBaseEffect(bool usesBaseEffect)
    {
        return false;
    }

    public override void FillArrowBaseEffectDirections(ArrowReadStep step)
    {
        if (step == null)
            return;

        step.AddBaseEffectDirection(MaterialEnum.Fire);
        step.AddBaseEffectDirection(MaterialEnum.Water);
        step.AddBaseEffectDirection(MaterialEnum.Wind);
        step.AddBaseEffectDirection(MaterialEnum.Earth);
    }
}
