public class FragileArrowModifier : MaterialModifierModel
{
    public override ArrowReadAfterReadAction GetArrowAfterReadAction()
    {
        return ArrowReadAfterReadAction.SplitIntoHalfArrowsToDiscard;
    }

    public override int GetArrowMatchTokenCount(int tokenCount)
    {
        return 0;
    }
}
