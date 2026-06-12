public class TutorialDeathBuffModel : BuffModel
{
    public TutorialDeathBuffModel(int stack) : base(BuffEnum.TutorialDeath, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        ConsumeStack(1);
    }

    public override void OnExpire(CombatantModel self, CombatantModel opponent)
    {
        if (self != null && self.IsEnemy && !self.IsDead)
            self.Enemy.Kill(opponent);
    }
}
