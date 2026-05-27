public class IgniteMagicModel : ScriptedMagicModel
{
    public IgniteMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result) => AddBuff(Target(battleManager), BuffEnum.Burning, 2, result);
}
