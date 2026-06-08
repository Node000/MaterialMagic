public class BigArrow2Modifier : MaterialModifierModel
{
    public override int GetArrowMatchTokenCount(int tokenCount)
    {
        return tokenCount < 2 ? 2 : tokenCount;
    }
}
