public class TemporaryModifier : MaterialModifierModel
{
    /// <summary>【临时】箭头不受每回合打出数量限制，也不占用本回合打出额度。</summary>
    public override bool IgnoresPlayLimit()
    {
        return true;
    }
}
