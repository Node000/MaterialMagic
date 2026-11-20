using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Production choice boundaries must use their owner, not the caller's ambient run.
/// All players use Simulation; content and random inputs are fixture-owned. The only
/// production tables deliberately retained are the real difficulty IDs and rarity weights.
/// </summary>
public class ChoiceRuleIsolationTests
{
    private MaterialModifierContext savedMaterial;
    private MagicModifierContext savedMagic;
    private RuleExecutionContext savedRules;
    private EconomyConfigData economy;
    private MagicData[] magic;

    [SetUp]
    public void SetUp()
    {
        savedMaterial = MaterialModifierModel.CurrentContext;
        savedMagic = MagicModifierModel.CurrentContext;
        savedRules = RuleExecutionScope.Current;
        economy = new EconomyConfigData
        {
            shopMaterialPrice = 10, shopRemoveMaterialPrice = 10,
            shopRefreshPrice = 4, shopSpellPrice = 22,
            battleGoldMin = 20, battleArrowRewardEnchantChance = 0f,
            ownedMagicWeightMultiplier = 1f
        };
        magic = new[] { Magic(89101), Magic(89102) };
    }

    [TearDown]
    public void TearDown()
    {
        MaterialModifierModel.CurrentContext = savedMaterial;
        MagicModifierModel.CurrentContext = savedMagic;
        Assert.That(RuleExecutionScope.Current, Is.SameAs(savedRules), "Every entered rule scope must be disposed, including on failure.");
    }

    private static MagicData Magic(int id) => new MagicData
    {
        numericId = id, id = "isolation_magic_" + id,
        rarity = MagicRarity.Common, recipe = new[] { MaterialEnum.Fire }
    };

    private static PlayerStatus Player(int seed, int step, int ascension, params string[] upgrades)
    {
        var player = new PlayerStatus(50, 200, PlayerStateServices.Simulation,
            new RunDifficultySaveData { ascensionLevel = ascension, upgradeIds = upgrades });
        player.SetRunRandomState(seed, step);
        // Unknown IDs are silently ignored by the factory. Do not let that turn an
        // isolation assertion into a test of two identical, unmodified players.
        Assert.That(player.Rules.Upgrades.Select(upgrade => upgrade.Id), Is.EqualTo(upgrades));
        Assert.That(player.Services, Is.SameAs(PlayerStateServices.Simulation));
        return player;
    }

    private static PlayerStatus Reduced(int seed = 1709, int step = 7)
        => Player(seed, step, 11, "gold_gain_80", "shop_price_120");

    private static PlayerStatus Enhanced(int seed = 2903, int step = 13)
        => Player(seed, step, -11, "gold_gain_125", "shop_price_50");

    private static void AssertOwner(PlayerState owner)
    {
        Assert.That(RuleExecutionScope.Current, Is.SameAs(owner.Rules));
        Assert.That(MaterialModifierModel.CurrentContext, Is.Not.Null);
        Assert.That(MaterialModifierModel.CurrentContext.PlayerState, Is.SameAs(owner));
        Assert.That(MaterialModifierModel.CurrentContext.BattleManager, Is.SameAs(owner.Battle));
    }

    // Execute the real owner entry point while the *other* player is the caller.
    // Verify reference restoration, not just equal field values.
    private static void UnderOtherPlayer(PlayerStatus other, Action operation)
    {
        RuleExecutionContext beforeRules = RuleExecutionScope.Current;
        MaterialModifierContext beforeMaterial = MaterialModifierModel.CurrentContext;
        MagicModifierContext beforeMagic = MagicModifierModel.CurrentContext;
        using (other.Rules.Enter())
        {
            MaterialModifierContext callerMaterial = MaterialModifierModel.CurrentContext;
            var callerMagic = new MagicModifierContext { PlayerState = other };
            MagicModifierModel.CurrentContext = callerMagic;
            try { operation(); }
            finally
            {
                Assert.That(RuleExecutionScope.Current, Is.SameAs(other.Rules));
                Assert.That(MaterialModifierModel.CurrentContext, Is.SameAs(callerMaterial));
                Assert.That(MagicModifierModel.CurrentContext, Is.SameAs(callerMagic));
            }
        }
        Assert.That(RuleExecutionScope.Current, Is.SameAs(beforeRules));
        Assert.That(MaterialModifierModel.CurrentContext, Is.SameAs(beforeMaterial));
        Assert.That(MagicModifierModel.CurrentContext, Is.SameAs(beforeMagic));
    }

    private static ShopLayer[] MixedLayers() => new[]
    {
        new ShopLayer(new Dictionary<ShopSlotEnum, float> { { ShopSlotEnum.Item, 1f } }, 2),
        new ShopLayer(new Dictionary<ShopSlotEnum, float> { { ShopSlotEnum.Arrow, 1f } }, 2)
    };

    private ShopGenerationServices ShopServices(PlayerStatus owner, Func<int, int, int> random = null)
    {
        var pool = new ShopProductPoolData
        {
            weakMaterialChance = 0f,
            normalMaterialOffers = new[]
            {
                new ShopMaterialOfferData { material = MaterialEnum.Fire, price = 10 },
                new ShopMaterialOfferData { material = MaterialEnum.Wind, price = 10 },
                new ShopMaterialOfferData { material = MaterialEnum.Water, price = 10 }
            }
        };
        return new ShopGenerationServices
        {
            NextRandomInt = random ?? ((min, max) => ChoiceRuleRandom.Next(owner, min, max)),
            IsMagicUnlocked = data => true,
            IsModifierUnlocked = data => true,
            FindMagic = id => magic.FirstOrDefault(data => data.numericId == id),
            FindRewardPool = id => null,
            FindProductPool = id => null,
            AllMagic = () => magic,
            AllProductPools = () => new[] { pool },
            FindModifier = id => null,
            ModifierPriceDelta = id => 0,
            ModifyPrice = price => { AssertOwner(owner); return DifficultyUpgradeSystem.ModifyShopPrice(price); },
            QuoteMagic = data =>
            {
                AssertOwner(owner);
                return ShopOfferGenerator.GetMagicBuyPrice(data, economy, DifficultyUpgradeSystem.ModifyShopPrice);
            }
        };
    }

