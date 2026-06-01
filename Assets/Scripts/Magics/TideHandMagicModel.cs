public class TideHandMagicModel : ScriptedMagicModel
{
    public TideHandMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override bool CastParticleTargetsAllEnemies => true;

    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        DamageAll(playerState, battleManager, 7, result);
        AddAllEnemyDebuffStacks(battleManager, 2);
        playerState.AddBuff(BuffEnum.ExtraRefresh, 1);
    }
}
