public class DirectionExtraDrawBuffModel : BuffModel
{
    public DirectionExtraDrawBuffModel(int stack) : base(BuffEnum.DirectionExtraDraw, stack)
    {
    }

    public override void AfterTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer && stack > 0)
            self.Player.DrawCards(stack);
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
