public class DebuffPowerBuffModel : BuffModel
{
    public DebuffPowerBuffModel(int stack) : base(BuffEnum.DebuffPower, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
