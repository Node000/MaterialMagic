public class StableBuffModel : BuffModel
{
    public StableBuffModel(int stack) : base(BuffEnum.Stable, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        ConsumeStack(1);
    }

    public override void OnExpire(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsEnemy && !self.IsDead)
            self.AddBuff(BuffEnum.Disorder, 1);
    }
}
