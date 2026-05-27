public class LiquefyModifier : MaterialModifierModel
{
    public override bool CanActAs(MaterialEnum material)
    {
        return material == MaterialEnum.Water;
    }
}
