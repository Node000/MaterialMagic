public class ShieldReflectBuffModel : BuffModel
{
    public ShieldReflectBuffModel(int stack) : base(BuffEnum.ShieldReflect, stack)
    {
    }

    public override void AfterTakeDamage(CombatantModel self, CombatantModel attacker, CombatDamageResult result)
    {
        if (result == null || result.ShieldDamage <= 0 || attacker == null)
            return;

        int repeatCount = 1;
        if (self != null && self.IsPlayer)
            repeatCount += self.Player.GetBuffStack(BuffEnum.ShieldReflectBoost);

        for (int i = 0; i < repeatCount; i++)
            attacker.TakeDamage(result.ShieldDamage);
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsEnemy)
            stack = 0;
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
