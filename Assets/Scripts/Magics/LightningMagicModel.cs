public class LightningMagicModel : ScriptedMagicModel
{
    public LightningMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuff(Target(battleManager), BuffEnum.Arc, 2, result);
    }
}