    private RewardGenerationServices RewardServices(PlayerStatus owner, Func<int, int, int> random = null)
        => new RewardGenerationServices
        {
            // Null intentionally exercises RewardOfferGenerator's owner-bound default.
            NextRandomInt = random,
            FindMagic = id => magic.FirstOrDefault(data => data.numericId == id),
            FindRewardPool = id => null,
            AllMagic = () => magic,
            IsMagicUnlocked = data => true,
            EliteLegendaryGuarantee = () => { AssertOwner(owner); return false; },
            MaterialModifierChoices = count => Array.Empty<MaterialModifierData>(),
            ModifyGold = amount => { AssertOwner(owner); return DifficultyUpgradeSystem.ModifyGoldGain(amount); },
            ModifyGoldChoiceCount = count => { AssertOwner(owner); return DifficultyUpgradeSystem.ModifyBattleRewardGoldChoiceCount(count); },
            ReportInsufficientPool = message => Assert.Fail(message)
        };

    private EventRuleServices EventServices(PlayerStatus owner, Func<int, int, int> random = null)
    {
        var services = new EventRuleServices(owner)
        {
            MagicSlotCapacity = 3,
            FindMagic = id => magic.FirstOrDefault(data => data.numericId == id),
            FindMagicModifier = id => null,
            FindMaterialModifier = id => null,
            MagicChoices = count => magic.Take(count).ToArray(),
            MagicModifierChoices = count => Array.Empty<MagicModifierData>(),
            MaterialModifierChoices = count => Array.Empty<MaterialModifierData>()
        };
        // Leave the constructor's owner-bound default intact when no override is requested.
        if (random != null) services.NextRandomInt = random;
        return services;
    }

    private static EventResolutionSession EventSession(PlayerStatus owner, EventRuleServices services, params EventEffectData[] effects)
    {
        var definition = new EventData
        {
            numericId = 89201, id = "isolation_event", startNodeId = "start",
            nodes = new[]
            {
                new EventNodeData
                {
                    id = "start", textKeys = Array.Empty<string>(),
                    options = new[] { new EventOptionData { id = "accept", recipe = "1", nextNodeId = "done", effects = effects } }
                },
                new EventNodeData { id = "done", textKeys = Array.Empty<string>() }
            }
        };
        var model = new EventModel(definition, services.NextRandomInt);
        return new EventResolutionSession(owner, model, model.CurrentOptions[0], services);
    }

    private static BonusLevelData Grid(params int[] amounts) => new BonusLevelData
    {
        radius = 1, drawCount = 1,
        rewards = amounts.Select(amount => new BonusRewardData { rewardType = BonusRewardType.Gold, amount = amount }).ToArray()
    };

    private static ChoiceResult ClaimGold(RewardSession session)
    {
        PendingChoice pending = session.PendingChoice;
        return session.SubmitChoice(new ChoiceSubmission(pending.Id, pending.Candidates.Single(candidate => candidate.Amount > 0).Id));
    }

    private sealed class GoldProbe : RelicModel
    {
        private readonly PlayerStatus owner;
        private readonly int bonus;
        private readonly Action onQuery;
        public readonly List<int> Inputs = new List<int>();
        public GoldProbe(PlayerStatus owner, int bonus, Action onQuery = null)
            : base(new RelicData { numericId = 89301 })
        { this.owner = owner; this.bonus = bonus; this.onQuery = onQuery; }
        public override int ModifyGoldGain(int amount)
        {
            AssertOwner(owner);
            Inputs.Add(amount);
            onQuery?.Invoke();
            return amount + bonus;
        }
    }

    private static GoldProbe AttachGoldProbe(PlayerStatus owner, int bonus, Action onQuery = null)
    {
        var probe = new GoldProbe(owner, bonus, onQuery);
        Assert.That(owner.Relics.TryAcquire(probe), Is.EqualTo(RelicAcquireResult.Acquired));
        return probe;
    }

    [Test]
    public void InjectedDifficultyIsSnapshottedAndDistinctPlayersKeepRealUpgradeIds()
    {
        string[] ids = { "gold_gain_80", "shop_price_120" };
        var source = new RunDifficultySaveData { ascensionLevel = 11, upgradeIds = ids };
        var first = new PlayerStatus(50, 200, PlayerStateServices.Simulation, source);
        PlayerStatus second = Enhanced();
        ids[0] = "gold_gain_125";
        source.ascensionLevel = 99;
        source.upgradeIds = Array.Empty<string>();

        Assert.That(first.Rules.Player, Is.SameAs(first));
        Assert.That(first.Rules.Difficulty, Is.Not.SameAs(source));
        Assert.That(first.Rules.Difficulty.upgradeIds, Is.EqualTo(new[] { "gold_gain_80", "shop_price_120" }));
        Assert.That(first.Rules.Upgrades.Select(upgrade => upgrade.Id), Is.EqualTo(first.Rules.Difficulty.upgradeIds));
        using (first.Rules.Enter())
        {
            Assert.That(DifficultyUpgradeSystem.CurrentAscensionLevel, Is.EqualTo(11));
            Assert.That(DifficultyUpgradeSystem.ModifyGoldGain(20), Is.EqualTo(16));
            Assert.That(DifficultyUpgradeSystem.ModifyShopPrice(10), Is.EqualTo(12));
            using (second.Rules.Enter())
            {
                Assert.That(DifficultyUpgradeSystem.CurrentAscensionLevel, Is.EqualTo(-11));
                Assert.That(DifficultyUpgradeSystem.ModifyGoldGain(20), Is.EqualTo(25));
                Assert.That(DifficultyUpgradeSystem.ModifyShopPrice(10), Is.EqualTo(5));
            }
            Assert.That(DifficultyUpgradeSystem.ModifyGoldGain(20), Is.EqualTo(16));
        }
    }

