public class BlizzardMagicModel : ScriptedMagicModel
{
    public BlizzardMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuffAll(battleManager, BuffEnum.Vulnerable, 1, result);
        AddBuffAll(battleManager, BuffEnum.Slow, 1, result);
        AddBuffAll(battleManager, BuffEnum.Weak, 1, result);
        DamageAll(playerState, battleManager, GetTotalEnemyDebuffStacks(battleManager), result);
    }
}
