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
        return string.IsNullOrEmpty(directionName) ? "本回合无法选中指定属性素材。" : $"本回合无法选中{directionName}属性素材。";
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }

    private static string GetDirectionName(int materialValue)
    {
        switch ((MaterialEnum)materialValue)
        {
            case MaterialEnum.Fire:
                return "上";
            case MaterialEnum.Water:
                return "下";
            case MaterialEnum.Wind:
                return "左";
            case MaterialEnum.Earth:
                return "右";
            default:
                return string.Empty;
        }
    }
}
