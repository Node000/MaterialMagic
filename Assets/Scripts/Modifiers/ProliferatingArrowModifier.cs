public class ProliferatingArrowModifier : MaterialModifierModel
{
    public override void OnPlayedDiscard()
    {
        PlayerState playerState = Context?.PlayerState;
        if (playerState == null || model == null)
            return;

        MaterialModel copy = model.CloneForBattle("proliferated_" + model.instanceId + "_" + playerState.TemporaryMaterialsNextTurn.Count);
        copy.removeCardAfterBattle = true;
        playerState.TemporaryMaterialsNextTurn.Add(copy);
        GameLog.Data($"Schedule proliferated arrow copy {copy.instanceId}");
    }
}
