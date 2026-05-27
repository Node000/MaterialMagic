public class ExplosionMagicModel : ScriptedMagicModel
{
    public ExplosionMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        AddBuff(target, BuffEnum.Burning, 2, result);
        int damage = target != null ? target.GetBuffStack(BuffEnum.Burning) : 0;
        Damage(playerState, target, damage, result);
    }
}
