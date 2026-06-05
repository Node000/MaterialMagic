public class DirectionWeakBonusBuffModel : BuffModel
{
    public DirectionWeakBonusBuffModel(int stack) : base(BuffEnum.DirectionWeakBonus, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
