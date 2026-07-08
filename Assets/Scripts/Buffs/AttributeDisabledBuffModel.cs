public class AttributeDisabledBuffModel : BuffModel
{
    public AttributeDisabledBuffModel(int stack) : base(BuffEnum.AttributeDisabled, stack)
    {
    }

    public override string GetSlotStackText()
    {
        return GetDirectionName(stack);
    }

    public override string GetTooltipStackText()
    {
        return GetDirectionName(stack);
    }

    public override string GetDesc()
    {
        string directionName = GetDirectionName(stack);
        if (string.IsNullOrEmpty(directionName))
            return LocalizationKeys.GetBuffDescription(buffType);

        string template = LocalizationSystem.GetText("buff.attributedisabled.desc_with_material", string.Empty);
        return string.IsNullOrEmpty(template) ? LocalizationKeys.GetBuffDescription(buffType) : string.Format(template, directionName);
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }

    private static string GetDirectionName(int materialValue)
    {
        MaterialEnum material = (MaterialEnum)materialValue;
        return material == MaterialEnum.None || material == MaterialEnum.Wild ? string.Empty : LocalizationKeys.GetMaterialName(material);
    }
}
