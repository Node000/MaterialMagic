public class PetrifyMagicModel : ScriptedMagicModel
{
    public PetrifyMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        Damage(playerState, target, 4, result);
        AddBuff(target, BuffEnum.Weak, 1, result);
        AddBuff(target, BuffEnum.Slow, 1, result);
    }
}
