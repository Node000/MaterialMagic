public class PeriodArrowModifier : MaterialModifierModel
{
    public override bool SuppressesDefaultWildBehavior()
    {
        return true;
    }

    public override bool IsArrowReadable(bool readable)
    {
        return false;
    }

    public override bool ShouldStopArrowReadSequence()
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
