using System;
using System.Collections.Generic;

/// <summary>Shared material DTO conversion. No slots, files, scenes or host save-system dependency.</summary>
public static class RuleMaterialCodec
{
    [ThreadStatic] private static bool projectLegacyCycles;

    // FullGraph retains the complete memory graph. The legacy DTO underneath it is only an acyclic
    // scalar fallback; persistent exports outside this scope reject cycles instead of overflowing.
    internal static IDisposable ProjectLegacyGraphForSnapshot() => new LegacyProjectionScope();

    private sealed class LegacyProjectionScope : IDisposable
    {
        private readonly bool previous = projectLegacyCycles;
        private bool disposed;
        public LegacyProjectionScope() { projectLegacyCycles = true; }
        public void Dispose() { if (disposed) return; projectLegacyCycles = previous; disposed = true; }
    }

    public static MaterialCardSaveData Export(MaterialModel card)
        => Export(card, new HashSet<MaterialModel>());

    private static MaterialCardSaveData Export(MaterialModel card, HashSet<MaterialModel> ancestors)
    {
        if (card == null) return null;
        if (!ancestors.Add(card))
        {
            if (projectLegacyCycles) return null;
            throw new InvalidOperationException("The legacy material save DTO cannot persist a cyclic linked-card graph. Use an in-memory FullGraph snapshot or the complete session checkpoint.");
        }
        try
        {
        var ids = new List<string>();
        foreach (MaterialModifierModel modifier in card.modifiers)
        {
            string id = MaterialModifierFactory.GetId(modifier);
            if (!string.IsNullOrEmpty(id)) ids.Add(id);
        }
        var linked = new MaterialCardSaveData[card.linkedCards.Count];
        for (int i = 0; i < linked.Length; i++) linked[i] = Export(card.linkedCards[i], ancestors);
        return new MaterialCardSaveData
        {
            instanceId = card.instanceId, material = (int)card.material,
            alternateMaterial = (int)card.alternateMaterial, enhancementIds = card.enhancementIds.ToArray(),
            modifierIds = ids.ToArray(), linkedCards = linked,
            isTemporary = card.isTemporary, isRetained = card.isRetained
        };
        }
        finally { ancestors.Remove(card); }
    }

    /// <summary>Detached display-only graph. Never calls draw/join/read hooks or resets a random-arrow lock.</summary>
    public static MaterialModel CloneForDisplay(MaterialModel source)
    {
        return CloneDisplay(source, new Dictionary<MaterialModel, MaterialModel>());
    }

    internal static MaterialModel CloneDisplay(MaterialModel source, Dictionary<MaterialModel, MaterialModel> visited)
    {
        if (source == null) return null;
        if (visited.TryGetValue(source, out MaterialModel known)) return known;
        var clone = new MaterialModel(source.instanceId, source.material)
        {
            alternateMaterial = source.alternateMaterial,
            isPlayed = source.isPlayed, isTemporary = source.isTemporary, isRetained = source.isRetained,
            removeCardAfterBattle = source.removeCardAfterBattle,
            enhancementIds = new List<string>(source.enhancementIds)
        };
        visited.Add(source, clone);
        foreach (MaterialModel card in source.linkedCards) clone.linkedCards.Add(CloneDisplay(card, visited));
        foreach (MaterialModel card in source.packedCards) clone.packedCards.Add(CloneDisplay(card, visited));
        foreach (MaterialModifierModel modifier in source.modifiers)
        {
            if (modifier == null) { clone.modifiers.Add(null); continue; }
            MaterialModifierModel copy = modifier.Clone();
            // Clone() is the production battle-copy policy; some concrete implementations deliberately
            // reset runtime state (RandomArrowModifier). Display snapshots must preserve that state.
            for (Type type = modifier.GetType(); type != null && typeof(MaterialModifierModel).IsAssignableFrom(type); type = type.BaseType)
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (field.Name == nameof(MaterialModifierModel.model)) continue;
                    object value = field.GetValue(modifier);
                    if (value is MaterialModel material) value = CloneDisplay(material, visited);
                    field.SetValue(copy, value);
                }
            copy.model = clone;
            clone.modifiers.Add(copy);
        }
        return clone;
    }

    public static MaterialModel Create(MaterialCardSaveData data)
    {
        if (data == null) return null;
        var card = new MaterialModel(data.instanceId, (MaterialEnum)data.material)
        { alternateMaterial = (MaterialEnum)data.alternateMaterial, isRetained = data.isRetained };
        if (data.enhancementIds != null) card.enhancementIds.AddRange(data.enhancementIds);
        for (int i = 0; data.linkedCards != null && i < data.linkedCards.Length; i++)
        {
            MaterialModel linked = Create(data.linkedCards[i]);
            if (linked != null) card.linkedCards.Add(linked);
        }
        for (int i = 0; data.modifierIds != null && i < data.modifierIds.Length; i++)
        {
            MaterialModifierModel modifier = MaterialModifierFactory.Create(data.modifierIds[i]);
            if (modifier == null) continue;
            if (modifier is DisabledArrowModifier disabled)
            { disabled.MarkRemoveAfterTurn(); disabled.MarkRemoveAfterBattle(); }
            card.AddModifier(modifier);
        }
        if (PlayerState.IsDeckPlaceholderMaterial(card)) card.RemoveModifiers<TemporaryModifier>();
        else if (data.isTemporary && !card.isTemporary) card.AddModifier(new TemporaryModifier());
        return card;
    }
}
