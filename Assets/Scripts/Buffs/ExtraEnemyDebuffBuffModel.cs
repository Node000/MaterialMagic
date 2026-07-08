public class ExtraEnemyDebuffBuffModel : BuffModel
{
    public ExtraEnemyDebuffBuffModel(int stack) : base(BuffEnum.ExtraEnemyDebuff, stack)
    {
    }

    public override void OnGiveBuff(CombatantModel self, CombatantModel target, BuffEnum buffType, ref int stack)
    {
        if (self.IsPlayer && target != null && target.IsEnemy && BuffModel.GetKind(buffType) == BuffKindEnum.DeBuff)
            stack += stack * this.stack;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
