public class BurnOnAttackBuffModel : BuffModel
{
    public BurnOnAttackBuffModel(int stack) : base(BuffEnum.BurnOnAttack, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
