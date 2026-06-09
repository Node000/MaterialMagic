public class ReturnArrowModifier : MaterialModifierModel
{
    public override bool SuppressesDefaultWildBehavior()
    {
        return true;
    }

    public override bool IsArrowReadable(bool readable)
    {
        return true;
    }

    public override bool UsesArrowBaseEffect(bool usesBaseEffect)
    {
        return false;
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
