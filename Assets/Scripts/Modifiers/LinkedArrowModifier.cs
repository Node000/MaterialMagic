public class LinkedArrowModifier : MaterialModifierModel
{
    public override bool SuppressesDefaultWildBehavior()
    {
        return true;
    }

    public override bool IsLinkedArrowContainer()
    {
        return true;
    }

    public override int GetArrowMatchTokenCount(int tokenCount)
    {
        return 0;
    }

    public override bool UsesArrowBaseEffect(bool usesBaseEffect)
    {
        return false;
    }
}
