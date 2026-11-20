using System.Collections.Generic;

/// <summary>The requirement and effect selected for one battle resolution.</summary>
public readonly struct MagicTrigger
{
    public MagicModel Source { get; }
    public MagicMatchRule Rule { get; }
    public IReadOnlyList<MaterialEnum> Recipe { get; }
    public int Length => Source == null ? 0 : Rule == MagicMatchRule.AnyTwoDifferentElements ? 2
        : Rule == MagicMatchRule.ExactRecipe ? Recipe?.Count ?? 0 : 0;
    public bool TargetsPlayer => Source != null && (Source.CastParticleTargetsPlayer
        || Source.EffectType == MagicEffectType.GainShield || Source.EffectType == MagicEffectType.Heal
        || Source.EffectType == MagicEffectType.DrawNextTurn);

    public MagicTrigger(MagicModel source, MagicMatchRule rule, IReadOnlyList<MaterialEnum> recipe)
    {
        Source = source;
        Rule = rule;
        Recipe = recipe;
    }
}
