public class IcePickMagicModel : ScriptedMagicModel
{
    public IcePickMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        Damage(playerState, target, 4, result);
        AddBuff(target, BuffEnum.Vulnerable, 1, result);
    }
}
