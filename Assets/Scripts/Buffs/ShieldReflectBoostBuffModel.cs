public class ShieldReflectBoostBuffModel : BuffModel
{
    public ShieldReflectBoostBuffModel(int stack) : base(BuffEnum.ShieldReflectBoost, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
