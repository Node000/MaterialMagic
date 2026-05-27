public class PoisonFogMagicModel : ScriptedMagicModel
{
    public PoisonFogMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        AddBuff(target, BuffEnum.Weak, 2, result);
        AddBuff(target, BuffEnum.Vulnerable, 1, result);
    }
}
