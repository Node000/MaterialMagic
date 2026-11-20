using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

/// <summary>Session-local stable IDs; independent of random GUIDs and retained after removals.</summary>
public sealed class AIEntityIds
{
    private sealed class IdentityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
    private readonly Dictionary<object, string> ids = new Dictionary<object, string>(new IdentityComparer());
    private readonly Dictionary<string, object> entities = new Dictionary<string, object>(StringComparer.Ordinal);
    private int nextCard;
    private int nextEnemy;

    public string Card(MaterialModel card) => Id(card, "card", ref nextCard);
    public string Enemy(EnemyModel enemy) => Id(enemy, "enemy", ref nextEnemy);
    public MaterialModel FindCard(string id) => id != null && entities.TryGetValue(id, out object value) ? value as MaterialModel : null;
    public EnemyModel FindEnemy(string id) => id != null && entities.TryGetValue(id, out object value) ? value as EnemyModel : null;

    public void Register(PlayerState player, BattleManager battle)
    {
        // Creation/deck order is stable; do not register only a random initial hand.
        RegisterCards(player.Deck);
        RegisterCards(player.DrawPile);
        RegisterCards(player.DiscardPile);
        RegisterCards(player.Hand);
        RegisterCards(player.PlayZone);
        RegisterCards(player.ConsumedPile);
        RegisterCards(player.TemporaryMaterialsNextTurn);
        for (int i = 0; battle != null && i < battle.Enemies.Count; i++) Enemy(battle.Enemies[i]);
    }
    private void RegisterCards(IReadOnlyList<MaterialModel> cards)
    {
        for (int i = 0; cards != null && i < cards.Count; i++) Card(cards[i]);
    }
    private string Id(object entity, string prefix, ref int next)
    {
        if (entity == null) return null;
        if (!ids.TryGetValue(entity, out string id))
        {
            id = prefix + ":" + (++next).ToString(System.Globalization.CultureInfo.InvariantCulture);
            ids.Add(entity, id);
            entities.Add(id, entity);
        }
        return id;
    }
}

public sealed class AICardObservation
{
    public string Id { get; }
    public MaterialEnum Material { get; }
    public MaterialEnum AlternateMaterial { get; }
    public bool Temporary { get; }
    public bool Retained { get; }
    public IReadOnlyList<string> Enhancements { get; }
    internal AICardObservation(MaterialModel card, AIEntityIds ids)
    {
        Id = ids.Card(card); Material = card.material; AlternateMaterial = card.alternateMaterial;
        Temporary = card.isTemporary; Retained = card.isRetained;
        Enhancements = new List<string>(card.enhancementIds).AsReadOnly();
    }
}

public sealed class AIIntentObservation
{
    public EnemyIntentType Type { get; }
    public int Value { get; }
    public int Times { get; }
    internal AIIntentObservation(EnemyIntentData intent)
    {
        Type = intent.intentType; Value = intent.value; Times = intent.times;
    }
}

public sealed class AIEnemyObservation
{
    public string Id { get; }
    public int ContentId { get; }
    public int Health { get; }
    public int Shield { get; }
    public bool IsDead { get; }
    public IReadOnlyList<AIIntentObservation> Intents { get; }
    internal AIEnemyObservation(EnemyModel enemy, AIEntityIds ids)
    {
        Id = ids.Enemy(enemy); ContentId = enemy.NumericId; Health = enemy.CurrentHealth;
        Shield = enemy.Shield; IsDead = enemy.IsDead;
        var intents = new List<AIIntentObservation>();
        for (int i = 0; i < enemy.CurrentIntents.Count; i++)
            if (enemy.CurrentIntents[i] != null) intents.Add(new AIIntentObservation(enemy.CurrentIntents[i]));
        Intents = intents.AsReadOnly();
    }
}

/// <summary>
/// Player-visible copied values, not model references. No draw-pile identities/order, future intents,
/// RNG seed/step, unrevealed map content or content-generator state is exposed to an AI policy.
/// Diagnostic replay state is explicitly separate in AIBattleDiagnosticSnapshot.
/// </summary>
public sealed class AIBattleObservation
{
    public BattlePhase Phase { get; }
    public int PlayerHealth { get; }
    public int PlayerShield { get; }
    public int Gold { get; }
    public int DrawPileCount { get; }
    public int RemainingRefreshChances { get; }
    public string FocusEnemyId { get; }
    public IReadOnlyList<AICardObservation> Hand { get; }
    public IReadOnlyList<AICardObservation> PlayZone { get; }
    public IReadOnlyList<AICardObservation> DiscardPile { get; }
    public IReadOnlyList<AIEnemyObservation> Enemies { get; }
    public IReadOnlyList<int> MagicIds { get; }
    public IReadOnlyList<int> MagicReadingRanks { get; }

