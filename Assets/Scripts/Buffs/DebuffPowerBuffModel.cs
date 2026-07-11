public class DebuffPowerBuffModel : BuffModel
{
    public DebuffPowerBuffModel(int stack) : base(BuffEnum.DebuffPower, stack)
    {
    }

    public override void OnReceiveBuff(CombatantModel self, CombatantModel source, BuffEnum buffType, ref int stack)
    {
        if (self.IsEnemy && buffType != BuffEnum.DebuffPower && BuffModel.GetKind(buffType) == BuffKindEnum.DeBuff)
            stack += this.stack;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
