public class EternalArrowModifier : MaterialModifierModel
{
    public override ArrowReadAfterReadAction GetArrowAfterReadAction()
    {
        return ArrowReadAfterReadAction.ReturnNextTurn;
    }
}
