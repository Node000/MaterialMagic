public class DirectionDamageBonusBuffModel : BuffModel
{
    public DirectionDamageBonusBuffModel(int stack) : base(BuffEnum.DirectionDamageBonus, stack)
    {
    }

    public override void OnAttack(CombatantModel self, CombatantModel target, ref int attackValue)
    {
        if (self.IsPlayer)
            attackValue += stack;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
