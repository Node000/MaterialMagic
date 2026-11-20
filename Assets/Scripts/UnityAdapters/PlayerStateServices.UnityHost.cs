#if !MATERIALMAGIC_STANDALONE
using System;

/// <summary>
/// Explicit adapter boundary. Presentation supplies the actual feedback, analytics and codex actions;
/// this assembly knows only Core services and delegates, never scene/audio/UI types.
/// </summary>
public static class UnityRuleHostBindings
{
    public static PlayerStateServices CreateServices(Action<int, int> damageFeedback, Action goldFeedback,
        Action<int> damageTaken, Action<int> damageDealt, Action<MagicData> magicDiscovered)
    {
        if (damageFeedback == null) throw new ArgumentNullException(nameof(damageFeedback));
        if (goldFeedback == null) throw new ArgumentNullException(nameof(goldFeedback));
        if (damageTaken == null) throw new ArgumentNullException(nameof(damageTaken));
        if (damageDealt == null) throw new ArgumentNullException(nameof(damageDealt));
        if (magicDiscovered == null) throw new ArgumentNullException(nameof(magicDiscovered));
        return new PlayerStateServices(damageFeedback, goldFeedback, damageTaken, damageDealt, magicDiscovered);
    }
}
#endif
