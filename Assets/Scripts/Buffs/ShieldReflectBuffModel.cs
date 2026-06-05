public class ShieldReflectBuffModel : BuffModel
{
    public ShieldReflectBuffModel(int stack) : base(BuffEnum.ShieldReflect, stack)
    {
    }

    public override void AfterTakeDamage(CombatantModel self, CombatantModel attacker, CombatDamageResult result)
    {
        if (result != null && result.ShieldDamage > 0)
            attacker?.TakeDamage(result.ShieldDamage);
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
