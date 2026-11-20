using UnityEngine;
using System.Collections.Generic;

public class PlayerState
{
    // Public synchronous rule calls bind this owner only for the call. Do not retain a rule scope
    // across host yields/awaits; nested calls preserve their current per-token modifier context.
    /// <summary>占位符（进阶负担）素材卡的 instanceId 前缀；占位符只用于占用卡组，不可被任何删牌流程移除。</summary>
    public const string DeckPlaceholderInstanceIdPrefix = "deck_placeholder_";

    public static string SelectedStartConfigId { get; set; } = "balanced";
    public static bool ContinueSavedRun { get; set; }
    public static bool GameSceneEntryRequested { get; set; }

    public static bool ConsumeGameSceneEntryRequested()
    {
        bool value = GameSceneEntryRequested;
        GameSceneEntryRequested = false;
        return value;
    }

    private readonly Dictionary<BuffEnum, BuffModel> buffs = new Dictionary<BuffEnum, BuffModel>();

    public event System.Action<BuffEnum, int> BuffAdded;
    public event System.Action<IReadOnlyList<MaterialModel>> DiscardPileShuffledIntoDrawPile;
    public event System.Action PlayZoneShuffled;

    private int temporaryMaterialIndex;
    internal int TemporaryMaterialIndex => temporaryMaterialIndex;
    internal void RestoreTemporaryMaterialIndex(int value) { temporaryMaterialIndex = Mathf.Max(0, value); }
    private int extraRefreshChancesThisTurn;
    private int permanentRefreshChancesUsedThisTurn;

    /// <summary>本回合已占用打出额度的箭头；点回手牌时凭此退还额度，避免退错保留/效果入区的箭头。</summary>
    private readonly HashSet<MaterialModel> playLimitChargedCards = new HashSet<MaterialModel>();

