public class BulwarkMagicModifierModel : MagicModifierModel
{
    public BulwarkMagicModifierModel(MagicModifierData data) : base(data)
    {
    }

    public override void AfterCast(MagicCastResult result)
    {
        MagicModifierContext context = Context;
        if (context == null || context.PlayerState == null)
            return;

        int shieldGain = context.PlayerState.GainShield(Data != null && Data.value > 0 ? Data.value : 2);
        if (result != null)
            result.playerShield += shieldGain;
    }
}
