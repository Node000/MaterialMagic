public class BigArrow4Modifier : MaterialModifierModel
{
    public override int GetArrowMatchTokenCount(int tokenCount)
    {
        return tokenCount < 4 ? 4 : tokenCount;
    }
}
