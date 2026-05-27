public class FireballArtMagicModel : ScriptedMagicModel
{
    public FireballArtMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        Damage(playerState, target, 5, result);
        AddBuff(target, BuffEnum.Burning, 2, result);
    }
}
