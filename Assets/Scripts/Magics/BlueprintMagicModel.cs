public sealed class BlueprintMagicModel : MagicModel
{
    public BlueprintMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }

    protected override bool AllowsModifiers => false;
    protected override MagicMatchRule NativeMatchRule => MagicMatchRule.NoTrigger;

    public override MagicTrigger GetCombatTrigger(PlayerState player = null)
    {
        if (player == null || !ReferenceEquals(player.GetItemByIndex(GetIndex()), this))
            return default;

        MagicModel neighbor = player.GetItemByIndex(player.GetRightNeighborIndex(GetIndex()));
        // Only copy a native trigger. This excludes other blueprints and never recurses.
        MagicTrigger trigger = neighbor?.GetOwnTrigger() ?? default;
        return trigger.Length > 0 ? trigger : default;
    }
}
