using UnityEngine;
using System.Collections.Generic;

public class PlayerState
{
    public static string SelectedStartConfigId { get; set; } = "balanced";
    public static bool ContinueSavedRun { get; set; }

    private readonly Dictionary<BuffEnum, BuffModel> buffs = new Dictionary<BuffEnum, BuffModel>();

    public event System.Action<BuffEnum, int> BuffAdded;
    public event System.Action<IReadOnlyList<MaterialModel>> DiscardPileShuffledIntoDrawPile;

    private int temporaryMaterialIndex;

    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }
    public int Gold { get; private set; }
    public int Shield { get; private set; }
    public int DrawCount { get; set; } = 5;
    public int MaxPlayCount { get; set; } = 3;
    public bool KeepHandOnEndTurn { get; set; }
    public readonly List<MaterialModel> TemporaryMaterialsNextTurn = new List<MaterialModel>();
    public List<MaterialModel> Deck { get; } = new List<MaterialModel>();
    public List<MaterialModel> DrawPile { get; } = new List<MaterialModel>();
    public List<MaterialModel> DiscardPile { get; } = new List<MaterialModel>();
    public List<MaterialModel> ConsumedPile { get; } = new List<MaterialModel>();
    public List<MaterialModel> Hand { get; } = new List<MaterialModel>();
    public List<MaterialModel> PlayZone { get; } = new List<MaterialModel>();
    public List<MagicModel> MagicBook { get; } = new List<MagicModel>();
    public IReadOnlyDictionary<BuffEnum, BuffModel> Buffs => buffs;
    public EnemyModel LastDamageSourceEnemy { get; private set; }
    public bool IsEndingTurn { get; private set; }

    public PlayerState(int maxHealth = 50, int gold = 0)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        Gold = gold;
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
        if (config == null)
        {
            state.DrawPile.AddRange(state.Deck);
            return state;
        }

        state.DrawCount = config.drawCount;
        state.MaxPlayCount = config.maxPlayCount;

        for (int i = 0; i < config.initialMaterials.Length; i++)
        {
            PlayerStartMaterialData material = config.initialMaterials[i];
            if (material != null)
                AddInitialMaterials(state, material.material, material.count);
        }

        for (int i = 0; i < config.initialMagics.Length; i++)
        {
            PlayerStartMagicData magic = config.initialMagics[i];
            if (magic != null)
                state.SetMagicAtSlot(CreateMagicFromData(magic.magicId, magic.slotIndex), magic.slotIndex);
        }

        state.DrawPile.AddRange(state.Deck);
        return state;
    }

    public int DrawCards(int count)
    {
        return DrawCardsToHand(count, true);
    }

    public int DrawCardsForRefresh(int count)
    {
        return DrawCardsToHand(count, false);
    }

    public int DrawBasicMaterialCards(int count)
    {
        return DrawCardsToHand(count, true, IsBasicMaterialCard);
    }

    public int DrawSpecificMaterialsToHand(IReadOnlyList<MaterialEnum> materials, bool createTemporaryIfMissing)
    {
        if (materials == null)
            return 0;

        int drawnCount = 0;
        for (int i = 0; i < materials.Count; i++)
        {
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
            TriggerAfterDraw(card);
            drawnCount++;
            GameLog.Data($"Draw fixed material {DescribeMaterial(card)} to hand. hand={Hand.Count} drawPile={DrawPile.Count} discardPile={DiscardPile.Count}");
        }
        return drawnCount;
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

    public RefreshHandResult RefreshBasicMaterialHandCards(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
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
            if (!EnsureDrawPileHasCards(canDraw))
                break;

            int randomIndex = GetRandomDrawableDrawPileIndex(canDraw);
            if (randomIndex < 0)
                break;

            MaterialModel card = DrawPile[randomIndex];
            DrawPile.RemoveAt(randomIndex);
            Hand.Add(card);
            card.TriggerOnDraw();
            if (triggerAfterDraw)
                TriggerAfterDraw(card);
            drawnCount++;
            GameLog.Data($"Draw card {DescribeMaterial(card)} to hand. hand={Hand.Count} drawPile={DrawPile.Count} discardPile={DiscardPile.Count}");
        }

        return drawnCount;
    }

    public bool ApplyArrowReadAfterAction(MaterialModel card, ArrowReadAfterReadAction action)
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
        if (!RemoveCardFromCombatPiles(card))
            return false;

        card.isPlayed = false;
        AddHalfArrowToDiscard(card, 0);
        AddHalfArrowToDiscard(card, 1);
        GameLog.Data($"Split fragile arrow {DescribeMaterial(card)} to half arrows. discardPile={DiscardPile.Count}");
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
        DiscardPile.Add(half);
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
        int drawnCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (!EnsureDrawPileHasCards())
                break;

            int randomIndex = NextRunRandomInt(0, DrawPile.Count);
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

    private int NextRunRandomInt(int minInclusive, int maxExclusive)
    {
        return this is PlayerStatus status ? status.NextRunRandomInt(minInclusive, maxExclusive) : Random.Range(minInclusive, maxExclusive);
    }

    private bool EnsureDrawPileHasCards()
    {
        return EnsureDrawPileHasCards(null);
    }

    private bool EnsureDrawPileHasCards(System.Predicate<MaterialModel> canDraw)
    {
        if (HasDrawableDrawPileCard(canDraw))
            return true;

        if (DiscardPile.Count == 0)
            return false;

        List<MaterialModel> shuffledCards = new List<MaterialModel>(DiscardPile);
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
        if (card == null || ConsumedPile.Contains(card))
            return;

        card.isPlayed = false;
        ConsumedPile.Add(card);
    }

    public bool TryMoveHandCardToPlay(MaterialModel card)
    {
        if (card == null)
            return false;

        int index = Hand.IndexOf(card);
        if (index < 0)
            return false;

        if (IsMaterialDisabled(card))
            return false;

        Hand.RemoveAt(index);
        PlayZone.Add(card);
        card.isPlayed = true;
        card.TriggerOnJoin();
        if (GetBuffStack(BuffEnum.MaterialOverplayDebuff) > 0 || HasMaterialOverplayDebuffSource())
        {
            if (PlayZone.Count > 4)
                AddRandomEnemyDebuff(1);
        }
        GameLog.Data($"Move card {DescribeMaterial(card)} hand->playZone. hand={Hand.Count} playZone={PlayZone.Count}");
        return true;
    }

    public bool IsMaterialDisabled(MaterialModel card)
    {
        if (card == null)
            return false;

        int disabledMaterial = GetBuffStack(BuffEnum.AttributeDisabled);
        return disabledMaterial > 0 && (int)card.material == disabledMaterial;
    }

    private void AddRandomEnemyDebuff(int stack)
    {
        int index = NextRunRandomInt(0, 3);
        AddBuff(index == 0 ? BuffEnum.Weak : index == 1 ? BuffEnum.Slow : BuffEnum.Vulnerable, stack);
    }

    private bool HasMaterialOverplayDebuffSource()
    {
        IReadOnlyList<EnemyModel> enemies = BattleManager.Instance?.Enemies;
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
        if (card == null)
            return false;

        int index = PlayZone.IndexOf(card);
        if (index < 0)
            return false;

        PlayZone.RemoveAt(index);
        card.TriggerOnDiscard();
        TriggerAfterDiscard(card);
        Hand.Add(card);
        GameLog.Data($"Move card {DescribeMaterial(card)} playZone->hand. hand={Hand.Count} playZone={PlayZone.Count}");
        return true;
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

    public int ReturnHandCardsToDrawPile(IReadOnlyList<MaterialModel> cards)
    {
        return ReturnHandCardsToDiscardPile(cards, null);
    }

    public int ReturnHandCardsToDrawPile(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        return ReturnHandCardsToDiscardPile(cards, removedTemporaryCards);
    }

    public int ReturnHandCardsToDiscardPile(IReadOnlyList<MaterialModel> cards)
    {
        return ReturnHandCardsToDiscardPile(cards, null);
    }

    public int ReturnHandCardsToDiscardPile(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
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

            DiscardPile.Add(card);
            discardedCount++;
            GameLog.Data($"Refresh discards card {DescribeMaterial(card)}. hand={Hand.Count} discardPile={DiscardPile.Count}");
        }

        return discardedCount;
    }

    public RefreshHandResult RefreshHandCards(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards)
    {
        return RefreshHandCards(cards, removedTemporaryCards, null);
    }

    public RefreshHandResult RefreshHandCards(IReadOnlyList<MaterialModel> cards, List<MaterialModel> removedTemporaryCards, BattleManager battleManager)
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
        int drawnCount = DrawCardsToHand(refreshCount, false, canDraw);
        GameLog.Data($"Refresh hand cards selected={cards.Count} drawn={drawnCount} discarded={discardedCount} temporaryRemoved={removedTemporaryCards?.Count ?? 0}");
        return new RefreshHandResult(drawnCount, discardedCount);
    }

    public void ReturnPlayZoneCardsToDrawPile()
    {
        ReturnPlayZoneCardsToDrawPile(null);
    }

    public void ReturnPlayZoneCardsToDrawPile(List<MaterialModel> removedTemporaryCards)
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
        return true;
    }

    public void ReturnPlayZoneCardsToDiscardPile(List<MaterialModel> removedTemporaryCards)
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
                DiscardPile.Add(card);
                GameLog.Data($"Return play zone card {DescribeMaterial(card)} to discard pile. discardPile={DiscardPile.Count}");
            }
        }

        PlayZone.Clear();
    }

    public void EndTurn()
    {
        EndTurn(null);
    }

    public void EndTurn(List<MaterialModel> removedTemporaryCards)
    {
        IsEndingTurn = true;
        if (Hand.Count > 0)
        {
            if (KeepHandOnEndTurn)
            {
                GameLog.Data("End turn keeps hand cards.");
            }
            else
            {
                for (int i = Hand.Count - 1; i >= 0; i--)
                {
                    MaterialModel card = Hand[i];
                    if (card != null && card.isRetained)
                    {
                        GameLog.Data($"End turn retains hand card {DescribeMaterial(card)}.");
                        continue;
                    }

                    Hand.RemoveAt(i);
                    card.TriggerOnDiscard();
                    TriggerAfterDiscard(card);
                    if (card.isTemporary)
                    {
                        removedTemporaryCards?.Add(card);
                        AddConsumedCard(card);
                        GameLog.Data($"End turn removes temporary hand card {DescribeMaterial(card)}.");
                    }
                    else
                    {
                        DiscardPile.Add(card);
                        GameLog.Data($"End turn discards hand card {DescribeMaterial(card)}. discardPile={DiscardPile.Count}");
                    }
                }
            }
        }
        IsEndingTurn = false;

        ReturnPlayZoneCardsToDiscardPile(removedTemporaryCards);
        KeepHandOnEndTurn = false;
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public int TakeDamage(int damage, CombatantModel attacker)
    {
        return TakeDamageResult(damage, attacker).HealthDamage;
    }

    public CombatDamageResult TakeDamageResult(int damage, CombatantModel attacker)
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
        result.TargetDied = healthBefore > 0 && CurrentHealth <= 0;
        GameLog.Data($"Player take damage raw={damage} final={result.FinalDamage} finalHealthDamage={healthDamage} shieldNow={Shield} hp={CurrentHealth}/{MaxHealth}");
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayDamageResultSfx(healthDamage, blockedDamage);

        TriggerAfterTakeDamage(attacker, result);
        if (result.TargetDied)
            TriggerOnDie(attacker);

        return result;
    }

    public int TakeDirectDamage(int damage)
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
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayDamageResultSfx(healthDamage, 0);

        if (healthBefore > 0 && CurrentHealth <= 0)
            TriggerOnDie(null);

        return healthDamage;
    }

    public void Heal(int amount)
    {
        if (amount <= 0)
            return;

        CurrentHealth += amount;
        if (CurrentHealth > MaxHealth)
            CurrentHealth = MaxHealth;
        GameLog.Data($"Player heal amount={amount} hp={CurrentHealth}/{MaxHealth}");
    }

    public void IncreaseMaxHealth(int amount)
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
        if (amount <= 0)
            return;

        MaxHealth += amount;
        if (CurrentHealth > MaxHealth)
            CurrentHealth = MaxHealth;
        GameLog.Data($"Player max health only change={amount} hp={CurrentHealth}/{MaxHealth}");
    }

    public int GainShield(int amount)
    {
        if (amount <= 0)
            return 0;

        int shieldValue = amount;
        TriggerOnGainShield(ref shieldValue);
        if (shieldValue <= 0)
            return 0;

        Shield += shieldValue;
        GameLog.Data($"Player gain shield={shieldValue} shield={Shield}");
        return shieldValue;
    }

    public int ConsumeShield(int amount)
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
        Shield = 0;
        GameLog.Data("Player clear shield");
    }

    public void ClearBuffs()
    {
        buffs.Clear();
        GameLog.Data("Player clear buffs");
    }

    public void ClearCombatState()
    {
        ClearShield();
        ClearBuffs();
        RemoveSturdyModifiers();
        RemoveBattleOnlyArrowState();
        TemporaryMaterialsNextTurn.Clear();
        ConsumedPile.Clear();
    }

    public void RemoveSturdyModifiers()
    {
        RemoveSturdyModifiers(Hand);
        RemoveSturdyModifiers(PlayZone);
        RemoveSturdyModifiers(DrawPile);
        RemoveSturdyModifiers(DiscardPile);
        RemoveSturdyModifiers(ConsumedPile);
        RemoveSturdyModifiers(Deck);
        RemoveSturdyModifiers(TemporaryMaterialsNextTurn);
    }

    private static void RemoveSturdyModifiers(List<MaterialModel> cards)
    {
        for (int i = 0; i < cards.Count; i++)
            cards[i]?.RemoveModifiers<SturdyModifier>();
    }

    public void RemoveBattleOnlyArrowState()
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
                card.AddModifier(new SturdyModifier());
        }
    }

    public void TriggerMaterialBegin()
    {
        for (int i = 0; i < PlayZone.Count; i++)
            PlayZone[i].TriggerOnBegin();
    }

    public void TriggerMaterialEnd()
    {
        for (int i = 0; i < PlayZone.Count; i++)
            PlayZone[i].TriggerOnEnd();
    }

    public void AddGold(int amount)
    {
        Gold += amount;
        if (Gold < 0)
            Gold = 0;
        GameLog.Data($"Player gold change={amount} gold={Gold}");
        if (amount > 0 && AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(GameSfxId.GetCoin);
    }

    public void AddBuff(BuffEnum buffType, int stack)
    {
        if (buffType == BuffEnum.None || stack <= 0)
            return;

        if (buffType == BuffEnum.AttributeDisabled)
        {
            if (buffs.TryGetValue(buffType, out BuffModel existingAttributeBuff))
                existingAttributeBuff.stack = stack;
            else
                buffs.Add(buffType, BuffModel.Create(buffType, stack));

            GameLog.Data($"Player add buff {buffType} material={stack} now={GetBuffStack(buffType)}");
            BuffAdded?.Invoke(buffType, stack);
            return;
        }

        if (buffs.TryGetValue(buffType, out BuffModel buff))
            buff.AddStack(stack);
        else
            buffs.Add(buffType, BuffModel.Create(buffType, stack));
        GameLog.Data($"Player add buff {buffType} stack+={stack} now={GetBuffStack(buffType)}");
        BuffAdded?.Invoke(buffType, stack);
    }

    public int GetBuffStack(BuffEnum buffType)
    {
        if (buffs.TryGetValue(buffType, out BuffModel buff))
            return buff.stack;

        return 0;
    }

    public void ConsumeBuff(BuffEnum buffType, int amount)
    {
        if (buffs.TryGetValue(buffType, out BuffModel buff))
        {
            buff.ConsumeStack(amount);
            if (buff.stack <= 0)
            {
                buff.OnExpire(new CombatantModel(this), null);
                buffs.Remove(buffType);
                GameLog.Data($"Player buff expired {buffType}");
            }
        }
    }

    public void TriggerOnTurnStart(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnTurnStart(self, target));
    }

    public void TriggerAfterTurnStart(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.AfterTurnStart(self, target));
    }

    public void TriggerAfterDraw(MaterialModel card)
    {
        TriggerBuffs(null, (buff, self, target) => buff.AfterDraw(self, card));
    }

    public void TriggerAfterDiscard(MaterialModel card)
    {
        TriggerBuffs(null, (buff, self, target) => buff.AfterDiscard(self, card));
    }

    public void TriggerOnInvoke(CombatantModel target)
    {
        TriggerBuffs(target, (buff, self, opponent) => buff.OnInvoke(self, opponent));
    }

    public void TriggerOnGetAction(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnGetAction(self, target));
    }

    public void TriggerAfterGetAction(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.AfterGetAction(self, target));
    }

    public void TriggerOnAttack(CombatantModel target, ref int attackValue)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].OnAttack(self, target, ref attackValue);
    }

    public void TriggerAfterAttack(CombatantModel attacker, ref int attackResult)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].AfterAttack(self, attacker, ref attackResult);
    }

    public void TriggerOnTakeDamage(CombatantModel attacker, ref int damage)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].OnTakeDamage(self, attacker, ref damage);
    }

    public void TriggerAfterTakeDamage(CombatantModel attacker, CombatDamageResult result)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].AfterTakeDamage(self, attacker, result);
    }

    public void TriggerOnGainShield(ref int shieldValue)
    {
        if (buffs.Count == 0)
            return;

        CombatantModel self = new CombatantModel(this);
        List<BuffModel> snapshot = new List<BuffModel>(buffs.Values);
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].OnGainShield(self, ref shieldValue);
    }

    public void TriggerOnTurnEnd(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnTurnEnd(self, target));
    }

    public void TriggerAfterTurnEnd(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.AfterTurnEnd(self, target));
    }

    public void TriggerOnDie(CombatantModel opponent)
    {
        TriggerBuffs(opponent, (buff, self, target) => buff.OnDie(self, target));
    }

    private void TriggerBuffs(CombatantModel opponent, BuffTrigger trigger)
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

            trigger(buff, self, opponent);
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

    private delegate void BuffTrigger(BuffModel buff, CombatantModel self, CombatantModel opponent);

    public bool ReturnLeftmostHandCardToDrawPile()
    {
        return ReturnLeftmostHandCardToDiscardPile(null);
    }

    public bool ReturnLeftmostHandCardToDrawPile(List<MaterialModel> removedTemporaryCards)
    {
        return ReturnLeftmostHandCardToDiscardPile(removedTemporaryCards);
    }

    public bool ReturnLeftmostHandCardToDiscardPile()
    {
        return ReturnLeftmostHandCardToDiscardPile(null);
    }

    public bool ReturnLeftmostHandCardToDiscardPile(List<MaterialModel> removedTemporaryCards)
    {
        if (Hand.Count == 0)
            return false;

        MaterialModel card = Hand[0];
        Hand.RemoveAt(0);
        card.TriggerOnDiscard();
        if (card.isTemporary)
        {
            removedTemporaryCards?.Add(card);
            AddConsumedCard(card);
            GameLog.Data($"Remove leftmost temporary hand card {DescribeMaterial(card)}");
        }
        else
        {
            DiscardPile.Add(card);
            GameLog.Data($"Discard leftmost hand card {DescribeMaterial(card)}. discardPile={DiscardPile.Count}");
        }
        return true;
    }

    public List<MaterialModel> ConsumeTemporaryMaterialsNextTurn()
    {
        List<MaterialModel> addedCards = new List<MaterialModel>();
        for (int i = 0; i < TemporaryMaterialsNextTurn.Count; i++)
        {
            MaterialModel source = TemporaryMaterialsNextTurn[i];
            if (source == null)
                continue;

            Hand.Add(source);
            source.TriggerOnDraw();
            TriggerAfterDraw(source);
            addedCards.Add(source);
            GameLog.Data($"Add scheduled material to hand {DescribeMaterial(source)}");
        }

        TemporaryMaterialsNextTurn.Clear();
        return addedCards;
    }

    public MaterialModel AddMaterialNextTurn(MaterialEnum material, MaterialModifierModel modifier)
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
        AddTemporaryMaterialNextTurn(material, true);
    }

    public MaterialModel AddTemporaryMaterialToHand(MaterialEnum material)
    {
        if (material == MaterialEnum.None)
            return null;

        MaterialModel card = new MaterialModel("temporary_" + material + "_" + temporaryMaterialIndex++, material);
        card.AddModifier(new TemporaryModifier());
        Hand.Add(card);
        card.TriggerOnDraw();
        TriggerAfterDraw(card);
        GameLog.Data($"Add temporary material to hand {DescribeMaterial(card)}");
        return card;
    }

    public MaterialModel AddDeckMaterial(MaterialEnum material)
    {
        return AddDeckMaterial(material, null);
    }

    public MaterialModel AddDeckMaterial(MaterialEnum material, MaterialModifierModel modifier)
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

    public bool RemoveCardEverywhere(MaterialModel card)
    {
        if (card == null)
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
        for (int i = 0; i < MagicBook.Count; i++)
        {
            if (MagicBook[i] != null && MagicBook[i].SlotIndex == slotIndex)
                return MagicBook[i];
        }

        return null;
    }

    public bool UseExtraRefreshChance()
    {
        if (GetBuffStack(BuffEnum.ExtraRefresh) <= 0)
            return false;

        ConsumeBuff(BuffEnum.ExtraRefresh, 1);
        return true;
    }

    public void ClearMagicSlot(int slotIndex)
    {
        for (int i = MagicBook.Count - 1; i >= 0; i--)
        {
            if (MagicBook[i] != null && MagicBook[i].SlotIndex == slotIndex)
                MagicBook.RemoveAt(i);
        }
    }

    public void SetMagicAtSlot(MagicModel magic, int slotIndex)
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
    }

    public static MagicModel CreateMagicFromData(int magicId, int slotIndex)
    {
        if (!GameDataDatabase.TryGetMagicData(magicId, out MagicData data))
            return null;

        return MagicFactory.Create(data, slotIndex);
    }

    private static void AddInitialMaterials(PlayerState state, MaterialEnum material, int count)
    {
        for (int i = 0; i < count; i++)
            state.Deck.Add(new MaterialModel(material + "_" + i, material));
    }

    private static string DescribeMaterial(MaterialModel card)
    {
        if (card == null)
            return "null";

        return card.instanceId + ":" + card.material + (card.isTemporary ? ":Temporary" : string.Empty);
    }
}
