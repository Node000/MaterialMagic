public class KeepShieldNextTurnBuffModel : BuffModel
{
    public KeepShieldNextTurnBuffModel(int stack) : base(BuffEnum.KeepShieldNextTurn, stack)
    {
    }

    public override string GetSlotStackText()
    {
        return string.Empty;
    }

    public override string GetTooltipStackText()
    {
        return string.Empty;
    }
}
