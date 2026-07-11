public class TemporaryWindOnMaterialConsumedBuffModel : BuffModel
{
    public TemporaryWindOnMaterialConsumedBuffModel(int stack) : base(BuffEnum.TemporaryWindOnMaterialConsumed, stack)
    {
    }

    public override void AfterArrowConsumed(CombatantModel self, MaterialModel card)
    {
        if (self?.Player == null || card == null)
            return;

        for (int i = 0; i < stack; i++)
            self.Player.AddTemporaryMaterialNextTurn(MaterialEnum.Wind, true);
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
