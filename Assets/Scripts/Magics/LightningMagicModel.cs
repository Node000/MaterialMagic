public class LightningMagicModel : ScriptedMagicModel
{
    public LightningMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        Damage(playerState, target, 4, result);
        AddBuff(target, BuffEnum.Arc, 1, result);
    }
}
