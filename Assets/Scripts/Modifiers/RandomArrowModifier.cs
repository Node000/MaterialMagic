public class RandomArrowModifier : MaterialModifierModel
{
    private MaterialEnum rolledMaterial = MaterialEnum.None;

    public override bool SuppressesDefaultWildBehavior()
    {
        return true;
    }

    public override void OnBeforeArrowRead(ArrowReadContext context)
    {
        if (rolledMaterial != MaterialEnum.None)
            return;

        int value = context != null ? context.NextRandomInt((int)MaterialEnum.Fire, (int)MaterialEnum.Earth + 1) : UnityEngine.Random.Range((int)MaterialEnum.Fire, (int)MaterialEnum.Earth + 1);
        rolledMaterial = (MaterialEnum)value;
    }

    public override bool IsArrowReadable(bool readable)
    {
        return true;
    }

    public override bool CanActAs(MaterialEnum material)
    {
        return material != MaterialEnum.None && material == rolledMaterial;
    }

    public override MaterialEnum GetArrowDisplayMaterial(MaterialEnum material)
    {
        return rolledMaterial != MaterialEnum.None ? rolledMaterial : material;
    }

    public override bool UsesArrowBaseEffect(bool usesBaseEffect)
    {
        return false;
    }

    public override void FillArrowBaseEffectDirections(ArrowReadStep step)
    {
        if (rolledMaterial != MaterialEnum.None)
            step.AddBaseEffectDirection(rolledMaterial);
    }

    public override MaterialModifierModel Clone()
    {
        RandomArrowModifier clone = (RandomArrowModifier)base.Clone();
        clone.rolledMaterial = MaterialEnum.None;
        return clone;
    }
}
