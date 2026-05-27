public class ExtraDrawBuffModel : BuffModel
{
    public ExtraDrawBuffModel(int stack) : base(BuffEnum.ExtraDraw, stack)
    {
    }

    public override void AfterTurnStart(CombatantModel self, CombatantModel opponent)
    {
    }
}
