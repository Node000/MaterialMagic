public class MirrorMagicModifierModel : MagicModifierModel
{
    public MirrorMagicModifierModel(MagicModifierData data) : base(data)
    {
    }

    public override MaterialEnum[] ModifyRecipe(MaterialEnum[] recipe)
    {
        if (recipe == null || recipe.Length < 2)
            return recipe;

        MaterialEnum[] reversed = new MaterialEnum[recipe.Length];
        for (int i = 0; i < recipe.Length; i++)
            reversed[i] = recipe[recipe.Length - 1 - i];
        return reversed;
    }
}
