public class BigArrow3Modifier : MaterialModifierModel
{
    public override int GetArrowMatchTokenCount(int tokenCount)
    {
        return tokenCount < 3 ? 3 : tokenCount;
    }
}
