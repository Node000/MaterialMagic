public class ExtraDrawOnEnemyDamageBuffModel : BuffModel
{
    private int damageTriggerCount;

    public ExtraDrawOnEnemyDamageBuffModel(int stack) : base(BuffEnum.ExtraDrawOnEnemyDamage, stack)
    {
    }

    public override void AfterAttack(CombatantModel self, CombatantModel target, ref int attackResult)
    {
        if (self?.Player == null || target == null || !target.IsEnemy || attackResult <= 0)
            return;

        damageTriggerCount += stack;
        int extraDraw = damageTriggerCount / 2;
        if (extraDraw > 0)
        {
            self.Player.AddBuff(BuffEnum.ExtraDraw, extraDraw);
            damageTriggerCount %= 2;
        }
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
        {
            stack = 0;
            damageTriggerCount = 0;
        }
    }
}
