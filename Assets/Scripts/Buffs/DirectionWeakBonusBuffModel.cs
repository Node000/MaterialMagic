public class DirectionWeakBonusBuffModel : BuffModel
{
    public DirectionWeakBonusBuffModel(int stack) : base(BuffEnum.DirectionWeakBonus, stack)
    {
    }

    public override void OnGiveBuff(CombatantModel self, CombatantModel target, BuffEnum buffType, ref int stack)
    {
        if (self.IsPlayer && target != null && target.IsEnemy && BuffModel.GetKind(buffType) == BuffKindEnum.DeBuff)
            stack += this.stack;
    }

    // 与 ExtraEnemyDebuff 同构：触发点可能在敌方回合发生，清除时机必须在玩家下个回合开始。
    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
