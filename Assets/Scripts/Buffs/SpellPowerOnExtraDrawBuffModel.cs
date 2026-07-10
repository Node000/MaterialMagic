public class SpellPowerOnExtraDrawBuffModel : BuffModel
{
    public SpellPowerOnExtraDrawBuffModel(int stack) : base(BuffEnum.SpellPowerOnExtraDraw, stack)
    {
    }

    public override void OnReceiveBuff(CombatantModel self, CombatantModel source, BuffEnum buffType, ref int stack)
    {
        if (self?.Player != null && buffType == BuffEnum.ExtraDraw && stack > 0)
            self.Player.AddBuff(BuffEnum.SpellPower, stack * this.stack);
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
