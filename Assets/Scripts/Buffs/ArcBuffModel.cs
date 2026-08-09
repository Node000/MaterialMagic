public class ArcBuffModel : BuffModel
{
    public ArcBuffModel(int stack) : base(BuffEnum.Arc, stack)
    {
    }

    public override void OnInvoke(CombatantModel self, CombatantModel target)
    {
        ApplyDamage(self);
    }

    public void ResolveAfterMagic(CombatantModel self, MagicCastResult result)
    {
        CombatDamageResult damageResult = ApplyDamage(self);
        result.AddEnemyDamageHit(self.Enemy, damageResult.HealthDamage, damageResult.ShieldDamage);
    }

    private CombatDamageResult ApplyDamage(CombatantModel self)
    {
        if (self != null && self.IsEnemy)
            return self.Enemy.TakeDamageIgnoringVulnerableResult(stack);

        return self != null ? self.TakeDamageResult(stack, null) : new CombatDamageResult();
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
