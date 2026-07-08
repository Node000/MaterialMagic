public class ArcBuffModel : BuffModel
{
    public ArcBuffModel(int stack) : base(BuffEnum.Arc, stack)
    {
    }

    public override void OnInvoke(CombatantModel self, CombatantModel target)
    {
        if (self != null && self.IsEnemy)
            self.Enemy.TakeDamageIgnoringVulnerable(stack);
        else
            self?.TakeDamage(stack);
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
