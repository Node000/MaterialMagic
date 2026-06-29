public static class MaterialModelExtensions
{
    public static void RemoveTurnOnlyModifiers(this MaterialModel model)
    {
        if (model == null)
            return;

        for (int i = model.modifiers.Count - 1; i >= 0; i--)
        {
            if (model.modifiers[i] != null && model.modifiers[i].RemoveModifierAfterTurn)
                model.modifiers.RemoveAt(i);
        }
    }
}