    [Test]
    public void InterleavedShopQuotesPurchasesAndRefundsUseTheOwnerDifficulty()
    {
        PlayerStatus first = Reduced();
        PlayerStatus second = Enhanced();
        var firstShop = new ShopSession(first, economy, 3, ShopServices(first));
        var secondShop = new ShopSession(second, economy, 3, ShopServices(second));
        UnderOtherPlayer(second, () => firstShop.Open(MixedLayers()));
        UnderOtherPlayer(first, () => secondShop.Open(MixedLayers()));
        Assert.That(firstShop.Offers.Select(offer => offer.price), Is.EqualTo(new[] { 24, 12 }));
        Assert.That(secondShop.Offers.Select(offer => offer.price), Is.EqualTo(new[] { 10, 5 }));
        UnderOtherPlayer(second, () =>
        {
            Assert.That(firstShop.RemoveArrowPrice, Is.EqualTo(12));
            Assert.That(firstShop.TryPurchase(firstShop.Offers[1], out ShopTransaction receipt, out ShopTransactionFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(ShopTransactionFailure.None));
            Assert.That(receipt.Price, Is.EqualTo(12));
        });
        Assert.That(first.Gold, Is.EqualTo(188));
        Assert.That(second.Gold, Is.EqualTo(200));
        UnderOtherPlayer(first, () => Assert.That(secondShop.TryPurchase(secondShop.Offers[1], out _, out _), Is.True));
        Assert.That(second.Gold, Is.EqualTo(195));
        Assert.That(first.Deck.Count, Is.EqualTo(1));
        Assert.That(second.Deck.Count, Is.EqualTo(1));
        UnderOtherPlayer(second, () => Assert.That(firstShop.TryUndo(out _), Is.True));
        Assert.That(first.Gold, Is.EqualTo(200), "A refund is not a new difficulty-adjusted reward.");
        Assert.That(second.Gold, Is.EqualTo(195));
        UnderOtherPlayer(first, () => Assert.That(secondShop.TryUndo(out _), Is.True));
        Assert.That(second.Gold, Is.EqualTo(200));
        Assert.That(first.Deck, Is.Empty);
        Assert.That(second.Deck, Is.Empty);
        Assert.That(firstShop.Offers[1].purchased, Is.False);
        Assert.That(secondShop.Offers[1].purchased, Is.False);
    }

    [Test]
    public void PaidShopRemovalRefreshAndUndoDoNotShareCountersOrGold()
    {
        PlayerStatus first = Reduced();
        PlayerStatus second = Enhanced();
        MaterialModel firstCard = first.AddDeckMaterial(MaterialEnum.Fire);
        MaterialModel secondCard = second.AddDeckMaterial(MaterialEnum.Water);
        var firstShop = new ShopSession(first, economy, 3, ShopServices(first));
        var secondShop = new ShopSession(second, economy, 3, ShopServices(second));
        UnderOtherPlayer(second, () => firstShop.Open(MixedLayers()));
        UnderOtherPlayer(first, () => secondShop.Open(MixedLayers()));
        UnderOtherPlayer(second, () => Assert.That(firstShop.TryRemoveMaterial(firstCard, out _, out _), Is.True));
        UnderOtherPlayer(first, () => Assert.That(secondShop.TryRemoveMaterial(secondCard, out _, out _), Is.True));
        Assert.That(first.Gold, Is.EqualTo(188));
        Assert.That(second.Gold, Is.EqualTo(195));
        UnderOtherPlayer(second, () =>
        {
            Assert.That(firstShop.TryRefresh(out int price), Is.True);
            Assert.That(price, Is.EqualTo(4));
            Assert.That(firstShop.RemoveArrowPrice, Is.EqualTo(13), "The paid-removal surcharge is added after the owner's difficulty multiplier.");
        });
        UnderOtherPlayer(first, () => Assert.That(secondShop.TryUndo(out _), Is.True));
        Assert.That(first.PaidShopArrowRemovalCount, Is.EqualTo(1));
        Assert.That(second.PaidShopArrowRemovalCount, Is.Zero);
        Assert.That(first.Gold, Is.EqualTo(184));
        Assert.That(second.Gold, Is.EqualTo(200));
        Assert.That(firstShop.RefreshCount, Is.EqualTo(1));
        Assert.That(secondShop.RefreshCount, Is.Zero);
        Assert.That(firstShop.RemoveArrowUsed, Is.False);
        Assert.That(second.Deck.Single(), Is.SameAs(secondCard));
        UnderOtherPlayer(first, () => Assert.That(secondShop.RemoveArrowPrice, Is.EqualTo(5)));
    }

    [Test]
    public void GeneratedBattleGoldAndClaimUseOwnerRulesWithoutDoubleDifficulty()
    {
        PlayerStatus first = Reduced();
        PlayerStatus second = Enhanced();
        GoldProbe firstProbe = AttachGoldProbe(first, 2);
        GoldProbe secondProbe = AttachGoldProbe(second, 4);
        var firstGenerator = new RewardOfferGenerator(first, economy: economy, services: RewardServices(first));
        var secondGenerator = new RewardOfferGenerator(second, economy: economy, services: RewardServices(second));
        BattleRewardChoices firstChoices = null;
        BattleRewardChoices secondChoices = null;
        UnderOtherPlayer(second, () => firstChoices = firstGenerator.GenerateBattleChoices());
        UnderOtherPlayer(first, () => secondChoices = secondGenerator.GenerateBattleChoices());
        Assert.That(firstChoices.GoldAmount, Is.EqualTo(16));
        Assert.That(secondChoices.GoldAmount, Is.EqualTo(25));
        Assert.That(firstProbe.Inputs, Is.Empty, "Generating candidates must not grant gold or call relic gain hooks.");
        Assert.That(secondProbe.Inputs, Is.Empty);
        var firstReward = new RewardSession(first, firstChoices, 3);
        var secondReward = new RewardSession(second, secondChoices, 3);
        UnderOtherPlayer(second, () =>
        {
            ChoiceResult result = ClaimGold(firstReward);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.GoldBefore, Is.EqualTo(200));
            Assert.That(result.GoldAfter, Is.EqualTo(218));
        });
        Assert.That(second.Gold, Is.EqualTo(200));
        UnderOtherPlayer(first, () => Assert.That(ClaimGold(secondReward).GoldAfter, Is.EqualTo(229)));
        Assert.That(firstProbe.Inputs, Is.EqualTo(new[] { 16 }));
        Assert.That(secondProbe.Inputs, Is.EqualTo(new[] { 25 }));
        Assert.That(first.Gold, Is.EqualTo(218));
        Assert.That(second.Gold, Is.EqualTo(229));
        Assert.That(firstReward.SubmitChoice(new ChoiceSubmission("old", "gold")).Failure, Is.EqualTo(ChoiceFailure.NoPendingChoice));
    }

