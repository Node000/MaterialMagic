public class BurningWindMagicModel : ScriptedMagicModel
{
    public BurningWindMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override bool CastParticleTargetsAllEnemies => true;

    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        DamageAll(playerState, battleManager, 4, result);
        AddBuffAll(battleManager, BuffEnum.Burning, 4, result);
    }
}