    /// <summary>每回合玩家主动打出箭头的默认上限；开局配置 maxPlayCount 缺省/非法时使用。</summary>
    public const int DefaultPlayLimitPerTurn = 8;

    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }
    public int Gold { get; private set; }
    public int DrawOrderVersion { get; set; }
    internal int BattleDrawSeed { get; private set; }
    internal int BattleDrawStep { get; private set; }

    internal void RestoreBattleDrawOrder(int seed, int step)
    {
        BattleDrawSeed = seed;
        BattleDrawStep = step;
    }

    internal void PrepareBattleDrawOrder()
    {
        if (DrawOrderVersion != 1) return;
        RestoreBattleDrawOrder(NextRunRandomInt(1, int.MaxValue), 0);
        ShuffleBattleDrawCards(DrawPile);
    }

    private void ShuffleBattleDrawCards(List<MaterialModel> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = RunRandom.Range(BattleDrawSeed, BattleDrawStep++, 0, i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }
    public int Shield { get; private set; }
    public int DrawCount { get; set; } = 5;
    /// <summary>每回合玩家主动打出箭头的上限，来自开局配置 maxPlayCount；默认 8。</summary>
    public int MaxPlayCount { get; set; } = DefaultPlayLimitPerTurn;
    /// <summary>实际生效的每回合打出上限；配置值小于等于 0 时回落到默认值。</summary>
    public int PlayLimitPerTurn => MaxPlayCount > 0 ? MaxPlayCount : DefaultPlayLimitPerTurn;
    public int PlayedCardCountThisTurn { get; private set; }
    public int RemainingPlayCount => Mathf.Max(0, PlayLimitPerTurn - PlayedCardCountThisTurn);
    public bool IsPlayLimitReached => PlayedCardCountThisTurn >= PlayLimitPerTurn;
    public int RefreshLimitReductionThisTurn { get; private set; }
    public int HandLimitThisTurn { get; private set; } = int.MaxValue;
    public int ExtraRefreshChancesThisTurn => extraRefreshChancesThisTurn;
    public int PermanentRefreshChances { get; private set; }
    public int RemainingPermanentRefreshChancesThisTurn => Mathf.Max(0, PermanentRefreshChances - permanentRefreshChancesUsedThisTurn);
    public int PermanentRefreshChancesUsedThisTurn => permanentRefreshChancesUsedThisTurn;

    public void RestoreRefreshTurnState(int permanentUsed, int reduction, int handLimit)
    {
        using (Rules.Enter()) RestoreRefreshTurnStateCore(permanentUsed, reduction, handLimit);
    }

    private void RestoreRefreshTurnStateCore(int permanentUsed, int reduction, int handLimit)
    {
        permanentRefreshChancesUsedThisTurn = Mathf.Clamp(permanentUsed, 0, PermanentRefreshChances);
        RefreshLimitReductionThisTurn = Mathf.Max(0, reduction);
        HandLimitThisTurn = Mathf.Max(0, handLimit);
    }
    public ArrowUpgradeState ArrowUpgrades { get; } = new ArrowUpgradeState();
    public RelicInventory Relics { get; }
    public PlayerStateServices Services { get; }
    public BattleManager Battle { get; internal set; }
    public RuleExecutionContext Rules { get; }
    public ShopSession CurrentShop { get; set; }
    /// <summary>本局已保留的商店付费删箭交易次数；免费删除不计入，换店/换层不重置。</summary>
    public int PaidShopArrowRemovalCount { get; private set; }

    internal void RestorePaidShopArrowRemovalCount(int count)
    {
        PaidShopArrowRemovalCount = Mathf.Max(0, count);
    }
    public RelicOfferSaveData PendingRelicOffer { get; set; }
    public RelicAcquireResult TryAcquireRelic(int id)
    {
        using (Rules.Enter()) return TryAcquireRelicCore(id);
    }

    private RelicAcquireResult TryAcquireRelicCore(int id) => Relics.TryAcquire(id);
    public bool KeepHandOnEndTurn { get; private set; }
    public readonly List<MaterialModel> TemporaryMaterialsNextTurn = new List<MaterialModel>();
    public List<MaterialModel> Deck { get; } = new List<MaterialModel>();
    public List<MaterialModel> DrawPile { get; } = new List<MaterialModel>();
    public List<MaterialModel> DiscardPile { get; } = new List<MaterialModel>();
    public List<MaterialModel> ConsumedPile { get; } = new List<MaterialModel>();
    public List<MaterialModel> Hand { get; } = new List<MaterialModel>();
    public List<MaterialModel> PlayZone { get; } = new List<MaterialModel>();
    public List<MagicModel> MagicBook { get; } = new List<MagicModel>();
    public MagicReadingOrder MagicReadingOrder { get; } = new MagicReadingOrder();
    public int MagicRightNeighborStep { get; private set; }
    public int MagicLayoutSlotCount { get; private set; }

    // Main PC uses a single row; PE slots are column-major with a step equal to the row count.
    public void SetMagicSlotLayout(int slotCount, int rightNeighborStep)
    {
        MagicLayoutSlotCount = Mathf.Max(0, slotCount);
        MagicRightNeighborStep = Mathf.Max(0, rightNeighborStep);
    }

    public int GetRightNeighborIndex(int index)
        => MagicRightNeighborStep > 0 && index >= 0 && index < MagicLayoutSlotCount - MagicRightNeighborStep
            ? index + MagicRightNeighborStep : -1;

    public MagicModel GetItemByIndex(int index) => index >= 0 ? GetMagicAtSlot(index) : null;

    public void SetMagicReadingOrder(IReadOnlyList<SlotReadingRank> slots)
    {
        using (Rules.Enter()) SetMagicReadingOrderCore(slots);
    }

    private void SetMagicReadingOrderCore(IReadOnlyList<SlotReadingRank> slots)
    {
        MagicReadingOrder.Set(slots);
        MagicReadingOrder.ApplyTo(MagicBook);
    }

    public IReadOnlyList<int> CaptureMagicReadingRanks(IReadOnlyList<MagicModel> magics)
    {
        using (Rules.Enter()) return CaptureMagicReadingRanksCore(magics);
    }

    private IReadOnlyList<int> CaptureMagicReadingRanksCore(IReadOnlyList<MagicModel> magics)
        => MagicReadingOrder.Capture(magics);
    public IReadOnlyDictionary<BuffEnum, BuffModel> Buffs => buffs;
    public EnemyModel LastDamageSourceEnemy { get; private set; }
    public bool IsEndingTurn { get; private set; }

    public PlayerState(int maxHealth = 50, int gold = 0, PlayerStateServices services = null, RunDifficultySaveData difficulty = null, IRuleContentProvider content = null, RuleProfilePolicy profilePolicy = null)
    {
        Services = services ?? PlayerStateServices.UnityHost;
        Rules = new RuleExecutionContext(this, difficulty ?? DifficultyUpgradeSystem.ExportCurrentState(), content, profilePolicy);
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        Gold = gold;
        Relics = new RelicInventory(this);
    }

    public void ResetKeepHandOnEndTurn()
    {
        using (Rules.Enter()) ResetKeepHandOnEndTurnCore();
    }

    private void ResetKeepHandOnEndTurnCore()
    {
        KeepHandOnEndTurn = false;
    }

    public void KeepHandOnEndTurnOnce()
    {
        using (Rules.Enter()) KeepHandOnEndTurnOnceCore();
    }

    private void KeepHandOnEndTurnOnceCore()
    {
        KeepHandOnEndTurn = true;
    }

    public static PlayerState CreateDefault()
    {
        string configId = string.IsNullOrEmpty(SelectedStartConfigId) ? "balanced" : SelectedStartConfigId;
        return CreateFromConfig(configId);
    }

    public static PlayerState CreateFromConfig(string configId)
    {
        if (!GameDataDatabase.TryGetPlayerStartConfigData(configId, out PlayerStartConfigData config))
            config = null;

        PlayerState state = new PlayerStatus(config != null ? config.maxHealth : 50, config != null ? config.gold : 0);
        GameLog.Data($"Create player config={configId} maxHealth={state.MaxHealth} gold={state.Gold}");
        ApplyStartConfig(state, config);
        return state;
    }

    public static void ApplyStartConfig(PlayerState state, PlayerStartConfigData config)
    {
        if (state == null) throw new System.ArgumentNullException(nameof(state));
        using (state.Rules.Enter())
        {
        if (config == null)
        {
            state.DrawPile.AddRange(state.Deck);
            return;
        }

        state.DrawCount = config.drawCount;
        state.MaxPlayCount = ResolveMaxPlayCount(config);

        for (int i = 0; i < config.initialMaterials.Length; i++)
        {
            PlayerStartMaterialData material = config.initialMaterials[i];
            if (material != null)
                AddInitialMaterials(state, material);
        }

        for (int i = 0; i < config.initialMagics.Length; i++)
        {
            PlayerStartMagicData magic = config.initialMagics[i];
            if (magic != null)
                state.SetMagicAtSlot(CreateMagicFromData(magic.magicId, magic.slotIndex), magic.slotIndex);
        }

        state.DrawPile.AddRange(state.Deck);
        }
    }

    /// <summary>开局配置里的每回合打出上限（叠加进阶“打出上限”词条）；配置缺失或值非法时回落到默认值（8）。</summary>
    public static int ResolveMaxPlayCount(PlayerStartConfigData config)
    {
        int baseCount = config != null && config.maxPlayCount > 0 ? config.maxPlayCount : DefaultPlayLimitPerTurn;
        return DifficultyUpgradeSystem.ModifyPlayLimit(baseCount);
    }

    public int DrawCards(int count)
    {
        using (Rules.Enter()) return DrawCardsCore(count);
    }

    private int DrawCardsCore(int count)
    {
        return DrawCardsToHand(count, true);
    }

    public int DrawCardsForRefresh(int count)
    {
        using (Rules.Enter()) return DrawCardsForRefreshCore(count);
    }

    private int DrawCardsForRefreshCore(int count)
    {
        return DrawCardsToHand(count, false);
    }

    public int DrawBasicMaterialCards(int count)
    {
        using (Rules.Enter()) return DrawBasicMaterialCardsCore(count);
    }

    private int DrawBasicMaterialCardsCore(int count)
    {
        return DrawCardsToHand(count, true, IsBasicMaterialCard);
    }

    public MaterialModel DrawCardToHand(bool triggerAfterDraw)
    {
        using (Rules.Enter()) return DrawCardToHandCore(triggerAfterDraw);
    }

    private MaterialModel DrawCardToHandCore(bool triggerAfterDraw)
    {
        return DrawCardToHand(triggerAfterDraw, null);
    }

    public MaterialModel DrawCardToHand(bool triggerAfterDraw, System.Predicate<MaterialModel> canDraw)
    {
        using (Rules.Enter()) return DrawCardToHandCore(triggerAfterDraw, canDraw);
    }

    private MaterialModel DrawCardToHandCore(bool triggerAfterDraw, System.Predicate<MaterialModel> canDraw)
    {
        using (Relics.BeginOperation())
        {
        if (Hand.Count >= HandLimitThisTurn)
            return null;

        if (!EnsureDrawPileHasCards(canDraw))
            return null;

        int randomIndex = GetRandomDrawableDrawPileIndex(canDraw);
        if (randomIndex < 0)
            return null;

        MaterialModel card = DrawPile[randomIndex];
        DrawPile.RemoveAt(randomIndex);
        Hand.Add(card);
        card.TriggerOnDraw();
        TriggerAfterEnterHand(card);
        if (triggerAfterDraw)
            TriggerAfterDraw(card);
        GameLog.Data($"Draw card {DescribeMaterial(card)} to hand. hand={Hand.Count} drawPile={DrawPile.Count} discardPile={DiscardPile.Count}");
        return card;
        }
    }

    private static MaterialModel TakeMaterialFromPile(List<MaterialModel> pile, MaterialEnum material)
    {
        if (pile == null)
            return null;

        for (int i = 0; i < pile.Count; i++)
        {
            MaterialModel card = pile[i];
            if (card != null && card.material == material)
            {
                pile.RemoveAt(i);
                return card;
            }
        }
        return null;
    }

    public int DrawSpecificMaterialsToHand(IReadOnlyList<MaterialEnum> materials, bool createTemporaryIfMissing)
    {
        using (Rules.Enter()) return DrawSpecificMaterialsToHandCore(materials, createTemporaryIfMissing);
    }

    private int DrawSpecificMaterialsToHandCore(IReadOnlyList<MaterialEnum> materials, bool createTemporaryIfMissing)
    {
        if (materials == null)
            return 0;

        int drawnCount = 0;
        for (int i = 0; i < materials.Count; i++)
        {
            if (Hand.Count >= HandLimitThisTurn)
                break;

            MaterialEnum material = materials[i];
            if (material == MaterialEnum.None)
                continue;

            MaterialModel card = TakeMaterialFromPile(DrawPile, material);
            if (card == null)
                card = TakeMaterialFromPile(DiscardPile, material);
            if (card == null && createTemporaryIfMissing)
            {
                card = new MaterialModel("temporary_tutorial_" + material + "_" + temporaryMaterialIndex++, material);
                card.AddModifier(new TemporaryModifier());
            }
            if (card == null)
                continue;

            card.isPlayed = false;
            Hand.Add(card);
            card.TriggerOnDraw();
            TriggerAfterEnterHand(card);
            TriggerAfterDraw(card);
            drawnCount++;
            GameLog.Data($"Draw fixed material {DescribeMaterial(card)} to hand. hand={Hand.Count} drawPile={DrawPile.Count} discardPile={DiscardPile.Count}");
        }
        return drawnCount;
    }

    public RefreshHandResult RefreshBasicMaterialHandCards(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) return RefreshBasicMaterialHandCardsCore(cards, removedTemporaryCards);
    }

    private RefreshHandResult RefreshBasicMaterialHandCardsCore(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        return RefreshHandCards(cards, removedTemporaryCards, null, IsBasicMaterialCard);
    }

    private int DrawCardsToHand(int count, bool triggerAfterDraw)
    {
        return DrawCardsToHand(count, triggerAfterDraw, null);
    }

    private int DrawCardsToHand(int count, bool triggerAfterDraw, System.Predicate<MaterialModel> canDraw)
    {
        int drawnCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (DrawCardToHand(triggerAfterDraw, canDraw) == null)
                break;
            drawnCount++;
        }

        return drawnCount;
    }

    public bool ApplyArrowReadAfterAction(MaterialModel card, ArrowReadAfterReadAction action)
    {
        using (Rules.Enter()) return ApplyArrowReadAfterActionCore(card, action);
    }

    private bool ApplyArrowReadAfterActionCore(MaterialModel card, ArrowReadAfterReadAction action)
    {
        if (card == null || action == ArrowReadAfterReadAction.None)
            return false;

        switch (action)
        {
            case ArrowReadAfterReadAction.ReturnNextTurn:
                return ReturnArrowReadSourceNextTurn(card);
            case ArrowReadAfterReadAction.SplitIntoHalfArrowsToDiscard:
                return SplitArrowReadSourceToDiscard(card);
            case ArrowReadAfterReadAction.Consume:
                return ConsumeCardForBattle(card);
            default:
                return false;
        }
    }

    private bool ReturnArrowReadSourceNextTurn(MaterialModel card)
    {
        if (!RemoveCardFromCombatPiles(card))
            return false;

        card.isPlayed = false;
        TemporaryMaterialsNextTurn.Add(card);
        GameLog.Data($"Return eternal arrow next turn {DescribeMaterial(card)}");
        return true;
    }

    private bool SplitArrowReadSourceToDiscard(MaterialModel card)
    {
        if (!ConsumeCardForBattle(card))
            return false;

        AddHalfArrowToDiscard(card, 0);
        AddHalfArrowToDiscard(card, 1);
        GameLog.Data($"Consume and split fragile arrow {DescribeMaterial(card)} to half arrows. discardPile={DiscardPile.Count}");
        return true;
    }

    private void AddHalfArrowToDiscard(MaterialModel source, int index)
    {
        MaterialModel half = new MaterialModel("half_" + source.instanceId + "_" + index + "_" + temporaryMaterialIndex++, source.material)
        {
            alternateMaterial = source.alternateMaterial,
            removeCardAfterBattle = true
        };

        for (int i = 0; i < source.modifiers.Count; i++)
        {
            MaterialModifierModel sourceModifier = source.modifiers[i];
            if (sourceModifier == null || sourceModifier is FragileArrowModifier || sourceModifier is HalfArrowModifier)
                continue;

            MaterialModifierModel clonedModifier = sourceModifier.Clone();
            if (clonedModifier != null)
                half.AddModifier(clonedModifier);
        }

        half.AddModifier(new HalfArrowModifier());
        AddCardToDiscardPile(half);
    }

    private bool RemoveCardFromCombatPiles(MaterialModel card)
    {
        bool removed = false;
        removed |= Hand.Remove(card);
        removed |= PlayZone.Remove(card);
        removed |= DrawPile.Remove(card);
        removed |= DiscardPile.Remove(card);
        removed |= ConsumedPile.Remove(card);
        removed |= TemporaryMaterialsNextTurn.Remove(card);
        return removed;
    }

    public int DrawCardsToPlayZoneTail(int count)
    {
        using (Rules.Enter()) return DrawCardsToPlayZoneTailCore(count);
    }

    private int DrawCardsToPlayZoneTailCore(int count)
    {
        int drawnCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (!EnsureDrawPileHasCards())
                break;

            int randomIndex = GetRandomDrawableDrawPileIndex(null);
            MaterialModel card = DrawPile[randomIndex];
            DrawPile.RemoveAt(randomIndex);
            PlayZone.Add(card);
            card.isPlayed = true;
            card.TriggerOnDraw();
            TriggerAfterDraw(card);
            drawnCount++;
            GameLog.Data($"Draw card {DescribeMaterial(card)} to play zone. playZone={PlayZone.Count} drawPile={DrawPile.Count} discardPile={DiscardPile.Count}");
        }

        return drawnCount;
    }

    internal int NextRelicRandomInt(int minInclusive, int maxExclusive) => NextRunRandomInt(minInclusive, maxExclusive);

    private int NextRunRandomInt(int minInclusive, int maxExclusive)
    {
        return this is PlayerStatus status ? status.NextRunRandomInt(minInclusive, maxExclusive) : Random.Range(minInclusive, maxExclusive);
    }

    private bool EnsureDrawPileHasCards()
    {
        return EnsureDrawPileHasCards(null, true);
    }

    private bool EnsureDrawPileHasCards(System.Predicate<MaterialModel> canDraw)
    {
        return EnsureDrawPileHasCards(canDraw, true);
    }

    private bool EnsureDrawPileHasCards(System.Predicate<MaterialModel> canDraw, bool allowShuffleFromDiscard)
    {
        if (HasDrawableDrawPileCard(canDraw))
            return true;

        if (!allowShuffleFromDiscard || DiscardPile.Count == 0)
            return false;

        List<MaterialModel> shuffledCards = new List<MaterialModel>(DiscardPile);
        if (BattleDrawSeed != 0) ShuffleBattleDrawCards(shuffledCards);
        DrawPile.AddRange(shuffledCards);
        DiscardPile.Clear();
        DiscardPileShuffledIntoDrawPile?.Invoke(shuffledCards);
        GameLog.Data($"Shuffle discard pile into draw pile. drawPile={DrawPile.Count}");
        return HasDrawableDrawPileCard(canDraw);
    }

    private bool HasDrawableDrawPileCard(System.Predicate<MaterialModel> canDraw)
    {
        if (canDraw == null)
            return DrawPile.Count > 0;

        for (int i = 0; i < DrawPile.Count; i++)
        {
            if (canDraw(DrawPile[i]))
                return true;
        }
        return false;
    }

    private int GetRandomDrawableDrawPileIndex(System.Predicate<MaterialModel> canDraw)
    {
        if (BattleDrawSeed != 0)
        {
            for (int i = 0; i < DrawPile.Count; i++)
                if (canDraw == null || canDraw(DrawPile[i])) return i;
            return -1;
        }
        if (canDraw == null)
            return DrawPile.Count > 0 ? NextRunRandomInt(0, DrawPile.Count) : -1;

        int drawableCount = 0;
        for (int i = 0; i < DrawPile.Count; i++)
        {
            if (canDraw(DrawPile[i]))
                drawableCount++;
        }

        if (drawableCount == 0)
            return -1;

        int target = NextRunRandomInt(0, drawableCount);
        for (int i = 0; i < DrawPile.Count; i++)
        {
            if (!canDraw(DrawPile[i]))
                continue;

            if (target == 0)
                return i;
            target--;
        }
        return -1;
    }

    private static bool IsBasicMaterialCard(MaterialModel card)
    {
        return card != null && (card.material == MaterialEnum.Fire || card.material == MaterialEnum.Wind || card.material == MaterialEnum.Water || card.material == MaterialEnum.Earth);
    }

    public bool ConsumeCardForBattle(MaterialModel card)
    {
        using (Rules.Enter()) return ConsumeCardForBattleCore(card);
    }

    private bool ConsumeCardForBattleCore(MaterialModel card)
    {
        if (card == null)
            return false;

        bool removed = false;
        removed |= DrawPile.Remove(card);
        removed |= DiscardPile.Remove(card);
        removed |= Hand.Remove(card);
        removed |= PlayZone.Remove(card);
        if (removed || Deck.Contains(card) || card.isTemporary)
            AddConsumedCard(card);
        return removed;
    }

    public void AddConsumedCard(MaterialModel card)
    {
        using (Rules.Enter()) AddConsumedCardCore(card);
    }

    private void AddConsumedCardCore(MaterialModel card)
    {
        if (card == null || ConsumedPile.Contains(card))
            return;

        card.isPlayed = false;
        ConsumedPile.Add(card);
        TriggerAfterMaterialConsumed(card);
        TriggerAfterArrowConsumed(card);
    }

    public bool TryMoveHandCardToPlay(MaterialModel card)
    {
        using (Rules.Enter()) return TryMoveHandCardToPlayCore(card);
    }

    private bool TryMoveHandCardToPlayCore(MaterialModel card)
    {
        return TryMoveHandCardToPlay(card, PlayZone.Count);
    }

    public bool TryMoveHandCardToPlay(MaterialModel card, int playZoneIndex)
    {
        using (Rules.Enter()) return TryMoveHandCardToPlayCore(card, playZoneIndex);
    }

    private bool TryMoveHandCardToPlayCore(MaterialModel card, int playZoneIndex)
    {
        return TryMoveHandCardToPlay(card, playZoneIndex, false);
    }

    public bool TryMoveHandCardToPlay(MaterialModel card, int playZoneIndex, bool allowDisabled)
    {
        using (Rules.Enter()) return TryMoveHandCardToPlayCore(card, playZoneIndex, allowDisabled);
    }

    private bool TryMoveHandCardToPlayCore(MaterialModel card, int playZoneIndex, bool allowDisabled)
    {
        return TryMoveHandCardToPlay(card, playZoneIndex, allowDisabled, false);
    }

    /// <summary>
    /// 将手牌箭头置入出牌区。countPlayLimit 为 true 时按本回合打出上限判定并累计额度（仅玩家主动打出使用，
    /// 事件/奖励等非战斗选择与效果类入区传 false）。带【临时】等豁免附魔的箭头不受上限拦截，也不占用额度。
    /// </summary>
    public bool TryMoveHandCardToPlay(MaterialModel card, int playZoneIndex, bool allowDisabled, bool countPlayLimit)
    {
        using (Rules.Enter()) return TryMoveHandCardToPlayCore(card, playZoneIndex, allowDisabled, countPlayLimit);
    }

    private bool TryMoveHandCardToPlayCore(MaterialModel card, int playZoneIndex, bool allowDisabled, bool countPlayLimit)
    {
        if (card == null)
            return false;

        int index = Hand.IndexOf(card);
        if (index < 0)
            return false;

        if (!allowDisabled && IsMaterialDisabled(card))
            return false;

        if (countPlayLimit && !CanPlayCardFromHand(card))
            return false;

        Hand.RemoveAt(index);
        PlayZone.Insert(Mathf.Clamp(playZoneIndex, 0, PlayZone.Count), card);
        card.isPlayed = true;
        card.TriggerOnJoin();
        if (GetBuffStack(BuffEnum.MaterialOverplayDebuff) > 0 || HasMaterialOverplayDebuffSource())
        {
            if (PlayZone.Count > 4)
                AddRandomEnemyDebuff(1);
        }
        if (countPlayLimit && !card.IgnoresPlayLimit())
        {
            playLimitChargedCards.Add(card);
            PlayedCardCountThisTurn++;
        }
        GameLog.Data($"Move card {DescribeMaterial(card)} hand->playZone. hand={Hand.Count} playZone={PlayZone.Count} playedThisTurn={PlayedCardCountThisTurn}");
        return true;
    }

    /// <summary>本回合是否还能从手牌打出该箭头；带【临时】等豁免附魔的箭头不受上限限制。</summary>
    public bool CanPlayCardFromHand(MaterialModel card)
    {
        using (Rules.Enter()) return CanPlayCardFromHandCore(card);
    }

    private bool CanPlayCardFromHandCore(MaterialModel card)
    {
        if (card == null)
            return false;

        if (card.IgnoresPlayLimit())
            return true;

        return !IsPlayLimitReached;
    }

    public void ResetPlayedCardCountThisTurn()
    {
        using (Rules.Enter()) ResetPlayedCardCountThisTurnCore();
    }

    private void ResetPlayedCardCountThisTurnCore()
    {
        PlayedCardCountThisTurn = 0;
        playLimitChargedCards.Clear();
    }

    /// <summary>把尚未消耗的已打出箭头点回手牌时退还本回合打出额度（保留箭头与效果入区的箭头不在册，不会误退）。</summary>
    private void RefundPlayLimitForReturnedCard(MaterialModel card)
    {
        if (card == null || !playLimitChargedCards.Remove(card))
            return;

        PlayedCardCountThisTurn = Mathf.Max(0, PlayedCardCountThisTurn - 1);
        GameLog.Data($"Player cancel play refunds play limit. playedThisTurn={PlayedCardCountThisTurn}");
    }

    /// <summary>被附魔禁用的箭头无法从手牌置入出牌区（旧 AttributeDisabled Buff 的直接判定已改为附魔效果）。</summary>
    public bool IsMaterialDisabled(MaterialModel card)
    {
        using (Rules.Enter()) return IsMaterialDisabledCore(card);
    }

    private bool IsMaterialDisabledCore(MaterialModel card)
    {
        if (card == null)
            return false;

        return !card.CanPlay();
    }

    private void AddRandomEnemyDebuff(int stack)
    {
        int index = NextRunRandomInt(0, 3);
        AddBuff(index == 0 ? BuffEnum.Weak : index == 1 ? BuffEnum.Slow : BuffEnum.Vulnerable, stack);
    }

    private bool HasMaterialOverplayDebuffSource()
    {
        IReadOnlyList<EnemyModel> enemies = Battle?.Enemies;
        if (enemies == null)
            return false;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead && enemy.GetBuffStack(BuffEnum.MaterialOverplayDebuff) > 0)
                return true;
        }

        return false;
    }

    public bool TryMovePlayCardToHand(MaterialModel card)
    {
        using (Rules.Enter()) return TryMovePlayCardToHandCore(card);
    }

    private bool TryMovePlayCardToHandCore(MaterialModel card)
    {
        return TryMovePlayCardToHand(card, Hand.Count);
    }

    public bool TryMovePlayCardToHand(MaterialModel card, int handIndex)
    {
        using (Rules.Enter()) return TryMovePlayCardToHandCore(card, handIndex);
    }

    private bool TryMovePlayCardToHandCore(MaterialModel card, int handIndex)
    {
        if (card == null)
            return false;

        int index = PlayZone.IndexOf(card);
        if (index < 0)
            return false;

        PlayZone.RemoveAt(index);
        card.TriggerOnDiscard();
        TriggerAfterDiscard(card);
        Hand.Insert(Mathf.Clamp(handIndex, 0, Hand.Count), card);
        TriggerAfterEnterHand(card);
        RefundPlayLimitForReturnedCard(card);
        GameLog.Data($"Move card {DescribeMaterial(card)} playZone->hand. hand={Hand.Count} playZone={PlayZone.Count}");
        return true;
    }

    public bool ReorderHandCard(MaterialModel card, int targetIndex)
    {
        using (Rules.Enter()) return ReorderHandCardCore(card, targetIndex);
    }

    private bool ReorderHandCardCore(MaterialModel card, int targetIndex)
    {
        return ReorderCard(Hand, card, targetIndex, "hand");
    }

    public bool ReorderPlayCard(MaterialModel card, int targetIndex)
    {
        using (Rules.Enter()) return ReorderPlayCardCore(card, targetIndex);
    }

    private bool ReorderPlayCardCore(MaterialModel card, int targetIndex)
    {
        return ReorderCard(PlayZone, card, targetIndex, "playZone");
    }

    private bool ReorderCard(List<MaterialModel> list, MaterialModel card, int targetIndex, string listName)
    {
        if (list == null || card == null)
            return false;

        int index = list.IndexOf(card);
        if (index < 0)
            return false;

        list.RemoveAt(index);
        int clampedIndex = Mathf.Clamp(targetIndex, 0, list.Count);
        list.Insert(clampedIndex, card);
        GameLog.Data($"Reorder card {DescribeMaterial(card)} in {listName}. index={clampedIndex}");
        return index != clampedIndex;
    }

    private readonly struct RefreshCombatSlot
    {
        public readonly MaterialModel Card;
        public readonly bool InPlayZone;
        public readonly int Index;

        public RefreshCombatSlot(MaterialModel card, bool inPlayZone, int index)
        {
            Card = card;
            InPlayZone = inPlayZone;
            Index = index;
        }
    }

    public readonly struct RefreshHandResult
    {
        public readonly int DrawnCount;
        public readonly int ReturnedCount;

        public RefreshHandResult(int drawnCount, int returnedCount)
        {
            DrawnCount = drawnCount;
            ReturnedCount = returnedCount;
        }
    }

    public RefreshHandResult RefreshBasicCombatCards(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) return RefreshBasicCombatCardsCore(cards, removedTemporaryCards);
    }

    private RefreshHandResult RefreshBasicCombatCardsCore(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        return RefreshCombatCards(cards, removedTemporaryCards, null, IsBasicMaterialCard);
    }

    public RefreshHandResult RefreshPlayZoneCardsToHand(List<MaterialModel> removedTemporaryCards, BattleManager battleManager, IReadOnlyList<MaterialEnum> forcedMaterials = null)
    {
        using (Rules.Enter()) return RefreshPlayZoneCardsToHandCore(removedTemporaryCards, battleManager, forcedMaterials);
    }

    private RefreshHandResult RefreshPlayZoneCardsToHandCore(List<MaterialModel> removedTemporaryCards, BattleManager battleManager, IReadOnlyList<MaterialEnum> forcedMaterials)
    {
        if (PlayZone.Count == 0)
            return new RefreshHandResult(0, 0);

        List<MaterialModel> cards = new List<MaterialModel>(PlayZone);
        if (forcedMaterials == null)
        {
            MaterialModifierContext previousContext = MaterialModifierModel.CurrentContext;
            MaterialModifierModel.CurrentContext = new MaterialModifierContext { PlayerState = this, BattleManager = battleManager };
            try
            {
                for (int i = 0; i < cards.Count; i++)
                    cards[i].TriggerOnRefresh();
            }
            finally
            {
                MaterialModifierModel.CurrentContext = previousContext;
            }
        }

        int returnedCount = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            MaterialModel card = cards[i];
            if (!PlayZone.Remove(card))
                continue;

            card.TriggerOnDiscard();
            TriggerAfterDiscard(card);
            if (card.isTemporary)
            {
                removedTemporaryCards?.Add(card);
                AddConsumedCard(card);
                GameLog.Data($"Refresh removes temporary play zone card {DescribeMaterial(card)}.");
                continue;
            }

            AddCardToDiscardPile(card);
            returnedCount++;
            GameLog.Data($"Refresh discards play zone card {DescribeMaterial(card)}. discardPile={DiscardPile.Count}");
        }

        int drawnCount = 0;
        if (forcedMaterials != null)
        {
            drawnCount = DrawSpecificMaterialsToHand(forcedMaterials, true);
        }
        else
        {
            for (int i = 0; i < cards.Count; i++)
            {
                MaterialModel replacement = DrawCombatRefreshReplacement(null, true);
                if (replacement == null)
                    break;

                replacement.isPlayed = false;
                Hand.Add(replacement);
                TriggerAfterEnterHand(replacement);
                drawnCount++;
            }
        }

        GameLog.Data($"Refresh play zone cards to hand selected={cards.Count} drawn={drawnCount} discarded={returnedCount} temporaryRemoved={removedTemporaryCards?.Count ?? 0}");
        return new RefreshHandResult(drawnCount, returnedCount);
    }

    public RefreshHandResult RefreshCombatCards(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards, BattleManager battleManager)
    {
        using (Rules.Enter()) return RefreshCombatCardsCore(cards, removedTemporaryCards, battleManager);
    }

    private RefreshHandResult RefreshCombatCardsCore(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards, BattleManager battleManager)
    {
        return RefreshCombatCards(cards, removedTemporaryCards, battleManager, null);
    }

    private RefreshHandResult RefreshCombatCards(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards, BattleManager battleManager, System.Predicate<MaterialModel> canDraw)
    {
        if (cards == null || cards.Count == 0)
            return new RefreshHandResult(0, 0);

        List<RefreshCombatSlot> slots = new List<RefreshCombatSlot>();
        for (int i = 0; i < cards.Count; i++)
        {
            MaterialModel card = cards[i];
            if (card == null)
                continue;

            int handIndex = Hand.IndexOf(card);
            if (handIndex >= 0)
                slots.Add(new RefreshCombatSlot(card, false, handIndex));
            else
            {
                int playZoneIndex = PlayZone.IndexOf(card);
                if (playZoneIndex >= 0)
                    slots.Add(new RefreshCombatSlot(card, true, playZoneIndex));
            }
        }

        if (slots.Count == 0)
            return new RefreshHandResult(0, 0);

        slots.Sort((a, b) => a.InPlayZone == b.InPlayZone ? a.Index.CompareTo(b.Index) : a.InPlayZone.CompareTo(b.InPlayZone));

        MaterialModifierContext previousContext = MaterialModifierModel.CurrentContext;
        MaterialModifierModel.CurrentContext = new MaterialModifierContext { PlayerState = this, BattleManager = battleManager };
        try
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i].Card.TriggerOnRefresh();
        }
        finally
        {
            MaterialModifierModel.CurrentContext = previousContext;
        }

        int returnedCount = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            MaterialModel card = slots[i].Card;
            if (slots[i].InPlayZone)
                PlayZone.Remove(card);
            else
                Hand.Remove(card);

            card.TriggerOnDiscard();
            TriggerAfterDiscard(card);
            if (card.isTemporary)
            {
                removedTemporaryCards?.Add(card);
                AddConsumedCard(card);
                GameLog.Data($"Refresh removes temporary combat card {DescribeMaterial(card)}.");
                continue;
            }

            AddCardToDiscardPile(card);
            returnedCount++;
            GameLog.Data($"Refresh discards combat card {DescribeMaterial(card)}. discardPile={DiscardPile.Count}");
        }

        List<MaterialModel> replacements = new List<MaterialModel>();
        for (int i = 0; i < slots.Count; i++)
        {
            MaterialModel replacement = DrawCombatRefreshReplacement(canDraw, allowShuffleFromDiscard: true);
            if (replacement == null)
                break;

            replacements.Add(replacement);
        }

        for (int i = 0; i < replacements.Count; i++)
        {
            MaterialModel replacement = replacements[i];
            if (slots[i].InPlayZone)
            {
                replacement.isPlayed = true;
                PlayZone.Insert(Mathf.Clamp(slots[i].Index, 0, PlayZone.Count), replacement);
                replacement.TriggerOnJoin();
            }
            else
            {
                replacement.isPlayed = false;
                Hand.Insert(Mathf.Clamp(slots[i].Index, 0, Hand.Count), replacement);
                TriggerAfterEnterHand(replacement);
            }
        }

        GameLog.Data($"Refresh combat cards selected={slots.Count} drawn={replacements.Count} discarded={returnedCount} temporaryRemoved={removedTemporaryCards?.Count ?? 0}");
        return new RefreshHandResult(replacements.Count, returnedCount);
    }

    private MaterialModel DrawCombatRefreshReplacement(System.Predicate<MaterialModel> canDraw, bool allowShuffleFromDiscard)
    {
        if (!EnsureDrawPileHasCards(canDraw, allowShuffleFromDiscard))
            return null;

        int randomIndex = GetRandomDrawableDrawPileIndex(canDraw);
        if (randomIndex < 0)
            return null;

        MaterialModel card = DrawPile[randomIndex];
        DrawPile.RemoveAt(randomIndex);
        card.TriggerOnDraw();
        return card;
    }

    public int ReturnHandCardsToDrawPile(IReadOnlyList<MaterialModel> cards)
    {
        using (Rules.Enter()) return ReturnHandCardsToDrawPileCore(cards);
    }

    private int ReturnHandCardsToDrawPileCore(IReadOnlyList<MaterialModel> cards)
    {
        return ReturnHandCardsToDiscardPile(cards, null);
    }

    public int ReturnHandCardsToDrawPile(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) return ReturnHandCardsToDrawPileCore(cards, removedTemporaryCards);
    }

    private int ReturnHandCardsToDrawPileCore(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        return ReturnHandCardsToDiscardPile(cards, removedTemporaryCards);
    }

    public int ReturnHandCardsToDiscardPile(IReadOnlyList<MaterialModel> cards)
    {
        using (Rules.Enter()) return ReturnHandCardsToDiscardPileCore(cards);
    }

    private int ReturnHandCardsToDiscardPileCore(IReadOnlyList<MaterialModel> cards)
    {
        return ReturnHandCardsToDiscardPile(cards, null);
    }

    public int ReturnHandCardsToDiscardPile(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) return ReturnHandCardsToDiscardPileCore(cards, removedTemporaryCards);
    }

    private int ReturnHandCardsToDiscardPileCore(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        if (cards == null)
            return 0;

        int discardedCount = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            MaterialModel card = cards[i];
            if (card == null)
                continue;

            int index = Hand.IndexOf(card);
            if (index < 0)
                continue;

            Hand.RemoveAt(index);
            card.TriggerOnDiscard();
            TriggerAfterDiscard(card);
            if (card.isTemporary)
            {
                removedTemporaryCards?.Add(card);
                AddConsumedCard(card);
                GameLog.Data($"Refresh removes temporary card {DescribeMaterial(card)}. hand={Hand.Count}");
                continue;
            }

            AddCardToDiscardPile(card);
            discardedCount++;
            GameLog.Data($"Refresh discards card {DescribeMaterial(card)}. hand={Hand.Count} discardPile={DiscardPile.Count}");
        }

        return discardedCount;
    }

    public RefreshHandResult RefreshHandCards(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) return RefreshHandCardsCore(cards, removedTemporaryCards);
    }

    private RefreshHandResult RefreshHandCardsCore(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        return RefreshHandCards(cards, removedTemporaryCards, null);
    }

    public RefreshHandResult RefreshHandCards(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards, BattleManager battleManager)
    {
        using (Rules.Enter()) return RefreshHandCardsCore(cards, removedTemporaryCards, battleManager);
    }

    private RefreshHandResult RefreshHandCardsCore(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards, BattleManager battleManager)
    {
        return RefreshHandCards(cards, removedTemporaryCards, battleManager, null);
    }

    private RefreshHandResult RefreshHandCards(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards, BattleManager battleManager, System.Predicate<MaterialModel> canDraw)
    {
        if (cards == null || cards.Count == 0)
            return new RefreshHandResult(0, 0);

        int refreshCount = cards.Count;
        MaterialModifierModel.CurrentContext = new MaterialModifierContext { PlayerState = this, BattleManager = battleManager };
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
                cards[i].TriggerOnRefresh();
        }
        MaterialModifierModel.CurrentContext = null;

        int discardedCount = ReturnHandCardsToDiscardPile(cards, removedTemporaryCards);
        int drawnCount = 0;
        for (int i = 0; i < refreshCount; i++)
        {
            if (DrawCardToHand(false, canDraw) == null)
                break;
            drawnCount++;
        }
        GameLog.Data($"Refresh hand cards selected={cards.Count} drawn={drawnCount} discarded={discardedCount} temporaryRemoved={removedTemporaryCards?.Count ?? 0}");
        return new RefreshHandResult(drawnCount, discardedCount);
    }

    public void ReturnPlayZoneCardsToDrawPile()
    {
        using (Rules.Enter()) ReturnPlayZoneCardsToDrawPileCore();
    }

    private void ReturnPlayZoneCardsToDrawPileCore()
    {
        ReturnPlayZoneCardsToDrawPile(null);
    }

    public void ReturnPlayZoneCardsToDrawPile(List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) ReturnPlayZoneCardsToDrawPileCore(removedTemporaryCards);
    }

    private void ReturnPlayZoneCardsToDrawPileCore(List<MaterialModel> removedTemporaryCards)
    {
        if (PlayZone.Count == 0)
            return;

        for (int i = 0; i < PlayZone.Count; i++)
        {
            MaterialModel card = PlayZone[i];
            card.TriggerOnPlayedDiscard();
            card.TriggerOnDiscard();
            TriggerAfterDiscard(card);
            if (card.isTemporary)
            {
                removedTemporaryCards?.Add(card);
                AddConsumedCard(card);
                GameLog.Data($"Return play zone removes temporary card {DescribeMaterial(card)}.");
            }
            else
            {
                DrawPile.Add(card);
                GameLog.Data($"Return play zone card {DescribeMaterial(card)} to draw pile. drawPile={DrawPile.Count}");
            }
        }

        PlayZone.Clear();
    }

    public bool ShufflePlayZone()
    {
        using (Rules.Enter()) return ShufflePlayZoneCore();
    }

    private bool ShufflePlayZoneCore()
    {
        if (PlayZone.Count <= 1)
            return false;

        for (int i = PlayZone.Count - 1; i > 0; i--)
        {
            int swapIndex = NextRunRandomInt(0, i + 1);
            MaterialModel temp = PlayZone[i];
            PlayZone[i] = PlayZone[swapIndex];
            PlayZone[swapIndex] = temp;
        }

        GameLog.Data($"Shuffle play zone count={PlayZone.Count}");
        PlayZoneShuffled?.Invoke();
        return true;
    }

    public void ReturnPlayZoneCardsToDiscardPile(List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) ReturnPlayZoneCardsToDiscardPileCore(removedTemporaryCards);
    }

    private void ReturnPlayZoneCardsToDiscardPileCore(List<MaterialModel> removedTemporaryCards)
    {
        if (PlayZone.Count == 0)
            return;

        for (int i = 0; i < PlayZone.Count; i++)
        {
            MaterialModel card = PlayZone[i];
            card.TriggerOnPlayedDiscard();
            card.TriggerOnDiscard();
            TriggerAfterDiscard(card);
            if (card.isTemporary)
            {
                removedTemporaryCards?.Add(card);
                AddConsumedCard(card);
                GameLog.Data($"Return play zone removes temporary card {DescribeMaterial(card)}.");
            }
            else
            {
                AddCardToDiscardPile(card);
                GameLog.Data($"Return play zone card {DescribeMaterial(card)} to discard pile. discardPile={DiscardPile.Count}");
            }
        }

        PlayZone.Clear();
    }

    public void EndTurn()
    {
        using (Rules.Enter()) EndTurnCore();
    }

    private void EndTurnCore()
    {
        EndTurn(null);
    }

    public void EndBattle()
    {
        using (Rules.Enter()) EndBattleCore();
    }

    private void EndBattleCore()
    {
        EndBattle(null);
    }

    public void EndBattle(List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) EndBattleCore(removedTemporaryCards);
    }

    private void EndBattleCore(List<MaterialModel> removedTemporaryCards)
    {
        // 战斗结束进入结算/地图阶段：所有手牌强制弃掉，不保留任何【保留】/【保留手牌】手牌。
        ForceDiscardAll(removedTemporaryCards);
    }

    /// <summary>
    /// 强制弃掉手牌与出牌区的全部箭头，无视【保留】(isRetained) 与【保留手牌】(KeepHandOnEndTurn) 等阻止弃牌的效果。
    /// 用于战斗结束、事件/休息/奖励关收尾以及回到地图阶段前的边界，保证非战斗阶段手中不留任何箭头。
    /// </summary>
    public void ForceDiscardAll(List<MaterialModel> removedTemporaryCards = null)
    {
        using (Rules.Enter()) ForceDiscardAllCore(removedTemporaryCards);
    }

    private void ForceDiscardAllCore(List<MaterialModel> removedTemporaryCards)
    {
        EndTurnInternal(removedTemporaryCards, force: true);
    }

    public void EndTurn(List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) EndTurnCore(removedTemporaryCards);
    }

    private void EndTurnCore(List<MaterialModel> removedTemporaryCards)
    {
        EndTurnInternal(removedTemporaryCards, force: false);
    }

    internal void DiscardHandOnConfirm(List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) DiscardHand(removedTemporaryCards, force: false);
    }

    private void EndTurnInternal(List<MaterialModel> removedTemporaryCards, bool force)
    {
        DiscardHand(removedTemporaryCards, force);
        ReturnPlayZoneCardsToDiscardPile(removedTemporaryCards);
    }

    private void DiscardHand(List<MaterialModel> removedTemporaryCards, bool force)
    {
        // Only the cards present at confirmation are candidates. Discard hooks can create new cards.
        var candidates = new List<MaterialModel>(Hand);
        int keepHand = force ? 0 : GetBuffStack(BuffEnum.KeepHand);
        bool keepAll = !force && (KeepHandOnEndTurn || keepHand > 0);
        KeepHandOnEndTurn = false;
        if (keepHand > 0)
            ConsumeBuff(BuffEnum.KeepHand, keepHand);
        if (keepAll)
            return;

        IsEndingTurn = true;
        try
        {
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                MaterialModel card = candidates[i];
                if ((!force && card != null && card.isRetained) || !Hand.Remove(card) || card == null)
                    continue;

                card.TriggerOnDiscard();
                TriggerAfterDiscard(card);
                if (card.isTemporary)
                {
                    removedTemporaryCards?.Add(card);
                    AddConsumedCard(card);
                }
                else
                {
                    AddCardToDiscardPile(card);
                }
            }
        }
        finally
        {
            IsEndingTurn = false;
        }
    }

    public void TakeDamage(int damage)
    {
        using (Rules.Enter()) TakeDamageCore(damage);
    }

    private void TakeDamageCore(int damage)
    {
        TakeDamage(damage, null);
    }

    public int TakeDamage(int damage, CombatantModel attacker)
    {
        using (Rules.Enter()) return TakeDamageCore(damage, attacker);
    }

    private int TakeDamageCore(int damage, CombatantModel attacker)
    {
        return TakeDamageResult(damage, attacker).HealthDamage;
    }

    public CombatDamageResult TakeDamageResult(int damage, CombatantModel attacker)
    {
        using (Rules.Enter()) return TakeDamageResultCore(damage, attacker);
    }

    private CombatDamageResult TakeDamageResultCore(int damage, CombatantModel attacker)
    {
        using (Relics.BeginOperation())
        {
        CombatDamageResult result = new CombatDamageResult { RawDamage = damage };
        if (damage <= 0)
            return result;

        int remainingDamage = damage;
        TriggerOnTakeDamage(attacker, ref remainingDamage);
        if (remainingDamage <= 0)
            return result;

        result.FinalDamage = remainingDamage;
        int healthBefore = CurrentHealth;
        int blockedDamage = 0;
        if (Shield > 0)
        {
            blockedDamage = remainingDamage < Shield ? remainingDamage : Shield;
            Shield -= blockedDamage;
            remainingDamage -= blockedDamage;
        }

        CurrentHealth -= remainingDamage;
        if (CurrentHealth < 0)
            CurrentHealth = 0;
        int healthDamage = healthBefore - CurrentHealth;
        if (healthDamage > 0)
            LastDamageSourceEnemy = attacker != null && attacker.IsEnemy ? attacker.Enemy : null;
        result.ShieldDamage = blockedDamage;
        result.HealthDamage = healthDamage;
        Services.OnDamageTaken(healthDamage);
        result.TargetDied = healthBefore > 0 && CurrentHealth <= 0;
        GameLog.Data($"Player take damage raw={damage} final={result.FinalDamage} finalHealthDamage={healthDamage} shieldNow={Shield} hp={CurrentHealth}/{MaxHealth}");
        Services.OnDamage(healthDamage, blockedDamage);

        Relics.Publish(new RelicEvent(RelicEventKind.DamageResolved, attacker, new CombatantModel(this),
            result.HealthDamage, damageKind: Relics.DamageKind, shieldDamage: result.ShieldDamage, originRelicId: Relics.OriginRelicId,
            rawDamage: result.RawDamage, finalDamage: result.FinalDamage, targetDied: result.TargetDied));
        TriggerAfterTakeDamage(attacker, result);
        if (result.TargetDied)
            TriggerOnDie(attacker);
        return result;
        }
    }

    public void RestoreCurrentHealth(int currentHealth)
    {
        using (Rules.Enter()) RestoreCurrentHealthCore(currentHealth);
    }

    private void RestoreCurrentHealthCore(int currentHealth)
    {
        CurrentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
    }

    public int TakeDirectDamage(int damage)
    {
        using (Rules.Enter()) return TakeDirectDamageCore(damage);
    }

    private int TakeDirectDamageCore(int damage)
    {
        using (Relics.BeginOperation())
        {
        if (damage <= 0)
            return 0;

        int healthBefore = CurrentHealth;
        CurrentHealth -= damage;
        if (CurrentHealth < 0)
            CurrentHealth = 0;
        int healthDamage = healthBefore - CurrentHealth;
        if (healthDamage > 0)
            LastDamageSourceEnemy = null;
        GameLog.Data($"Player take direct damage={damage} hp={CurrentHealth}/{MaxHealth}");
        Services.OnDamage(healthDamage, 0);

        if (healthBefore > 0 && CurrentHealth <= 0)
            TriggerOnDie(null);

        Relics.Publish(new RelicEvent(RelicEventKind.HealthLost, target: new CombatantModel(this), amount: healthDamage,
            damageKind: RelicDamageKind.HealthLoss, originRelicId: Relics.OriginRelicId));
        return healthDamage;
        }
    }

    public void Heal(int amount)
    {
        using (Rules.Enter()) HealCore(amount);
    }

    private void HealCore(int amount)
    {
        if (amount <= 0)
            return;

        int before = CurrentHealth;
        CurrentHealth += amount;
        if (CurrentHealth > MaxHealth)
            CurrentHealth = MaxHealth;
        GameLog.Data($"Player heal amount={amount} hp={CurrentHealth}/{MaxHealth}");
        if (CurrentHealth > before)
            Relics.Publish(new RelicEvent(RelicEventKind.Healed, target: new CombatantModel(this), amount: CurrentHealth - before, originRelicId: Relics.OriginRelicId));
    }

    public void IncreaseMaxHealth(int amount)
    {
        using (Rules.Enter()) IncreaseMaxHealthCore(amount);
    }

    private void IncreaseMaxHealthCore(int amount)
    {
        if (amount <= 0)
            return;

        MaxHealth += amount;
        CurrentHealth += amount;
        if (CurrentHealth > MaxHealth)
            CurrentHealth = MaxHealth;
        GameLog.Data($"Player max health change={amount} hp={CurrentHealth}/{MaxHealth}");
    }

    public void IncreaseMaxHealthOnly(int amount)
    {
        using (Rules.Enter()) IncreaseMaxHealthOnlyCore(amount);
    }

    private void IncreaseMaxHealthOnlyCore(int amount)
    {
        if (amount <= 0)
            return;

        MaxHealth += amount;
        if (CurrentHealth > MaxHealth)
            CurrentHealth = MaxHealth;
        GameLog.Data($"Player max health only change={amount} hp={CurrentHealth}/{MaxHealth}");
    }

    public void AdjustMaxHealthOnly(int amount)
    {
        using (Rules.Enter()) AdjustMaxHealthOnlyCore(amount);
    }

    private void AdjustMaxHealthOnlyCore(int amount)
    {
        if (amount == 0)
            return;

        MaxHealth = Mathf.Max(1, MaxHealth + amount);
        if (CurrentHealth > MaxHealth)
            CurrentHealth = MaxHealth;
        GameLog.Data($"Player max health adjust={amount} hp={CurrentHealth}/{MaxHealth}");
    }

    public int GainShield(int amount)
    {
        using (Rules.Enter()) return GainShieldCore(amount);
    }

    private int GainShieldCore(int amount)
    {
        using (Relics.BeginOperation())
        {
        if (amount <= 0)
            return 0;

        int shieldValue = amount;
        int slowReduction = ApplyGainShieldModifiers(ref shieldValue);
        if (shieldValue <= 0)
        {
            if (slowReduction > 0)
                ConsumeBuff(BuffEnum.Slow, slowReduction);
            return 0;
        }

        Shield += shieldValue;
        if (slowReduction > 0)
            ConsumeBuff(BuffEnum.Slow, slowReduction);
        GameLog.Data($"Player gain shield={shieldValue} shield={Shield}");
        Relics.Publish(new RelicEvent(RelicEventKind.ShieldGained, target: new CombatantModel(this), amount: shieldValue, originRelicId: Relics.OriginRelicId));
        return shieldValue;
        }
    }

    public int ConsumeShield(int amount)
    {
        using (Rules.Enter()) return ConsumeShieldCore(amount);
    }

    private int ConsumeShieldCore(int amount)
    {
        if (amount <= 0 || Shield <= 0)
            return 0;

        int consumed = amount < Shield ? amount : Shield;
        Shield -= consumed;
        GameLog.Data($"Player consume shield={consumed} shield={Shield}");
        return consumed;
    }

    public void ClearShield()
    {
        using (Rules.Enter()) ClearShieldCore();
    }

    private void ClearShieldCore()
    {
        Shield = 0;
        GameLog.Data("Player clear shield");
    }

    public IReadOnlyCollection<MaterialModel> PlayLimitChargedCards => new List<MaterialModel>(playLimitChargedCards).AsReadOnly();

    public void RestorePlayLimitChargedCards(IEnumerable<MaterialModel> cards)
    {
        using (Rules.Enter()) RestorePlayLimitChargedCardsCore(cards);
    }

    private void RestorePlayLimitChargedCardsCore(IEnumerable<MaterialModel> cards)
    {
        playLimitChargedCards.Clear();
        if (cards != null)
            foreach (MaterialModel card in cards)
                if (card != null && PlayZone.Contains(card)) playLimitChargedCards.Add(card);
    }

    public void RestoreCombatSnapshot(int shield, IReadOnlyList<MaterialModel> hand, IReadOnlyList<MaterialModel> drawPile, IReadOnlyList<MaterialModel> discardPile, IReadOnlyList<MaterialModel> playZone, IReadOnlyList<MaterialModel> consumedPile, IReadOnlyList<MaterialModel> temporaryMaterialsNextTurn, int extraRefreshChancesThisTurn = 0, int playedCardCountThisTurn = 0)
    {
        using (Rules.Enter()) RestoreCombatSnapshotCore(shield, hand, drawPile, discardPile, playZone, consumedPile, temporaryMaterialsNextTurn, extraRefreshChancesThisTurn, playedCardCountThisTurn);
    }

    private void RestoreCombatSnapshotCore(int shield, IReadOnlyList<MaterialModel> hand, IReadOnlyList<MaterialModel> drawPile, IReadOnlyList<MaterialModel> discardPile, IReadOnlyList<MaterialModel> playZone, IReadOnlyList<MaterialModel> consumedPile, IReadOnlyList<MaterialModel> temporaryMaterialsNextTurn, int extraRefreshChancesThisTurn, int playedCardCountThisTurn)
    {
        Shield = Mathf.Max(0, shield);
        this.extraRefreshChancesThisTurn = Mathf.Max(0, extraRefreshChancesThisTurn);
        PlayedCardCountThisTurn = Mathf.Max(0, playedCardCountThisTurn);
        playLimitChargedCards.Clear();
        Hand.Clear();
        DrawPile.Clear();
        DiscardPile.Clear();
        PlayZone.Clear();
        ConsumedPile.Clear();
        TemporaryMaterialsNextTurn.Clear();
        AddCombatCards(Hand, hand, false);
        AddCombatCards(DrawPile, drawPile, false);
        AddCombatCards(DiscardPile, discardPile, false);
        AddCombatCards(PlayZone, playZone, true);
        AddCombatCards(ConsumedPile, consumedPile, false);
        AddCombatCards(TemporaryMaterialsNextTurn, temporaryMaterialsNextTurn, false);

        // 读档恢战：出牌区里未消耗的箭头按“本回合已打出”重新记账，使其点回手牌时能正常退还额度。
        for (int i = 0; i < PlayZone.Count; i++)
        {
            MaterialModel card = PlayZone[i];
            if (card != null && !card.IgnoresPlayLimit())
                playLimitChargedCards.Add(card);
        }
    }

    private static void AddCombatCards(List<MaterialModel> target, IReadOnlyList<MaterialModel> source, bool isPlayed)
    {
        for (int i = 0; source != null && i < source.Count; i++)
        {
            MaterialModel card = source[i];
            if (card == null)
                continue;
            card.isPlayed = isPlayed;
            target.Add(card);
        }
    }

    public void RestoreBattleBuffs(BuffRuntimeSaveData[] saved)
    {
        using (Rules.Enter()) RestoreBattleBuffsCore(saved);
    }

    private void RestoreBattleBuffsCore(BuffRuntimeSaveData[] saved)
    {
        if (saved == null) return; // v3: retain legacy prepared-state fallback.
        buffs.Clear();
        foreach (var value in saved)
        {
            if (value == null || value.type == BuffEnum.None || value.stack <= 0) continue;
            var buff = BuffModel.Create(value.type, value.stack);
            buff.RestoreRuntimeState(value.values);
            buffs[value.type] = buff;
        }
    }

    public void ClearBuffs()
    {
        using (Rules.Enter()) ClearBuffsCore();
    }

    private void ClearBuffsCore()
    {
        buffs.Clear();
        GameLog.Data("Player clear buffs");
    }

    public void ClearCombatState()
    {
        using (Rules.Enter()) ClearCombatStateCore();
    }

    private void ClearCombatStateCore()
    {
        RestoreBattleDrawOrder(0, 0);
        ClearShield();
        ClearBuffs();
        RemoveBattleOnlyArrowState();
        TemporaryMaterialsNextTurn.Clear();
        ConsumedPile.Clear();
        extraRefreshChancesThisTurn = 0;
        RefreshLimitReductionThisTurn = 0;
        HandLimitThisTurn = int.MaxValue;
        ResetPlayedCardCountThisTurn();
    }

    public void RemoveBattleOnlyArrowState()
    {
        using (Rules.Enter()) RemoveBattleOnlyArrowStateCore();
    }

    private void RemoveBattleOnlyArrowStateCore()
    {
        RemoveBattleOnlyArrowState(Hand);
        RemoveBattleOnlyArrowState(PlayZone);
        RemoveBattleOnlyArrowState(DrawPile);
        RemoveBattleOnlyArrowState(DiscardPile);
        RemoveBattleOnlyArrowState(ConsumedPile);
        RemoveBattleOnlyArrowState(Deck);
        RemoveBattleOnlyArrowState(TemporaryMaterialsNextTurn);
    }

    private static void RemoveBattleOnlyArrowState(List<MaterialModel> cards)
    {
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            MaterialModel card = cards[i];
            if (card == null)
                continue;

            if (card.ShouldRemoveAfterBattle())
            {
                cards.RemoveAt(i);
                continue;
            }

            card.RemoveBattleOnlyModifiers();
            card.ClearPackedCards();
        }
    }

    public void ApplySturdyToHand()
    {
        using (Rules.Enter()) ApplySturdyToHandCore();
    }

    private void ApplySturdyToHandCore()
    {
        for (int i = 0; i < Hand.Count; i++)
        {
            MaterialModel card = Hand[i];
            bool hasSturdy = false;
            for (int j = 0; j < card.modifiers.Count; j++)
            {
                if (card.modifiers[j] is SturdyModifier)
                {
                    hasSturdy = true;
                    break;
                }
            }
            if (!hasSturdy)
            {
                SturdyModifier modifier = new SturdyModifier();
                modifier.MarkRemoveAfterBattle();
                card.AddModifier(modifier);
            }
        }
    }

    public void TriggerMaterialBegin()
    {
        using (Rules.Enter()) TriggerMaterialBeginCore();
    }

    private void TriggerMaterialBeginCore()
    {
        for (int i = 0; i < PlayZone.Count; i++)
            PlayZone[i].TriggerOnBegin();
    }

    public void TriggerMaterialEnd()
    {
        using (Rules.Enter()) TriggerMaterialEndCore();
    }

    private void TriggerMaterialEndCore()
    {
        for (int i = 0; i < PlayZone.Count; i++)
            PlayZone[i].TriggerOnEnd();
    }

    public void RemoveTurnOnlyModifiers()
    {
        using (Rules.Enter()) RemoveTurnOnlyModifiersCore();
    }

    private void RemoveTurnOnlyModifiersCore()
    {
        RemoveTurnOnlyModifiers(Hand);
        RemoveTurnOnlyModifiers(PlayZone);
        RemoveTurnOnlyModifiers(DrawPile);
        RemoveTurnOnlyModifiers(DiscardPile);
        RemoveTurnOnlyModifiers(ConsumedPile);
        RemoveTurnOnlyModifiers(TemporaryMaterialsNextTurn);
    }

    /// <summary>
    /// 移除禁用附魔：material 为 None 时清除全部，否则只清该方向。
    /// 用于属性禁用 Buff 被覆盖或提前移除时，使 Buff 显示与实际情况保持一致。
    /// </summary>
    public void ClearDisabledArrowModifiers(MaterialEnum material)
    {
        using (Rules.Enter()) ClearDisabledArrowModifiersCore(material);
    }

    private void ClearDisabledArrowModifiersCore(MaterialEnum material)
    {
        ClearDisabledArrowModifiersIn(Hand, material);
        ClearDisabledArrowModifiersIn(PlayZone, material);
        ClearDisabledArrowModifiersIn(DrawPile, material);
        ClearDisabledArrowModifiersIn(DiscardPile, material);
        ClearDisabledArrowModifiersIn(ConsumedPile, material);
        ClearDisabledArrowModifiersIn(Deck, material);
        ClearDisabledArrowModifiersIn(TemporaryMaterialsNextTurn, material);
    }

    private static void ClearDisabledArrowModifiersIn(List<MaterialModel> cards, MaterialEnum material)
    {
        for (int i = 0; cards != null && i < cards.Count; i++)
        {
            MaterialModel card = cards[i];
            if (card == null)
                continue;
            if (material != MaterialEnum.None && card.material != material)
                continue;

            card.RemoveModifiers<DisabledArrowModifier>();
        }
    }

    private static void RemoveTurnOnlyModifiers(List<MaterialModel> cards)
    {
        if (cards == null)
            return;

        for (int i = 0; i < cards.Count; i++)
            cards[i]?.RemoveTurnOnlyModifiers();
    }

    /// <summary>
    /// 开局把金币直接设成固定值（教程等特殊运行使用）：不走进阶金币倍率、不播放音效。
    /// 普通新局仍走起始配置的初始金币。
    /// </summary>
    public void SetStartingGold(int gold)
    {
        using (Rules.Enter()) SetStartingGoldCore(gold);
    }

    private void SetStartingGoldCore(int gold)
    {
        Gold = Mathf.Max(0, gold);
        GameLog.Data($"Player starting gold set to {Gold}");
    }

    public void AddGold(int amount, bool applyDifficulty = true, RelicGoldReason reason = RelicGoldReason.Reward, bool playFeedback = true)
    {
        using (Rules.Enter()) AddGoldCore(amount, applyDifficulty, reason, playFeedback);
    }

    private void AddGoldCore(int amount, bool applyDifficulty, RelicGoldReason reason, bool playFeedback)
    {
        int goldBefore = Gold;
        if (amount > 0 && applyDifficulty)
            amount = DifficultyUpgradeSystem.ModifyGoldGain(amount);
        if (amount > 0 && reason == RelicGoldReason.Reward)
            amount = Relics.ModifyGoldGain(amount);
        if (amount == 0)
            return;

        Gold += amount;
        if (Gold < 0)
            Gold = 0;
        GameLog.Data($"Player gold change={amount} gold={Gold}");
        if (amount > 0 && playFeedback)
            Services.OnGoldGained();
        Relics.Publish(new RelicEvent(RelicEventKind.GoldChanged, amount: Gold - goldBefore, before: goldBefore, after: Gold, originRelicId: Relics.OriginRelicId));
    }

    public void AddBuff(BuffEnum buffType, int stack)
    {
        using (Rules.Enter()) AddBuffCore(buffType, stack);
    }

    private void AddBuffCore(BuffEnum buffType, int stack)
    {
        AddBuff(buffType, stack, null);
    }

    public void AddBuff(BuffEnum buffType, int stack, CombatantModel source)
    {
        using (Rules.Enter()) AddBuffCore(buffType, stack, source);
    }

    private void AddBuffCore(BuffEnum buffType, int stack, CombatantModel source)
    {
        using (Relics.BeginOperation())
        {
        int before = GetBuffStack(buffType);
        if (buffType == BuffEnum.None || stack <= 0)
            return;

        CombatantModel self = new CombatantModel(this);
        ModifyIncomingBuff(source, self, buffType, ref stack);
        if (stack <= 0)
            return;

        if (buffType == BuffEnum.AttributeDisabled)
        {
            int previousMaterial = 0;
            if (buffs.TryGetValue(buffType, out BuffModel existingAttributeBuff))
            {
                previousMaterial = existingAttributeBuff.stack;
                existingAttributeBuff.stack = stack;
            }
            else
            {
                buffs.Add(buffType, BuffModel.Create(buffType, stack));
            }

            // 方向被改写时，同步解除旧方向的禁用附魔，避免 Buff 显示与实际可打出状态不一致。
            if (previousMaterial > 0 && previousMaterial != stack)
                ClearDisabledArrowModifiers((MaterialEnum)previousMaterial);

            int appliedAfter = GetBuffStack(buffType);
            Relics.RecordBuffChange(self, buffs[buffType], before, appliedAfter);
            GameLog.Data($"Player add buff {buffType} material={stack} now={GetBuffStack(buffType)}");
            BuffAdded?.Invoke(buffType, stack);
            TriggerAfterGiveBuff(source, self, buffType, stack);
            Relics.Publish(new RelicEvent(RelicEventKind.BuffApplied, source, self, stack, before, appliedAfter, buffType, originRelicId: Relics.OriginRelicId));
            return;
        }

        if (buffs.TryGetValue(buffType, out BuffModel buff))
            buff.AddStack(stack);
        else
            buffs.Add(buffType, BuffModel.Create(buffType, stack));
        int after = GetBuffStack(buffType);
        Relics.RecordBuffChange(self, buffs[buffType], before, after);
        GameLog.Data($"Player add buff {buffType} stack+={stack} now={GetBuffStack(buffType)}");
        BuffAdded?.Invoke(buffType, stack);
        TriggerAfterGiveBuff(source, self, buffType, stack);
        Relics.Publish(new RelicEvent(RelicEventKind.BuffApplied, source, self, stack, before, after, buffType, originRelicId: Relics.OriginRelicId));
        }
    }

    private void TriggerAfterGiveBuff(CombatantModel source, CombatantModel target, BuffEnum buffType, int stack)
    {
        if (source == null || source.Buffs == null || source.Buffs.Count == 0)
            return;

        List<BuffModel> sourceBuffs = new List<BuffModel>(source.Buffs.Values);
        sourceBuffs.Sort((a, b) => a.buffType.CompareTo(b.buffType));
        for (int i = 0; i < sourceBuffs.Count; i++)
            sourceBuffs[i].AfterGiveBuff(source, target, buffType, stack);
    }

    private void ModifyIncomingBuff(CombatantModel source, CombatantModel self, BuffEnum buffType, ref int stack)
    {
        if (source != null && source.Buffs != null && source.Buffs.Count > 0)
        {
            List<BuffModel> sourceBuffs = new List<BuffModel>(source.Buffs.Values);
            sourceBuffs.Sort((a, b) => a.buffType.CompareTo(b.buffType));
            for (int i = 0; i < sourceBuffs.Count; i++)
                sourceBuffs[i].OnGiveBuff(source, self, buffType, ref stack);
        }

        if (source?.Player != null) stack = source.Player.Relics.ModifyBuffGiven(buffType, stack);
        if (buffs.Count > 0)
        {
            List<BuffModel> targetBuffs = new List<BuffModel>(buffs.Values);
            targetBuffs.Sort((a, b) => a.buffType.CompareTo(b.buffType));
            for (int i = 0; i < targetBuffs.Count; i++)
                targetBuffs[i].OnReceiveBuff(self, source, buffType, ref stack);
        }
        stack = Relics.ModifyBuffReceived(buffType, stack);
    }

    public int GetBuffStack(BuffEnum buffType)
    {
        using (Rules.Enter()) return GetBuffStackCore(buffType);
    }

    private int GetBuffStackCore(BuffEnum buffType)
    {
        if (buffs.TryGetValue(buffType, out BuffModel buff))
            return buff.stack;

        return 0;
    }

    public void ConsumeBuff(BuffEnum buffType, int amount)
    {
        using (Rules.Enter()) ConsumeBuffCore(buffType, amount);
    }

    private void ConsumeBuffCore(BuffEnum buffType, int amount)
    {
        using (Relics.BeginOperation())
        {
        if (buffs.TryGetValue(buffType, out BuffModel buff))
        {
            int before = buff.stack;
            buff.ConsumeStack(amount);
            Relics.RecordBuffChange(new CombatantModel(this), buff, before, buff.stack);
            if (buff.stack <= 0)
            {
                buff.OnExpire(new CombatantModel(this), null);
                buffs.Remove(buffType);
                GameLog.Data($"Player buff expired {buffType}");
            }
        }
        }
    }

    public void TriggerOnTurnStart(CombatantModel opponent)
    {
        using (Rules.Enter()) TriggerOnTurnStartCore(opponent);
    }

    private void TriggerOnTurnStartCore(CombatantModel opponent)
    {
        ResetKeepHandOnEndTurn();
        TriggerBuffs(opponent, (buff, self, target) => buff.OnTurnStart(self, target));
    }

    public void TriggerAfterTurnStart(CombatantModel opponent)
    {
        using (Rules.Enter()) TriggerAfterTurnStartCore(opponent);
    }

    private void TriggerAfterTurnStartCore(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.AfterTurnStart(self, target));
    }

    public void TriggerAfterTurnStartDraw(CombatantModel opponent, int drawCount)
    {
        using (Rules.Enter()) TriggerAfterTurnStartDrawCore(opponent, drawCount);
    }

    private void TriggerAfterTurnStartDrawCore(CombatantModel opponent, int drawCount)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.AfterTurnStartDraw(self, target, drawCount));
    }

    public void TriggerAfterDraw(MaterialModel card)
    {
        using (Rules.Enter()) TriggerAfterDrawCore(card);
    }

    private void TriggerAfterDrawCore(MaterialModel card)
    {
        TriggerBuffs(null, (buff, self, target) => buff.AfterDraw(self, card));
        ArrowUpgradeSystem.TriggerOnDraw(this, card, Battle);
        Relics.Publish(new RelicEvent(RelicEventKind.CardDrawn, card: card, originRelicId: Relics.OriginRelicId));
    }

    /// <summary>箭头进入手牌的统一切点，用于由 Buff 给新手牌补打附魔。</summary>
    public void TriggerAfterEnterHand(MaterialModel card)
    {
        using (Rules.Enter()) TriggerAfterEnterHandCore(card);
    }

    private void TriggerAfterEnterHandCore(MaterialModel card)
    {
        TriggerBuffs(null, (buff, self, target) => buff.AfterEnterHand(self, card));
        Relics.Publish(new RelicEvent(RelicEventKind.CardEnteredHand, card: card, originRelicId: Relics.OriginRelicId));
    }

    public int DrawArrowUpgradeBonusCard(int count = 1)
    {
        using (Rules.Enter()) return DrawArrowUpgradeBonusCardCore(count);
    }

    private int DrawArrowUpgradeBonusCardCore(int count)
    {
        int drawn = 0;
        for (int i = 0; i < count; i++)
        {
            if (DrawCardToHand(true) != null)
                drawn++;
        }
        return drawn;
    }

    public void TriggerAfterDiscard(MaterialModel card)
    {
        using (Rules.Enter()) TriggerAfterDiscardCore(card);
    }

    private void TriggerAfterDiscardCore(MaterialModel card)
    {
        TriggerBuffs(null, (buff, self, target) => buff.AfterDiscard(self, card));
    }

    public void AddCardToDiscardPile(MaterialModel card)
    {
        using (Rules.Enter()) AddCardToDiscardPileCore(card);
    }

    private void AddCardToDiscardPileCore(MaterialModel card)
    {
        if (card == null)
            return;

        DiscardPile.Add(card);
        ArrowUpgradeSystem.TriggerOnDiscard(this, card, Battle);
    }

    public void TriggerAfterMaterialConsumed(MaterialModel card)
    {
        using (Rules.Enter()) TriggerAfterMaterialConsumedCore(card);
    }

    private void TriggerAfterMaterialConsumedCore(MaterialModel card)
    {
        TriggerBuffs(null, (buff, self, target) => buff.AfterMaterialConsumed(self, card));
    }

    public void TriggerAfterArrowConsumed(MaterialModel card)
    {
        using (Rules.Enter()) TriggerAfterArrowConsumedCore(card);
    }

    private void TriggerAfterArrowConsumedCore(MaterialModel card)
    {
        TriggerBuffs(null, (buff, self, target) => buff.AfterArrowConsumed(self, card));
        Relics.Publish(new RelicEvent(RelicEventKind.ArrowConsumed, card: card, originRelicId: Relics.OriginRelicId));
    }

    public void TriggerAfterEnemyBurningDamage(EnemyModel enemy, int damage)
    {
        using (Rules.Enter()) TriggerAfterEnemyBurningDamageCore(enemy, damage);
    }

    private void TriggerAfterEnemyBurningDamageCore(EnemyModel enemy, int damage)
    {
        if (damage > 0)
            TriggerBuffs(enemy != null ? new CombatantModel(enemy) : null, (buff, self, target) => buff.AfterEnemyBurningDamage(self, enemy, damage));
    }

    public void TriggerOnInvoke(CombatantModel target)
    {
        using (Rules.Enter()) TriggerOnInvokeCore(target);
    }

    private void TriggerOnInvokeCore(CombatantModel target)
    {
        TriggerBuffs(target, (buff, self, opponent) => buff.OnInvoke(self, opponent));
    }

    public void TriggerAfterPlayerDecide(CombatantModel opponent)
    {
        using (Rules.Enter()) TriggerAfterPlayerDecideCore(opponent);
    }

    private void TriggerAfterPlayerDecideCore(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.AfterPlayerDecide(self, target));
    }

    public void TriggerOnGetAction(CombatantModel opponent)
    {
        using (Rules.Enter()) TriggerOnGetActionCore(opponent);
    }

    private void TriggerOnGetActionCore(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnGetAction(self, target));
    }

    public void TriggerAfterGetAction(CombatantModel opponent)
    {
        using (Rules.Enter()) TriggerAfterGetActionCore(opponent);
    }

    private void TriggerAfterGetActionCore(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.AfterGetAction(self, target));
    }

    public void TriggerOnAttack(CombatantModel target, ref int attackValue)
    {
        using (Rules.Enter()) TriggerOnAttackCore(target, ref attackValue);
    }

    private void TriggerOnAttackCore(CombatantModel target, ref int attackValue)
    {

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].OnAttack(self, target, ref attackValue);
        attackValue = Relics.ModifyAttack(attackValue);
    }

    public void TriggerAfterAttack(CombatantModel attacker, ref int attackResult)
    {
        using (Rules.Enter()) TriggerAfterAttackCore(attacker, ref attackResult);
    }

    private void TriggerAfterAttackCore(CombatantModel attacker, ref int attackResult)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
        {
            var buff = snapshot[i];
            int value = attackResult;
            Relics.ObserveBuffHook(self, buff, () => buff.AfterAttack(self, attacker, ref value));
            attackResult = value;
        }
    }

    public void TriggerOnTakeDamage(CombatantModel attacker, ref int damage)
    {
        using (Rules.Enter()) TriggerOnTakeDamageCore(attacker, ref damage);
    }

    private void TriggerOnTakeDamageCore(CombatantModel attacker, ref int damage)
    {

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].OnTakeDamage(self, attacker, ref damage);
        damage = Relics.ModifyDamageTaken(damage);
    }

    public void TriggerAfterTakeDamage(CombatantModel attacker, CombatDamageResult result)
    {
        using (Rules.Enter()) TriggerAfterTakeDamageCore(attacker, result);
    }

    private void TriggerAfterTakeDamageCore(CombatantModel attacker, CombatDamageResult result)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
        {
            var buff = snapshot[i];
            Relics.ObserveBuffHook(self, buff, () => buff.AfterTakeDamage(self, attacker, result));
        }
    }

    public void TriggerOnGainShield(ref int shieldValue)
    {
        using (Rules.Enter()) TriggerOnGainShieldCore(ref shieldValue);
    }

    private void TriggerOnGainShieldCore(ref int shieldValue)
    {
        ApplyGainShieldModifiers(ref shieldValue);
    }

    private int ApplyGainShieldModifiers(ref int shieldValue)
    {
        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        BuffModel slowBuff = null;
        for (int i = 0; i < snapshot.Count; i++)
        {
            BuffModel buff = snapshot[i];
            if (!buffs.TryGetValue(buff.buffType, out BuffModel currentBuff) || !ReferenceEquals(currentBuff, buff))
                continue;
            if (buff.buffType == BuffEnum.Slow)
            {
                slowBuff = buff;
                continue;
            }

            buff.OnGainShield(self, ref shieldValue);
        }

        shieldValue = Relics.ModifyShieldGain(shieldValue);
        int beforeSlow = shieldValue;
        if (slowBuff != null && buffs.TryGetValue(slowBuff.buffType, out BuffModel currentSlow) && ReferenceEquals(currentSlow, slowBuff))
            slowBuff.OnGainShield(self, ref shieldValue);
        if (shieldValue < 0)
            shieldValue = 0;
        return beforeSlow > shieldValue ? beforeSlow - shieldValue : 0;
    }

    public void TriggerOnTurnEnd(CombatantModel opponent)
    {
        using (Rules.Enter()) TriggerOnTurnEndCore(opponent);
    }

    private void TriggerOnTurnEndCore(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnTurnEnd(self, target));
    }

    public void TriggerAfterTurnEnd(CombatantModel opponent)
    {
        using (Rules.Enter()) TriggerAfterTurnEndCore(opponent);
    }

    private void TriggerAfterTurnEndCore(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.AfterTurnEnd(self, target));
    }

    public void TriggerOnDie(CombatantModel opponent)
    {
        using (Rules.Enter()) TriggerOnDieCore(opponent);
    }

    private void TriggerOnDieCore(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnDie(self, target));
    }

    private void TriggerBuffs(CombatantModel opponent, BuffTrigger trigger)
    {
        using (Relics.BeginOperation())
        {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        List<BuffModel> expiredBuffs = null;
        for (int i = 0; i < snapshot.Count; i++)
        {
            BuffModel buff = snapshot[i];
            if (!buffs.TryGetValue(buff.buffType, out BuffModel currentBuff) || !ReferenceEquals(currentBuff, buff))
                continue;

            Relics.ObserveBuffHook(self, buff, () => trigger(buff, self, opponent));
            if (buff.stack <= 0 && buffs.TryGetValue(buff.buffType, out currentBuff) && ReferenceEquals(currentBuff, buff))
            {
                if (expiredBuffs == null)
                    expiredBuffs = new List<BuffModel>();
                buff.OnExpire(self, opponent);
                expiredBuffs.Add(buff);
            }
        }

        if (expiredBuffs != null)
        {
            for (int i = 0; i < expiredBuffs.Count; i++)
            {
                BuffModel buff = expiredBuffs[i];
                if (buff.stack <= 0 && buffs.TryGetValue(buff.buffType, out BuffModel currentBuff) && ReferenceEquals(currentBuff, buff))
                    buffs.Remove(buff.buffType);
            }
        }
        }
    }

    private delegate void BuffTrigger(BuffModel buff, CombatantModel self, CombatantModel opponent);

    public bool ReturnLeftmostHandCardToDrawPile()
    {
        using (Rules.Enter()) return ReturnLeftmostHandCardToDrawPileCore();
    }

    private bool ReturnLeftmostHandCardToDrawPileCore()
    {
        return ReturnLeftmostHandCardToDiscardPile(null);
    }

    public bool ReturnLeftmostHandCardToDrawPile(List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) return ReturnLeftmostHandCardToDrawPileCore(removedTemporaryCards);
    }

    private bool ReturnLeftmostHandCardToDrawPileCore(List<MaterialModel> removedTemporaryCards)
    {
        return ReturnLeftmostHandCardToDiscardPile(removedTemporaryCards);
    }

    public bool ReturnLeftmostHandCardToDiscardPile()
    {
        using (Rules.Enter()) return ReturnLeftmostHandCardToDiscardPileCore();
    }

    private bool ReturnLeftmostHandCardToDiscardPileCore()
    {
        return ReturnLeftmostHandCardToDiscardPile(null);
    }

    public bool ReturnLeftmostHandCardToDiscardPile(List<MaterialModel> removedTemporaryCards)
    {
        using (Rules.Enter()) return ReturnLeftmostHandCardToDiscardPileCore(removedTemporaryCards);
    }

    private bool ReturnLeftmostHandCardToDiscardPileCore(List<MaterialModel> removedTemporaryCards)
    {
        if (Hand.Count == 0)
            return false;

        MaterialModel card = Hand[0];
        Hand.RemoveAt(0);
        card.TriggerOnDiscard();
        TriggerAfterDiscard(card);
        if (card.isTemporary)
        {
            removedTemporaryCards?.Add(card);
            AddConsumedCard(card);
            GameLog.Data($"Remove leftmost temporary hand card {DescribeMaterial(card)}");
        }
        else
        {
            AddCardToDiscardPile(card);
            GameLog.Data($"Discard leftmost hand card {DescribeMaterial(card)}. discardPile={DiscardPile.Count}");
        }
        return true;
    }

    public List<MaterialModel> ConsumeTemporaryMaterialsNextTurn()
    {
        using (Rules.Enter()) return ConsumeTemporaryMaterialsNextTurnCore();
    }

    private List<MaterialModel> ConsumeTemporaryMaterialsNextTurnCore()
    {
        List<MaterialModel> addedCards = new List<MaterialModel>();
        for (int i = 0; i < TemporaryMaterialsNextTurn.Count; i++)
        {
            MaterialModel source = TemporaryMaterialsNextTurn[i];
            if (source == null)
                continue;
            if (Hand.Count >= HandLimitThisTurn)
                break;

            Hand.Add(source);
            source.TriggerOnDraw();
            TriggerAfterEnterHand(source);
            TriggerAfterDraw(source);
            addedCards.Add(source);
            GameLog.Data($"Add scheduled material to hand {DescribeMaterial(source)}");
        }

        TemporaryMaterialsNextTurn.Clear();
        return addedCards;
    }

    public MaterialModel AddMaterialNextTurn(MaterialEnum material, MaterialModifierModel modifier)
    {
        using (Rules.Enter()) return AddMaterialNextTurnCore(material, modifier);
    }

    private MaterialModel AddMaterialNextTurnCore(MaterialEnum material, MaterialModifierModel modifier)
    {
        if (material == MaterialEnum.None)
            return null;

        MaterialModel card = new MaterialModel("next_" + material + "_" + temporaryMaterialIndex++, material);
        if (modifier != null)
            card.AddModifier(modifier);
        TemporaryMaterialsNextTurn.Add(card);
        GameLog.Data($"Schedule material next turn {DescribeMaterial(card)}");
        return card;
    }

    public MaterialModel AddTemporaryMaterialNextTurn(MaterialEnum material, bool temporary)
    {
        using (Rules.Enter()) return AddTemporaryMaterialNextTurnCore(material, temporary);
    }

    private MaterialModel AddTemporaryMaterialNextTurnCore(MaterialEnum material, bool temporary)
    {
        if (material == MaterialEnum.None)
            return null;

        MaterialModel card = new MaterialModel("temporary_next_" + material + "_" + temporaryMaterialIndex++, material);
        if (temporary)
            card.AddModifier(new TemporaryModifier());
        TemporaryMaterialsNextTurn.Add(card);
        GameLog.Data($"Schedule temporary material next turn {material}");
        return card;
    }

    public void AddTemporaryMaterialNextTurn(MaterialEnum material)
    {
        using (Rules.Enter()) AddTemporaryMaterialNextTurnCore(material);
    }

    private void AddTemporaryMaterialNextTurnCore(MaterialEnum material)
    {
        AddTemporaryMaterialNextTurn(material, true);
    }

    public MaterialModel AddTemporaryMaterialToHand(MaterialEnum material)
    {
        using (Rules.Enter()) return AddTemporaryMaterialToHandCore(material);
    }

    // A grant respects the hand limit and enters the hand without counting as a draw.
    internal MaterialModel GrantTemporaryMaterialToHand(MaterialEnum material)
    {
        using (Rules.Enter())
        {
            if (Hand.Count >= HandLimitThisTurn)
                return null;
            return AddTemporaryMaterialToHandCore(material, isDraw: false);
        }
    }

    private MaterialModel AddTemporaryMaterialToHandCore(MaterialEnum material, bool isDraw = true)
    {
        if (material == MaterialEnum.None)
            return null;

        MaterialModel card = new MaterialModel("temporary_" + material + "_" + temporaryMaterialIndex++, material);
        card.AddModifier(new TemporaryModifier());
        Hand.Add(card);
        if (isDraw)
            card.TriggerOnDraw();
        TriggerAfterEnterHand(card);
        if (isDraw)
            TriggerAfterDraw(card);
        GameLog.Data($"Add temporary material to hand {DescribeMaterial(card)}");
        return card;
    }

    public MaterialModel AddDeckMaterial(MaterialEnum material)
    {
        using (Rules.Enter()) return AddDeckMaterialCore(material);
    }

    private MaterialModel AddDeckMaterialCore(MaterialEnum material)
    {
        return AddDeckMaterial(material, null);
    }

    public MaterialModel AddDeckMaterial(MaterialEnum material, MaterialModifierModel modifier)
    {
        using (Rules.Enter()) return AddDeckMaterialCore(material, modifier);
    }

    private MaterialModel AddDeckMaterialCore(MaterialEnum material, MaterialModifierModel modifier)
    {
        if (material == MaterialEnum.None)
            return null;

        MaterialModel card = new MaterialModel("deck_" + material + "_" + temporaryMaterialIndex++, material);
        if (modifier != null)
            card.AddModifier(modifier);
        Deck.Add(card);
        DrawPile.Add(card);
        GameLog.Data($"Add deck material {DescribeMaterial(card)}");
        return card;
    }

    public MaterialModel AddDeckPlaceholderMaterial()
    {
        using (Rules.Enter()) return AddDeckPlaceholderMaterialCore();
    }

    private MaterialModel AddDeckPlaceholderMaterialCore()
    {
        MaterialModel card = new MaterialModel(DeckPlaceholderInstanceIdPrefix + temporaryMaterialIndex++, MaterialEnum.None);
        Deck.Add(card);
        DrawPile.Add(card);
        GameLog.Data($"Add deck placeholder {DescribeMaterial(card)}");
        return card;
    }

    /// <summary>占位符卡（无基础效果、由进阶负担生成）不能被永久删除。</summary>
    public static bool IsDeckPlaceholderMaterial(MaterialModel card)
    {
        if (card == null)
            return false;

        if (!string.IsNullOrEmpty(card.instanceId) && card.instanceId.StartsWith(DeckPlaceholderInstanceIdPrefix))
            return true;

        return card.material == MaterialEnum.None;
    }

    public bool RemoveCardEverywhere(MaterialModel card)
    {
        using (Rules.Enter()) return RemoveCardEverywhereCore(card);
    }

    private bool RemoveCardEverywhereCore(MaterialModel card)
    {
        if (card == null || IsDeckPlaceholderMaterial(card))
            return false;

        bool removed = false;
        removed |= Deck.Remove(card);
        removed |= DrawPile.Remove(card);
        removed |= DiscardPile.Remove(card);
        removed |= Hand.Remove(card);
        removed |= PlayZone.Remove(card);
        removed |= TemporaryMaterialsNextTurn.Remove(card);
        removed |= ConsumedPile.Remove(card);
        if (removed)
            GameLog.Data($"Remove material card {DescribeMaterial(card)}");
        return removed;
    }

    public MagicModel GetMagicAtSlot(int slotIndex)
    {
        using (Rules.Enter()) return GetMagicAtSlotCore(slotIndex);
    }

    private MagicModel GetMagicAtSlotCore(int slotIndex)
    {
        for (int i = 0; i < MagicBook.Count; i++)
        {
            if (MagicBook[i] != null && MagicBook[i].SlotIndex == slotIndex)
                return MagicBook[i];
        }

        return null;
    }

    public void ResetExtraRefreshChancesThisTurn()
    {
        using (Rules.Enter()) ResetExtraRefreshChancesThisTurnCore();
    }

    private void ResetExtraRefreshChancesThisTurnCore()
    {
        extraRefreshChancesThisTurn = 0;
        permanentRefreshChancesUsedThisTurn = 0;
        RefreshLimitReductionThisTurn = 0;
        HandLimitThisTurn = int.MaxValue;
    }

    public void SetRefreshLimitReductionThisTurn(int amount)
    {
        using (Rules.Enter()) SetRefreshLimitReductionThisTurnCore(amount);
    }

    private void SetRefreshLimitReductionThisTurnCore(int amount)
    {
        if (amount > RefreshLimitReductionThisTurn)
            RefreshLimitReductionThisTurn = amount;
    }

    public void SetHandLimitThisTurn(int limit)
    {
        using (Rules.Enter()) SetHandLimitThisTurnCore(limit);
    }

    private void SetHandLimitThisTurnCore(int limit)
    {
        if (limit > 0 && limit < HandLimitThisTurn)
            HandLimitThisTurn = limit;
    }

    public void AddPermanentRefreshChance(int amount)
    {
        using (Rules.Enter()) AddPermanentRefreshChanceCore(amount);
    }

    private void AddPermanentRefreshChanceCore(int amount)
    {
        if (amount <= 0)
            return;

        PermanentRefreshChances += amount;
        GameLog.Data($"Player permanent refresh chances +={amount} perTurn={PermanentRefreshChances}");
    }

    public bool UsePermanentRefreshChance()
    {
        using (Rules.Enter()) return UsePermanentRefreshChanceCore();
    }

    private bool UsePermanentRefreshChanceCore()
    {
        if (RemainingPermanentRefreshChancesThisTurn <= 0)
            return false;

        permanentRefreshChancesUsedThisTurn++;
        GameLog.Data($"Player use permanent refresh chance remainingThisTurn={RemainingPermanentRefreshChancesThisTurn}");
        return true;
    }

    public bool HasAdditionalRefreshChance()
    {
        using (Rules.Enter()) return HasAdditionalRefreshChanceCore();
    }

    private bool HasAdditionalRefreshChanceCore()
    {
        return RemainingPermanentRefreshChancesThisTurn > 0 || ExtraRefreshChancesThisTurn > 0;
    }

    public bool UseAdditionalRefreshChance()
    {
        using (Rules.Enter()) return UseAdditionalRefreshChanceCore();
    }

    private bool UseAdditionalRefreshChanceCore()
    {
        return UsePermanentRefreshChance() || UseExtraRefreshChance();
    }

    public void AddExtraRefreshChances(int amount)
    {
        using (Rules.Enter()) AddExtraRefreshChancesCore(amount);
    }

    private void AddExtraRefreshChancesCore(int amount)
    {
        if (amount <= 0)
            return;

        extraRefreshChancesThisTurn += amount;
        GameLog.Data($"Player extra refresh chances +={amount} now={extraRefreshChancesThisTurn}");
    }

    public bool UseExtraRefreshChance()
    {
        using (Rules.Enter()) return UseExtraRefreshChanceCore();
    }

    private bool UseExtraRefreshChanceCore()
    {
        if (extraRefreshChancesThisTurn <= 0)
            return false;

        extraRefreshChancesThisTurn--;
        GameLog.Data($"Player use extra refresh chance now={extraRefreshChancesThisTurn}");
        return true;
    }

    public bool CanConsumeArrowUpgradeMagics(IReadOnlyList<MagicModel> selectedMagics)
    {
        using (Rules.Enter()) return CanConsumeArrowUpgradeMagicsCore(selectedMagics);
    }

    private bool CanConsumeArrowUpgradeMagicsCore(IReadOnlyList<MagicModel> selectedMagics)
    {
        if (selectedMagics == null || selectedMagics.Count == 0)
            return false;

        HashSet<MagicModel> selected = new HashSet<MagicModel>();
        for (int i = 0; i < selectedMagics.Count; i++)
        {
            MagicModel magic = selectedMagics[i];
            if (magic == null || !MagicBook.Contains(magic) || !selected.Add(magic))
                return false;
        }
        return true;
    }

    public void ConsumeArrowUpgradeMagics(IReadOnlyList<MagicModel> selectedMagics)
    {
        using (Rules.Enter()) ConsumeArrowUpgradeMagicsCore(selectedMagics);
    }

    private void ConsumeArrowUpgradeMagicsCore(IReadOnlyList<MagicModel> selectedMagics)
    {
        for (int i = 0; selectedMagics != null && i < selectedMagics.Count; i++)
            ClearMagicSlot(selectedMagics[i].SlotIndex);
    }

    public void RestoreArrowUpgradeState(IEnumerable<string> unlockedNodeIds, int pendingNextTurnShield, int permanentRefreshChanceCount)
    {
        using (Rules.Enter()) RestoreArrowUpgradeStateCore(unlockedNodeIds, pendingNextTurnShield, permanentRefreshChanceCount);
    }

    private void RestoreArrowUpgradeStateCore(IEnumerable<string> unlockedNodeIds, int pendingNextTurnShield, int permanentRefreshChanceCount)
    {
        ArrowUpgrades.Restore(unlockedNodeIds, pendingNextTurnShield);
        PermanentRefreshChances = Mathf.Max(0, permanentRefreshChanceCount);
        permanentRefreshChancesUsedThisTurn = 0;
    }

    public void ClearMagicSlot(int slotIndex)
    {
        using (Rules.Enter()) ClearMagicSlotCore(slotIndex);
    }

    private void ClearMagicSlotCore(int slotIndex)
    {
        for (int i = MagicBook.Count - 1; i >= 0; i--)
        {
            if (MagicBook[i] != null && MagicBook[i].SlotIndex == slotIndex)
                MagicBook.RemoveAt(i);
        }
    }

    public void SetMagicAtSlot(MagicModel magic, int slotIndex)
    {
        using (Rules.Enter()) SetMagicAtSlotCore(magic, slotIndex);
    }

    private void SetMagicAtSlotCore(MagicModel magic, int slotIndex)
    {
        if (magic == null || slotIndex < 0)
            return;

        magic.SlotIndex = slotIndex;
        GameLog.Data($"Set magic slot={slotIndex} magic={magic.Id}");
        for (int i = MagicBook.Count - 1; i >= 0; i--)
        {
            if (MagicBook[i] != null && MagicBook[i].SlotIndex == slotIndex)
                MagicBook.RemoveAt(i);
        }

        int insertIndex = 0;
        while (insertIndex < MagicBook.Count && MagicBook[insertIndex].SlotIndex < slotIndex)
            insertIndex++;

        MagicBook.Insert(insertIndex, magic);
        MagicReadingOrder.ApplyTo(MagicBook);
        Services.OnMagicDiscovered(magic.Data);
    }

    /// <summary>
    /// 按道具栏的视觉序列（含空格）移动一个道具并让其余槽位向目标挤压，
    /// 之后槽位索引即触发顺序，与视图布局保持一致。
    /// </summary>
    public void RearrangeMagicSlotsByVisualOrder(int fromIndex, int toIndex, int visualCount, bool preserveEmptySlots = false)
    {
        using (Rules.Enter()) RearrangeMagicSlotsByVisualOrderCore(fromIndex, toIndex, visualCount, preserveEmptySlots);
    }

    private void RearrangeMagicSlotsByVisualOrderCore(int fromIndex, int toIndex, int visualCount, bool preserveEmptySlots)
    {
        if (visualCount <= 0 || fromIndex == toIndex || fromIndex < 0 || fromIndex >= visualCount || toIndex < 0 || toIndex >= visualCount)
            return;

        MagicModel moved = GetMagicAtSlot(fromIndex);
        if (moved == null)
            return;

        List<MagicModel> content = new List<MagicModel>(visualCount);
        for (int i = 0; i < visualCount; i++)
            content.Add(GetMagicAtSlot(i));

        if (preserveEmptySlots && content[toIndex] == null)
        {
            content[fromIndex] = null;
            content[toIndex] = moved;
        }
        else if (preserveEmptySlots)
        {
            // Insert along the occupied visual reading order. Slot indices can be column-major.
            var slots = new List<int>();
            for (int i = 0; i < content.Count; i++)
                if (content[i] != null) slots.Add(i);
            slots.Sort((a, b) =>
            {
                int rank = MagicReadingOrder.GetRank(a).CompareTo(MagicReadingOrder.GetRank(b));
                return rank != 0 ? rank : a.CompareTo(b);
            });
            int from = slots.IndexOf(fromIndex);
            int to = slots.IndexOf(toIndex);
            int step = from < to ? 1 : -1;
            for (int i = from; i != to; i += step)
                content[slots[i]] = content[slots[i + step]];
            content[toIndex] = moved;
        }
        else
        {
            content.RemoveAt(fromIndex);
            content.Insert(toIndex, moved);
        }

        MagicBook.Clear();
        for (int i = 0; i < content.Count; i++)
        {
            MagicModel model = content[i];
            if (model == null)
                continue;

            model.SlotIndex = i;
            MagicBook.Add(model);
        }
        MagicReadingOrder.ApplyTo(MagicBook);
        GameLog.Data($"Rearrange magic slot from={fromIndex} to={toIndex} count={MagicBook.Count}");
    }

    /// <summary>
    /// 清掉超限槽位并按槽位升序重新压实到 0..count-1，避免出现空洞。
    /// 道具栏从此只显示已占用道具，slotIndex 即从左到右的触发顺序。
    /// </summary>
    public void NormalizeMagicSlots(int slotLimit)
    {
        using (Rules.Enter()) NormalizeMagicSlotsCore(slotLimit);
    }

    private void NormalizeMagicSlotsCore(int slotLimit)
    {
        if (MagicBook.Count == 0)
            return;

        MagicBook.Sort((a, b) =>
        {
            int ai = a != null ? a.SlotIndex : 0;
            int bi = b != null ? b.SlotIndex : 0;
            return ai.CompareTo(bi);
        });

        if (slotLimit > 0 && MagicBook.Count > slotLimit)
            MagicBook.RemoveRange(slotLimit, MagicBook.Count - slotLimit);

        for (int i = 0; i < MagicBook.Count; i++)
        {
            if (MagicBook[i] != null)
                MagicBook[i].SlotIndex = i;
        }
        MagicReadingOrder.ApplyTo(MagicBook);
    }

    public static MagicModel CreateMagicFromData(int magicId, int slotIndex)
    {
        if (!GameDataDatabase.TryGetMagicData(magicId, out MagicData data))
            return null;

        return MagicFactory.Create(data, slotIndex);
    }

    private static void AddInitialMaterials(PlayerState state, PlayerStartMaterialData data)
    {
        if (state == null || data == null || data.material == MaterialEnum.None)
            return;

        for (int i = 0; i < data.count; i++)
        {
            MaterialModel card = new MaterialModel(data.material + "_" + i, data.material);
            AddMaterialModifiers(card, data.modifierIds);
            state.Deck.Add(card);
        }
    }

    public static void AddMaterialModifiers(MaterialModel card, string[] modifierIds)
    {
        if (card == null || modifierIds == null)
            return;

        for (int i = 0; i < modifierIds.Length; i++)
        {
            MaterialModifierModel modifier = CreateMaterialModifierFromData(modifierIds[i]);
            if (modifier != null)
                card.AddModifier(modifier);
        }
    }

    public static MaterialModifierModel CreateMaterialModifierFromData(string modifierId)
    {
        MaterialModifierData data = GetMaterialModifierDataById(modifierId);
        return data != null ? MaterialModifierFactory.Create(data) : null;
    }

    public static MaterialModifierData GetMaterialModifierDataById(string modifierId)
    {
        if (string.IsNullOrEmpty(modifierId))
            return null;

        DataTable<MaterialModifierData> table = GameDataReader.LoadTable<MaterialModifierData>("MaterialModifierData");
        for (int i = 0; table != null && table.items != null && i < table.items.Count; i++)
        {
            MaterialModifierData data = table.items[i];
            if (data != null && data.id == modifierId)
                return data;
        }
        return null;
    }

    private static string DescribeMaterial(MaterialModel card)
    {
        if (card == null)
            return "null";

        return card.instanceId + ":" + card.material + (card.isTemporary ? ":Temporary" : string.Empty);
    }
}
