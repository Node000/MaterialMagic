public class ExtraDrawOnEnemyDamageBuffModel : BuffModel
{
    private int damageTriggerCount;
    public override System.Collections.Generic.Dictionary<string, int> ExportRuntimeState() => new System.Collections.Generic.Dictionary<string, int> { { "hits", damageTriggerCount } };
    public override void RestoreRuntimeState(System.Collections.Generic.IReadOnlyDictionary<string, int> values) => damageTriggerCount = ReadState(values, "hits");

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
