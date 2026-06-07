public class MagicAttackAllBuffModel : BuffModel
{
    public MagicAttackAllBuffModel(int stack) : base(BuffEnum.MagicAttackAll, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
