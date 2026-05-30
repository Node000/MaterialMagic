public class PioneerMagicModifierModel : MagicModifierModel
{
    private int castCountThisTurn;

    public PioneerMagicModifierModel(MagicModifierData data) : base(data)
    {
    }

    public override void OnBattleStart()
    {
        castCountThisTurn = 0;
    }

    public override void OnTurnStart()
    {
        castCountThisTurn = 0;
    }

    public override void AfterCast(MagicCastResult result)
    {
        castCountThisTurn++;
        if (castCountThisTurn == 2 && Context != null && Context.PlayerState != null)
            Context.PlayerState.AddBuff(BuffEnum.ExtraDraw, Data != null && Data.value > 0 ? Data.value : 1);
    }
}