    [Test]
    public void RewardGoldCandidatePolicyReadsItsOwnerRatherThanTheAmbientRun()
    {
        PlayerStatus first = Player(1709, 7, 11, "gold_gain_80", "reward_magic_choices_minus_1");
        PlayerStatus second = Player(2903, 13, -11, "gold_gain_125", "reward_magic_choices_plus_1");
        BattleRewardChoices firstChoices = null;
        BattleRewardChoices secondChoices = null;
        UnderOtherPlayer(second, () => firstChoices = new RewardOfferGenerator(first, economy: economy, services: RewardServices(first)).GenerateBattleChoices());
        UnderOtherPlayer(first, () => secondChoices = new RewardOfferGenerator(second, economy: economy, services: RewardServices(second)).GenerateBattleChoices());
        // These historical IDs modify RewardGoldChoiceDelta, not the magic pool.
        Assert.That(firstChoices.GoldAmount, Is.Zero);
        Assert.That(secondChoices.GoldAmount, Is.EqualTo(25));
        Assert.That(new RewardSession(first, firstChoices, 3).PendingChoice.Candidates.Any(candidate => candidate.Amount > 0), Is.False);
        Assert.That(new RewardSession(second, secondChoices, 3).PendingChoice.Candidates.Single(candidate => candidate.Amount > 0).Amount, Is.EqualTo(25));
        Assert.That(first.Gold, Is.EqualTo(200));
        Assert.That(second.Gold, Is.EqualTo(200));
    }

    [Test]
    public void InterleavedEventEffectsApplyEachOwnersDifficultyAndRelicOnlyOnce()
    {
        PlayerStatus first = Reduced();
        PlayerStatus second = Enhanced();
        GoldProbe firstProbe = AttachGoldProbe(first, 2);
        GoldProbe secondProbe = AttachGoldProbe(second, 4);
        EventEffectData[] effects =
        {
            new EventEffectData { rewardType = EventRewardType.GainGold, amount = 20 },
            new EventEffectData { rewardType = EventRewardType.LoseGold, amount = 3 },
            new EventEffectData { rewardType = EventRewardType.GainGold, amount = 20 }
        };
        EventResolutionSession firstEvent = EventSession(first, EventServices(first), effects);
        EventResolutionSession secondEvent = EventSession(second, EventServices(second), effects);
        int firstStep = first.RunRandomStep;
        int secondStep = second.RunRandomStep;
        UnderOtherPlayer(second, () => Assert.That(firstEvent.Advance().Result.GoldAfter, Is.EqualTo(218)));
        Assert.That(second.Gold, Is.EqualTo(200));
        UnderOtherPlayer(first, () => Assert.That(secondEvent.Advance().Result.GoldAfter, Is.EqualTo(229)));
        UnderOtherPlayer(second, () => Assert.That(firstEvent.Advance().Result.GoldAfter, Is.EqualTo(215)));
        UnderOtherPlayer(first, () => Assert.That(secondEvent.Advance().Result.GoldAfter, Is.EqualTo(226)));
        UnderOtherPlayer(second, () => Assert.That(firstEvent.Advance().Result.GoldAfter, Is.EqualTo(233)));
        UnderOtherPlayer(first, () => Assert.That(secondEvent.Advance().Result.GoldAfter, Is.EqualTo(255)));
        UnderOtherPlayer(second, firstEvent.RunUntilChoiceOrComplete);
        UnderOtherPlayer(first, secondEvent.RunUntilChoiceOrComplete);
        Assert.That(firstProbe.Inputs, Is.EqualTo(new[] { 16, 16 }));
        Assert.That(secondProbe.Inputs, Is.EqualTo(new[] { 25, 25 }));
        Assert.That(firstEvent.IsComplete && secondEvent.IsComplete, Is.True);
        Assert.That(firstEvent.Event.CurrentNode.id, Is.EqualTo("done"));
        Assert.That(secondEvent.Event.GetOptionResolveCount(secondEvent.Option), Is.EqualTo(1));
        Assert.That(first.RunRandomStep, Is.EqualTo(firstStep));
        Assert.That(second.RunRandomStep, Is.EqualTo(secondStep));
    }

