public class AttributeDisabledBuffModel : BuffModel
{
    public AttributeDisabledBuffModel(int stack) : base(BuffEnum.AttributeDisabled, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
