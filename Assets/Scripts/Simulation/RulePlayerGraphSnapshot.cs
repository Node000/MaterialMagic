using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// In-memory branch snapshot. It is deliberately not the legacy checkpoint DTO: packed arrows,
/// random-arrow locks and concrete modifier runtime fields are retained without executing hooks.
/// </summary>
public sealed class RulePlayerGraphSnapshot
{
    private readonly MaterialModel[] deck, hand, draw, discard, play, consumed, scheduled, charged;
    private readonly MagicModel[] magics;
    private readonly int temporaryIndex;
    private readonly bool keepHand;
    private static readonly MethodInfo Memberwise = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo MagicDataField = typeof(MagicModel).GetField("<Data>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo MagicModifiersField = typeof(MagicModel).GetField("<Modifiers>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo MagicRecipeField = typeof(MagicModel).GetField("effectiveRecipe", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ModifierDataField = typeof(MagicModifierModel).GetField("<Data>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    public RulePlayerGraphSnapshot(PlayerState source)
    {
        var visited = new Dictionary<MaterialModel, MaterialModel>();
        deck = Copy(source.Deck, visited); hand = Copy(source.Hand, visited); draw = Copy(source.DrawPile, visited);
        discard = Copy(source.DiscardPile, visited); play = Copy(source.PlayZone, visited); consumed = Copy(source.ConsumedPile, visited);
        scheduled = Copy(source.TemporaryMaterialsNextTurn, visited); charged = Copy(source.PlayLimitChargedCards, visited);
        magics = new MagicModel[source.MagicBook.Count];
        for (int i = 0; i < magics.Length; i++) magics[i] = CloneMagic(source.MagicBook[i]);
        temporaryIndex = source.TemporaryMaterialIndex;
        keepHand = source.KeepHandOnEndTurn;
    }

    public void Restore(PlayerState player, PlayerCombatSaveData scalar)
    {
        var visited = new Dictionary<MaterialModel, MaterialModel>();
        MaterialModel[] restoredDeck = Copy(deck, visited), restoredHand = Copy(hand, visited), restoredDraw = Copy(draw, visited),
            restoredDiscard = Copy(discard, visited), restoredPlay = Copy(play, visited), restoredConsumed = Copy(consumed, visited),
            restoredScheduled = Copy(scheduled, visited), restoredCharged = Copy(charged, visited);
        player.Deck.Clear(); player.Deck.AddRange(restoredDeck);
        player.RestoreBattleDrawOrder(scalar?.battleDrawSeed ?? 0, scalar?.battleDrawStep ?? 0);
        player.RestoreCombatSnapshot(scalar?.shield ?? 0, restoredHand, restoredDraw, restoredDiscard, restoredPlay,
            restoredConsumed, restoredScheduled, scalar?.extraRefreshChancesThisTurn ?? 0, scalar?.playedCardCountThisTurn ?? 0);
        // RestoreCombatSnapshot sets pile flags for legacy saves. The branch graph owns exact flags.
        foreach (var pair in visited)
        {
            pair.Value.isPlayed = pair.Key.isPlayed; pair.Value.isTemporary = pair.Key.isTemporary;
            pair.Value.isRetained = pair.Key.isRetained; pair.Value.removeCardAfterBattle = pair.Key.removeCardAfterBattle;
        }
        player.RestorePlayLimitChargedCards(restoredCharged);
        player.RestoreTemporaryMaterialIndex(temporaryIndex);
        if (keepHand) player.KeepHandOnEndTurnOnce(); else player.ResetKeepHandOnEndTurn();
        player.MagicBook.Clear();
        foreach (MagicModel magic in magics) player.MagicBook.Add(CloneMagic(magic));
    }

    private static MaterialModel[] Copy(IEnumerable<MaterialModel> values, Dictionary<MaterialModel, MaterialModel> visited)
    {
        var result = new List<MaterialModel>();
        foreach (MaterialModel card in values) result.Add(RuleMaterialCodec.CloneDisplay(card, visited));
        return result.ToArray();
    }

    private static MagicModel CloneMagic(MagicModel source)
    {
        if (source == null) return null;
        var clone = (MagicModel)Memberwise.Invoke(source, null);
        MagicDataField.SetValue(clone, DeepCopy(source.Data));
        var modifiers = new List<MagicModifierModel>();
        foreach (MagicModifierModel modifier in source.Modifiers)
        {
            if (modifier == null) { modifiers.Add(null); continue; }
            var copy = (MagicModifierModel)Memberwise.Invoke(modifier, null);
            ModifierDataField.SetValue(copy, DeepCopy(modifier.Data));
            copy.model = clone;
            modifiers.Add(copy);
        }
        MagicModifiersField.SetValue(clone, modifiers);
        var cached = MagicRecipeField.GetValue(source) as MaterialEnum[];
        MagicRecipeField.SetValue(clone, cached != null ? (MaterialEnum[])cached.Clone() : null);
        return clone;
    }

    private static T DeepCopy<T>(T value) => RuleJson.Clone(value);
}
