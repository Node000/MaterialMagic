public class ProliferatingArrowModifier : MaterialModifierModel
{
    public override void OnPlayedDiscard()
    {
        PlayerState playerState = Context?.PlayerState;
        if (playerState == null || model == null)
            return;

        MaterialModel copy = model.CloneForBattle("proliferated_" + model.instanceId + "_" + playerState.DiscardPile.Count);
        copy.removeCardAfterBattle = true;
        copy.isPlayed = false;
        playerState.DiscardPile.Add(copy);
        GameLog.Data($"Add proliferated arrow copy to discard pile {copy.instanceId}");
    }
}
