public class PoisonFogMagicModel : ScriptedMagicModel
{
    public PoisonFogMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuff(Target(battleManager), BuffEnum.Vulnerable, 2, result);
    }
}
