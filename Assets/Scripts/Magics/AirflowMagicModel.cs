public class AirflowMagicModel : ScriptedMagicModel
{
    public AirflowMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result) => DamageTarget(playerState, battleManager, 3, result);
}
