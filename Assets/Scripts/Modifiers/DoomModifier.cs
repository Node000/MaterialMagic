public class DoomModifier : MaterialModifierModel
{
    public override void OnDiscard()
    {
        PlayerState player = CurrentContext?.PlayerState ?? BattleManager.Instance?.PlayerState;
        if (model != null && !model.isPlayed && player != null && player.IsEndingTurn)
            player.TakeDirectDamage(5);
    }
}
