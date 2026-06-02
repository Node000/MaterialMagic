public class RepeatSpellBuffModel : BuffModel
{
    public RepeatSpellBuffModel(int stack) : base(BuffEnum.RepeatSpell, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
