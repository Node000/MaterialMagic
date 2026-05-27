public class BurningHandMagicModel : ScriptedMagicModel
{
    public BurningHandMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.AddBuff(BuffEnum.SpellPower, 2);
        AddBuffAll(battleManager, BuffEnum.Burning, 8, result);
    }
}
