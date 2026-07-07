public class KeepHandBuffModel : BuffModel
{
    public KeepHandBuffModel(int stack) : base(BuffEnum.KeepHand, stack)
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

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        self?.Player?.KeepHandOnEndTurnOnce();
        stack = 0;
    }
}
