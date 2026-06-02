public class IcePickMagicModel : ScriptedMagicModel
{
    public IcePickMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuff(Target(battleManager), BuffEnum.Weak, 3, result);
    }
}
