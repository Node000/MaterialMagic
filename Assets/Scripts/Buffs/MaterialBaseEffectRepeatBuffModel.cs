public class MaterialBaseEffectRepeatBuffModel : BuffModel
{
    public MaterialBaseEffectRepeatBuffModel(int stack) : base(BuffEnum.MaterialBaseEffectRepeat, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
