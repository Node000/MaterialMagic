public class EarthHandMagicModel : ScriptedMagicModel
{
    public EarthHandMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override bool CastParticleTargetsAllEnemies => true;

    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        GainShield(playerState, battleManager, 10, result);
        DamageAll(playerState, battleManager, playerState.Shield, result);
    }
}