    [Test]
    public void BonusGoldUsesOwnerDifficultyAndRelicAndRetriedStepsCannotPayAgain()
    {
        PlayerStatus first = Reduced();
        PlayerStatus second = Enhanced();
        GoldProbe firstProbe = AttachGoldProbe(first, 2);
        GoldProbe secondProbe = AttachGoldProbe(second, 4);
        var firstGrid = new BonusGridSession(Grid(20), (min, max) => ChoiceRuleRandom.Next(first, min, max));
        var secondGrid = new BonusGridSession(Grid(20), (min, max) => ChoiceRuleRandom.Next(second, min, max));
        BonusGridStepResult firstStep = firstGrid.Step(MaterialEnum.Fire);
        BonusGridStepResult secondStep = secondGrid.Step(MaterialEnum.Fire);
        BonusGridRewardResult firstResult = null;
        BonusGridRewardResult secondResult = null;
        UnderOtherPlayer(second, () => firstResult = BonusGridRewardRules.Apply(firstStep, first, playGoldFeedback: false));
        UnderOtherPlayer(first, () => secondResult = BonusGridRewardRules.Apply(secondStep, second, playGoldFeedback: false));
        Assert.That(firstResult.GoldGained, Is.EqualTo(18));
        Assert.That(secondResult.GoldGained, Is.EqualTo(29));
        UnderOtherPlayer(second, () => Assert.That(BonusGridRewardRules.Apply(firstStep, first, playGoldFeedback: false), Is.SameAs(firstResult)));
        UnderOtherPlayer(first, () => Assert.That(BonusGridRewardRules.Apply(secondStep, second, playGoldFeedback: false), Is.SameAs(secondResult)));
        UnderOtherPlayer(first, () => Assert.Throws<InvalidOperationException>(() => BonusGridRewardRules.Apply(firstStep, second, playGoldFeedback: false)));
        Assert.That(first.Gold, Is.EqualTo(218));
        Assert.That(second.Gold, Is.EqualTo(229));
        Assert.That(firstProbe.Inputs, Is.EqualTo(new[] { 16 }));
        Assert.That(secondProbe.Inputs, Is.EqualTo(new[] { 25 }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NestedScopesRestoreExactAmbientMaterialAndMagicContexts(bool throwInside)
    {
        PlayerStatus first = Reduced();
        PlayerStatus second = Enhanced();
        var ambientMaterial = new MaterialModifierContext { PlayerState = second, EnemyBuffChanged = true };
        var ambientMagic = new MagicModifierContext { PlayerState = second };
        MaterialModifierModel.CurrentContext = ambientMaterial;
        MagicModifierModel.CurrentContext = ambientMagic;
        using (first.Rules.Enter())
        {
            AssertOwner(first);
            MaterialModifierContext outerMaterial = MaterialModifierModel.CurrentContext;
            var outerMagic = new MagicModifierContext { PlayerState = first };
            MagicModifierModel.CurrentContext = outerMagic;
            Action nested = () =>
            {
                using (second.Rules.Enter())
                {
                    AssertOwner(second);
                    Assert.That(MaterialModifierModel.CurrentContext, Is.Not.SameAs(outerMaterial));
                    MaterialModifierModel.CurrentContext = new MaterialModifierContext { PlayerState = second };
                    MagicModifierModel.CurrentContext = new MagicModifierContext { PlayerState = second };
                    if (throwInside) throw new InvalidOperationException("fixture nested failure");
                }
            };
            if (throwInside) Assert.Throws<InvalidOperationException>(() => nested());
            else nested();
            Assert.That(RuleExecutionScope.Current, Is.SameAs(first.Rules));
            Assert.That(MaterialModifierModel.CurrentContext, Is.SameAs(outerMaterial));
            Assert.That(MagicModifierModel.CurrentContext, Is.SameAs(outerMagic));
        }
        Assert.That(RuleExecutionScope.Current, Is.SameAs(savedRules));
        Assert.That(MaterialModifierModel.CurrentContext, Is.SameAs(ambientMaterial));
        Assert.That(MagicModifierModel.CurrentContext, Is.SameAs(ambientMagic));
        Assert.That(ambientMaterial.EnemyBuffChanged, Is.True);
    }

    [TestCase("shop", false)]
    [TestCase("shop", true)]
    [TestCase("reward_generation", false)]
    [TestCase("reward_generation", true)]
    [TestCase("reward_submission", false)]
    [TestCase("reward_submission", true)]
    [TestCase("event", false)]
    [TestCase("event", true)]
    [TestCase("bonus", false)]
    [TestCase("bonus", true)]
    public void ProductionBoundariesRestoreCallerContextsEvenWhenInjectedCodeThrows(string boundary, bool throwInside)
    {
        PlayerStatus owner = Reduced();
        PlayerStatus caller = Enhanced();
        int observations = 0;
        Action probe = () =>
        {
            AssertOwner(owner);
            observations++;
            MaterialModifierModel.CurrentContext = new MaterialModifierContext { PlayerState = owner };
            MagicModifierModel.CurrentContext = new MagicModifierContext { PlayerState = owner };
            if (throwInside) throw new InvalidOperationException("fixture boundary failure");
        };
        UnderOtherPlayer(caller, () =>
        {
            Action invoke = () => InvokeBoundary(boundary, owner, probe);
            if (throwInside) Assert.Throws<InvalidOperationException>(() => invoke());
            else invoke();
        });
        Assert.That(observations, Is.EqualTo(1));
        Assert.That(caller.Gold, Is.EqualTo(200));
    }

    private void InvokeBoundary(string boundary, PlayerStatus owner, Action probe)
    {
        switch (boundary)
        {
            case "shop":
                ShopGenerationServices shop = ShopServices(owner);
                shop.ModifyPrice = amount => { probe(); return DifficultyUpgradeSystem.ModifyShopPrice(amount); };
                new ShopSession(owner, economy, 3, shop).Open(new[] { MixedLayers()[1] });
                break;
            case "reward_generation":
                RewardGenerationServices rewards = RewardServices(owner);
                rewards.ModifyGold = amount => { probe(); return DifficultyUpgradeSystem.ModifyGoldGain(amount); };
                new RewardOfferGenerator(owner, economy: economy, services: rewards).GenerateBattleChoices();
                break;
            case "reward_submission":
                AttachGoldProbe(owner, 0, probe);
                Assert.That(ClaimGold(new RewardSession(owner, new BattleRewardChoices { GoldAmount = 16 }, 3)).Accepted, Is.True);
                break;
            case "event":
                EventRuleServices events = EventServices(owner);
                events.FindMaterialModifier = id => { probe(); return null; };
                EventSession(owner, events, new EventEffectData { rewardType = EventRewardType.GainMaterial, material = MaterialEnum.Fire }).Advance();
                break;
            case "bonus":
                BonusGridStepResult step = new BonusGridSession(Grid(20), (min, max) => min).Step(MaterialEnum.Fire);
                BonusGridRewardRules.Apply(step, owner, amount => { probe(); return DifficultyUpgradeSystem.ModifyGoldGain(amount); }, playGoldFeedback: false);
                break;
            default: Assert.Fail("Unknown fixture boundary: " + boundary); break;
        }
    }

    private sealed class RandomTrace
    {
        private readonly PlayerStatus owner;
        public readonly List<string> Calls = new List<string>();
        public RandomTrace(PlayerStatus owner) { this.owner = owner; }
        public int Ambient(int min, int max) { AssertOwner(owner); return Record(min, max, true); }
        public int Explicit(int min, int max) => Record(min, max, false);
        private int Record(int min, int max, bool ambient)
        {
            int step = owner.RunRandomStep;
            int expected = RunRandom.Range(owner.RunRandomSeed, step, min, max);
            int value = ChoiceRuleRandom.Next(ambient ? null : owner, min, max);
            Assert.That(owner.RunRandomStep, Is.EqualTo(step + 1));
            Assert.That(value, Is.EqualTo(expected));
            Calls.Add(min + ":" + max + ":" + value);
            return value;
        }
    }

    private string RandomShop(PlayerStatus owner, RandomTrace trace)
    {
        var session = new ShopSession(owner, economy, 3, ShopServices(owner, trace.Ambient));
        session.Open(MixedLayers());
        return string.Join("|", session.Offers.Select(offer => offer.kind + ":" + offer.price + ":" + offer.material + ":" + offer.magicData?.numericId));
    }

    private string RandomReward(PlayerStatus owner, RandomTrace trace)
    {
        BattleRewardChoices choices = new RewardOfferGenerator(owner, economy: economy, services: RewardServices(owner, trace.Ambient)).GenerateBattleChoices();
        return choices.GoldAmount + ":" + choices.Magic?.numericId + ":" + choices.Arrow.material;
    }

    private string RandomEvent(PlayerStatus owner, RandomTrace trace)
    {
        EventResolutionSession session = EventSession(owner, EventServices(owner, trace.Ambient),
            new EventEffectData { rewardType = EventRewardType.GainRandomMaterial, count = 3 });
        Assert.That(session.Advance().Kind, Is.EqualTo(EventResolutionStepKind.Effect));
        return string.Join(",", owner.Deck.Select(card => card.material));
    }

    private static string RandomGrid(PlayerStatus owner, RandomTrace trace)
    {
        var session = new BonusGridSession(Grid(10, 20, 30), trace.Explicit);
        return string.Join(",", Enumerable.Range(0, session.CellCount).Select(index => session.GetCell(index).Reward?.Amount));
    }

    private static string OnlyThisRun(PlayerStatus owner, PlayerStatus other, Func<string> operation)
    {
        int otherStep = other.RunRandomStep;
        string result = null;
        UnderOtherPlayer(other, () => result = operation());
        Assert.That(other.RunRandomStep, Is.EqualTo(otherStep), "One owner's production call must not consume the ambient caller's RNG.");
        return result;
    }

    [Test]
    public void InterleavedShopRewardEventAndBonusRandomMatchSeparateRunSequences()
    {
        PlayerStatus first = Reduced();
        PlayerStatus second = Enhanced();
        PlayerStatus firstControl = Reduced();
        PlayerStatus secondControl = Enhanced();
        var firstTrace = new RandomTrace(first);
        var secondTrace = new RandomTrace(second);
        var firstControlTrace = new RandomTrace(firstControl);
        var secondControlTrace = new RandomTrace(secondControl);
        var firstResults = new List<string>();
        var secondResults = new List<string>();
        firstResults.Add(OnlyThisRun(first, second, () => RandomShop(first, firstTrace)));
        secondResults.Add(OnlyThisRun(second, first, () => RandomEvent(second, secondTrace)));
        firstResults.Add(OnlyThisRun(first, second, () => RandomReward(first, firstTrace)));
        secondResults.Add(OnlyThisRun(second, first, () => RandomShop(second, secondTrace)));
        secondResults.Add(OnlyThisRun(second, first, () => RandomGrid(second, secondTrace)));
        firstResults.Add(OnlyThisRun(first, second, () => RandomEvent(first, firstTrace)));
        firstResults.Add(OnlyThisRun(first, second, () => RandomGrid(first, firstTrace)));
        secondResults.Add(OnlyThisRun(second, first, () => RandomReward(second, secondTrace)));
        var firstExpected = new[]
        {
            OnlyThisRun(firstControl, secondControl, () => RandomShop(firstControl, firstControlTrace)),
            OnlyThisRun(firstControl, secondControl, () => RandomReward(firstControl, firstControlTrace)),
            OnlyThisRun(firstControl, secondControl, () => RandomEvent(firstControl, firstControlTrace)),
            OnlyThisRun(firstControl, secondControl, () => RandomGrid(firstControl, firstControlTrace))
        };
        var secondExpected = new[]
        {
            OnlyThisRun(secondControl, firstControl, () => RandomEvent(secondControl, secondControlTrace)),
            OnlyThisRun(secondControl, firstControl, () => RandomShop(secondControl, secondControlTrace)),
            OnlyThisRun(secondControl, firstControl, () => RandomGrid(secondControl, secondControlTrace)),
            OnlyThisRun(secondControl, firstControl, () => RandomReward(secondControl, secondControlTrace))
        };
        Assert.That(firstResults, Is.EqualTo(firstExpected));
        Assert.That(secondResults, Is.EqualTo(secondExpected));
        Assert.That(firstTrace.Calls, Is.EqualTo(firstControlTrace.Calls));
        Assert.That(secondTrace.Calls, Is.EqualTo(secondControlTrace.Calls));
        Assert.That(firstTrace.Calls.Count, Is.GreaterThan(8));
        Assert.That(secondTrace.Calls.Count, Is.GreaterThan(8));
        Assert.That(first.RunRandomStep, Is.EqualTo(firstControl.RunRandomStep));
        Assert.That(second.RunRandomStep, Is.EqualTo(secondControl.RunRandomStep));
    }

    [Test]
    public void DefaultRewardAndEventRandomBindingsUseOwnerNotOppositeAmbientPlayer()
    {
        PlayerStatus first = Reduced();
        PlayerStatus second = Enhanced();
        int firstStart = first.RunRandomStep;
        int secondStart = second.RunRandomStep;
        EventResolutionSession firstEvent = EventSession(first, EventServices(first), new EventEffectData { rewardType = EventRewardType.GainRandomMaterial, count = 2 });
        EventResolutionSession secondEvent = EventSession(second, EventServices(second), new EventEffectData { rewardType = EventRewardType.GainRandomMaterial, count = 2 });
        UnderOtherPlayer(second, () => firstEvent.Advance());
        Assert.That(second.RunRandomStep, Is.EqualTo(secondStart));
        UnderOtherPlayer(first, () => secondEvent.Advance());
        Assert.That(first.RunRandomStep, Is.EqualTo(firstStart + 2));
        Assert.That(second.RunRandomStep, Is.EqualTo(secondStart + 2));
        Assert.That(first.Deck.Select(card => (int)card.material), Is.EqualTo(Enumerable.Range(firstStart, 2).Select(step => RunRandom.Range(first.RunRandomSeed, step, (int)MaterialEnum.Fire, (int)MaterialEnum.Earth + 1))));
        Assert.That(second.Deck.Select(card => (int)card.material), Is.EqualTo(Enumerable.Range(secondStart, 2).Select(step => RunRandom.Range(second.RunRandomSeed, step, (int)MaterialEnum.Fire, (int)MaterialEnum.Earth + 1))));
        List<RewardArrowOption> firstArrows = null;
        List<RewardArrowOption> secondArrows = null;
        UnderOtherPlayer(second, () => firstArrows = new RewardOfferGenerator(first, economy: economy, services: RewardServices(first)).GetArrowChoices(2));
        Assert.That(second.RunRandomStep, Is.EqualTo(secondStart + 2));
        UnderOtherPlayer(first, () => secondArrows = new RewardOfferGenerator(second, economy: economy, services: RewardServices(second)).GetArrowChoices(2));
        Assert.That(firstArrows.Select(arrow => (int)arrow.material), Is.EqualTo(Enumerable.Range(firstStart + 2, 2).Select(step => RunRandom.Range(first.RunRandomSeed, step, (int)MaterialEnum.Fire, (int)MaterialEnum.Earth + 1))));
        Assert.That(secondArrows.Select(arrow => (int)arrow.material), Is.EqualTo(Enumerable.Range(secondStart + 2, 2).Select(step => RunRandom.Range(second.RunRandomSeed, step, (int)MaterialEnum.Fire, (int)MaterialEnum.Earth + 1))));
        Assert.That(first.RunRandomStep, Is.EqualTo(firstStart + 4));
        Assert.That(second.RunRandomStep, Is.EqualTo(secondStart + 4));
    }

    [Test]
    public void LegalShopQueriesAndStableChoiceObservationsConsumeNeitherPlayersRandom()
    {
        PlayerStatus first = Reduced();
        PlayerStatus second = Enhanced();
        MaterialModel firstCard = first.AddDeckMaterial(MaterialEnum.Fire);
        MaterialModel secondCard = second.AddDeckMaterial(MaterialEnum.Water);
        var firstTrace = new RandomTrace(first);
        var secondTrace = new RandomTrace(second);
        var firstShop = new ShopSession(first, economy, 3, ShopServices(first, firstTrace.Ambient));
        var secondShop = new ShopSession(second, economy, 3, ShopServices(second, secondTrace.Ambient));
        firstShop.Open(MixedLayers());
        secondShop.Open(MixedLayers());
        var firstReward = new RewardSession(first, new RewardOfferGenerator(first, economy: economy, services: RewardServices(first, firstTrace.Ambient)).GenerateBattleChoices(), 3);
        var secondReward = new RewardSession(second, new RewardOfferGenerator(second, economy: economy, services: RewardServices(second, secondTrace.Ambient)).GenerateBattleChoices(), 3);
        EventResolutionSession firstEvent = EventSession(first, EventServices(first, firstTrace.Ambient), new EventEffectData { rewardType = EventRewardType.RemoveMaterial });
        EventResolutionSession secondEvent = EventSession(second, EventServices(second, secondTrace.Ambient), new EventEffectData { rewardType = EventRewardType.RemoveMaterial });
        firstEvent.RunUntilChoiceOrComplete();
        secondEvent.RunUntilChoiceOrComplete();
        var firstGrid = new BonusGridSession(Grid(10, 20), firstTrace.Explicit);
        var secondGrid = new BonusGridSession(Grid(10, 20), secondTrace.Explicit);
        PendingChoice[] pending = { firstReward.PendingChoice, secondReward.PendingChoice, firstEvent.PendingChoice, secondEvent.PendingChoice };
        string[][] ids = pending.Select(choice => choice.Candidates.Select(candidate => candidate.Id).ToArray()).ToArray();
        int firstStep = first.RunRandomStep;
        int secondStep = second.RunRandomStep;
        int firstCalls = firstTrace.Calls.Count;
        int secondCalls = secondTrace.Calls.Count;
        for (int read = 0; read < 4; read++)
        {
            UnderOtherPlayer(second, () => ObserveShop(firstShop, firstCard, 12));
            UnderOtherPlayer(first, () => ObserveShop(secondShop, secondCard, 5));
            for (int index = 0; index < pending.Length; index++)
            {
                PendingChoice choice = pending[index];
                Assert.That(choice.Candidates.Select(candidate => candidate.Id), Is.EqualTo(ids[index]));
                ChoiceCandidate candidate = choice.Candidates[0];
                Assert.That(choice.Find(candidate.Id), Is.SameAs(candidate));
                Assert.That(choice.Validate(new ChoiceSubmission(choice.Id, candidate.Id), out List<ChoiceCandidate> selected), Is.EqualTo(ChoiceFailure.None));
                Assert.That(selected.Single(), Is.SameAs(candidate));
            }
            UnderOtherPlayer(second, () => Assert.That(firstEvent.Advance().Kind, Is.EqualTo(EventResolutionStepKind.Choice)));
            UnderOtherPlayer(first, () => Assert.That(secondEvent.Advance().Kind, Is.EqualTo(EventResolutionStepKind.Choice)));
            for (int index = 0; index < firstGrid.CellCount; index++)
            {
                Assert.That(firstGrid.GetCell(index).Collected, Is.False);
                Assert.That(secondGrid.GetCell(index).Collected, Is.False);
            }
        }
        Assert.That(firstReward.PendingChoice, Is.SameAs(pending[0]));
        Assert.That(secondReward.PendingChoice, Is.SameAs(pending[1]));
        Assert.That(firstEvent.PendingChoice, Is.SameAs(pending[2]));
        Assert.That(secondEvent.PendingChoice, Is.SameAs(pending[3]));
        Assert.That(first.RunRandomStep, Is.EqualTo(firstStep));
        Assert.That(second.RunRandomStep, Is.EqualTo(secondStep));
        Assert.That(firstTrace.Calls.Count, Is.EqualTo(firstCalls));
        Assert.That(secondTrace.Calls.Count, Is.EqualTo(secondCalls));
        Assert.That(first.Gold, Is.EqualTo(200));
        Assert.That(second.Gold, Is.EqualTo(200));
        Assert.That(first.Deck.Single(), Is.SameAs(firstCard));
        Assert.That(second.Deck.Single(), Is.SameAs(secondCard));
    }

    private static void ObserveShop(ShopSession session, MaterialModel card, int removalPrice)
    {
        Assert.That(session.RemoveArrowPrice, Is.EqualTo(removalPrice));
        Assert.That(session.CanAfford(session.Offers[0]), Is.True);
        Assert.That(session.CanUseOffer(session.Offers[0]), Is.True);
        Assert.That(session.IndexOfOffer(session.Offers[0]), Is.Zero);
        Assert.That(session.GetFreeMagicSlotIndex(), Is.Zero);
        Assert.That(session.HasRemovableMaterial(), Is.True);
        Assert.That(session.IsRemovableMaterial(card), Is.True);
        Assert.That(RewardSession.FirstFreeSlot(session.Owner, 3), Is.Zero);
        Assert.That(session.ExportState().offers.Length, Is.EqualTo(2), "Export is an in-memory observation, not a disk save.");
    }
}
