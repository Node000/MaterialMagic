public class PreparedShieldBuffModel : BuffModel
{
    public override bool IsVisible => false;

    public PreparedShieldBuffModel(int stack) : base(BuffEnum.PreparedShield, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        int amount = stack;
        if (amount <= 0 || self?.Player == null)
            return;

        self.Player.GainShield(amount);
        self.Player.ConsumeBuff(BuffEnum.PreparedShield, amount);
    }
}
