public class ExtraDrawOnEnemyDamageBuffModel : BuffModel
{
    public ExtraDrawOnEnemyDamageBuffModel(int stack) : base(BuffEnum.ExtraDrawOnEnemyDamage, stack)
    {
    }

    public override void AfterAttack(CombatantModel self, CombatantModel target, ref int attackResult)
    {
        if (self?.Player != null && target != null && target.IsEnemy && attackResult > 0)
            self.Player.AddBuff(BuffEnum.ExtraDraw, stack);
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
