public class ReturnArrowModifier : MaterialModifierModel
{
    public override bool IsArrowReadable(bool readable)
    {
        return true;
    }

    public override int GetArrowMatchTokenCount(int tokenCount)
    {
        return 0;
    }

    public override bool ShouldRemoveSourceAfterArrowRead()
    {
        return true;
    }

    public override ArrowReadDirectionChange GetArrowReadDirectionChange()
    {
        return ArrowReadDirectionChange.Reverse;
    }
}
