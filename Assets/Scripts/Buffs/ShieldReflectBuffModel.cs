public class ShieldReflectBuffModel : BuffModel
{
    public ShieldReflectBuffModel(int stack) : base(BuffEnum.ShieldReflect, stack)
    {
    }

    public override void AfterAttack(CombatantModel self, CombatantModel target, ref int attackResult)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
