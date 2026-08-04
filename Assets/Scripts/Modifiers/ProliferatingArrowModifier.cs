public class ProliferatingArrowModifier : MaterialModifierModel
{
    public override ArrowReadAfterReadAction GetArrowAfterReadAction()
    {
        return ArrowReadAfterReadAction.ReturnNextTurn;
    }

    public override void OnArrowBaseEffectResolve(ArrowReadContext context)
    {
        PlayerState playerState = context?.PlayerState ?? Context?.PlayerState;
        if (playerState == null || model == null)
            return;

        MaterialModel copy = model.CloneForBattle("proliferated_" + model.instanceId + "_" + playerState.DiscardPile.Count);
        copy.removeCardAfterBattle = true;
        copy.isPlayed = false;
        copy.RemoveModifiers<ProliferatingArrowModifier>();
        playerState.AddCardToDiscardPile(copy);
        GameLog.Data($"Add proliferated arrow copy to discard pile {copy.instanceId}");
    }
}