    internal AIBattleObservation(BattleCommandSession commands, AIEntityIds ids)
    {
        PlayerState player = commands.Player;
        BattleManager battle = commands.Battle;
        Phase = battle != null ? battle.CurrentPhase : BattlePhase.None;
        PlayerHealth = player.CurrentHealth; PlayerShield = player.Shield; Gold = player.Gold;
        DrawPileCount = player.DrawPile.Count; RemainingRefreshChances = commands.RemainingRefreshChances;
        FocusEnemyId = ids.Enemy(battle?.FocusTarget);
        Hand = CopyCards(player.Hand, ids); PlayZone = CopyCards(player.PlayZone, ids); DiscardPile = CopyCards(player.DiscardPile, ids);
        var enemies = new List<AIEnemyObservation>();
        for (int i = 0; battle != null && i < battle.Enemies.Count; i++)
            if (battle.Enemies[i] != null) enemies.Add(new AIEnemyObservation(battle.Enemies[i], ids));
        Enemies = enemies.AsReadOnly();
        var magic = new List<int>();
        for (int i = 0; i < player.MagicBook.Count; i++) magic.Add(player.MagicBook[i]?.Data?.numericId ?? 0);
        MagicIds = magic.AsReadOnly();
        MagicReadingRanks = player.CaptureMagicReadingRanks(player.MagicBook);
    }

    internal static IReadOnlyList<AICardObservation> CopyCards(IReadOnlyList<MaterialModel> cards, AIEntityIds ids)
    {
        var copy = new List<AICardObservation>();
        for (int i = 0; i < cards.Count; i++) if (cards[i] != null) copy.Add(new AICardObservation(cards[i], ids));
        return copy.AsReadOnly();
    }
}

/// <summary>
/// Hidden diagnostic state for replay assertions, NEVER supplied to IAICommandPolicy.
/// Baseline fingerprint covers RNG, pile identity/material/modifier sequence, visible scalar state,
/// enemies and current intents. Optional host fingerprint extends this to content-specific state;
/// this is not a complete battle-save/clone protocol.
/// </summary>
public sealed class AIBattleDiagnosticSnapshot
{
    public int Seed { get; }
    public int RandomStep { get; }
    public string Fingerprint { get; }
    public IReadOnlyList<AICardObservation> HiddenDrawPile { get; }

    internal AIBattleDiagnosticSnapshot(BattleCommandSession commands, AIEntityIds ids, string extension)
    {
        var status = commands.Player as PlayerStatus;
        Seed = status?.RunRandomSeed ?? 0;
        RandomStep = status?.RunRandomStep ?? 0;
        HiddenDrawPile = AIBattleObservation.CopyCards(commands.Player.DrawPile, ids);
        AIBattleObservation observation = new AIBattleObservation(commands, ids);
        var text = new StringBuilder();
        text.Append(Seed).Append('/').Append(RandomStep).Append('/').Append((int)observation.Phase)
            .Append('/').Append(observation.PlayerHealth).Append('/').Append(observation.PlayerShield)
            .Append('/').Append(observation.Gold).Append('/').Append(observation.RemainingRefreshChances)
            .Append('/').Append(observation.FocusEnemyId).Append('/').Append(commands.TurnResolving);
        AppendCards(text, observation.Hand); AppendCards(text, observation.PlayZone);
        AppendCards(text, observation.DiscardPile); AppendCards(text, HiddenDrawPile);
        for (int i = 0; i < observation.Enemies.Count; i++)
        {
            AIEnemyObservation enemy = observation.Enemies[i];
            text.Append('|').Append(enemy.Id).Append(':').Append(enemy.ContentId).Append(':').Append(enemy.Health).Append(':').Append(enemy.Shield);
            for (int n = 0; n < enemy.Intents.Count; n++)
                text.Append(';').Append((int)enemy.Intents[n].Type).Append(',').Append(enemy.Intents[n].Value).Append(',').Append(enemy.Intents[n].Times);
        }
        for (int i = 0; i < observation.MagicIds.Count; i++) text.Append("|magic:").Append(observation.MagicIds[i]).Append(':').Append(observation.MagicReadingRanks[i]);
        text.Append("|extension:").Append(extension ?? string.Empty);
        Fingerprint = text.ToString();
    }
    private static void AppendCards(StringBuilder text, IReadOnlyList<AICardObservation> cards)
    {
        text.Append('|');
        for (int i = 0; i < cards.Count; i++)
        {
            AICardObservation card = cards[i];
            text.Append(card.Id).Append(':').Append((int)card.Material).Append(':').Append((int)card.AlternateMaterial)
                .Append(':').Append(card.Temporary).Append(':').Append(card.Retained);
            for (int n = 0; n < card.Enhancements.Count; n++)
            {
                string value = card.Enhancements[n] ?? string.Empty;
                text.Append(':').Append(value.Length).Append(':').Append(value);
            }
            text.Append(';');
        }
    }
}
