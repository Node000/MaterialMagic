public class DirectionShieldBonusBuffModel : BuffModel
{
    public DirectionShieldBonusBuffModel(int stack) : base(BuffEnum.DirectionShieldBonus, stack)
    {
    }

    public override void OnGainShield(CombatantModel self, ref int shieldValue)
    {
        if (self.IsPlayer)
            shieldValue += stack;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
