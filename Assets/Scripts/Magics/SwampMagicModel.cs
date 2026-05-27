public class SwampMagicModel : ScriptedMagicModel
{
    public SwampMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        AddBuff(target, BuffEnum.Slow, 2, result);
        AddBuff(target, BuffEnum.Weak, 2, result);
        playerState.AddBuff(BuffEnum.ExtraRefresh, 1);
    }
}
