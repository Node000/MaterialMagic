using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class RewardChoiceSessionTests
{
    private PlayerState player;

    [SetUp]
    public void SetUp()
    {
        player = new PlayerState(50, 10, PlayerStateServices.Simulation,
            new RunDifficultySaveData { upgradeIds = Array.Empty<string>() });
    }

    private static MagicData Magic(int id)
        => new MagicData { numericId = id, id = "choice_reward_" + id, recipe = new[] { MaterialEnum.Fire } };

    private static MaterialModifierData ArrowModifier()
        => new MaterialModifierData { id = "choice_kindling", script = nameof(KindlingModifier) };

    private RewardSession Battle(int capacity = 2)
        => new RewardSession(player, new BattleRewardChoices
        {
            GoldAmount = 7,
            Magic = Magic(81001),
            Arrow = new RewardArrowOption { material = MaterialEnum.Wind, modifierData = ArrowModifier() }
        }, capacity, "reward_choice");

    private static RewardGenerationServices GenerationServices(params MagicData[] magic)
        => new RewardGenerationServices
        {
            NextRandomInt = (min, max) => min,
            FindMagic = id => magic.FirstOrDefault(data => data.numericId == id),
            FindRewardPool = id => null,
            AllMagic = () => magic,
            IsMagicUnlocked = data => true,
            EliteLegendaryGuarantee = () => false,
            MaterialModifierChoices = count => Array.Empty<MaterialModifierData>(),
            ModifyGold = amount => amount,
            ModifyGoldChoiceCount = count => count,
            ReportInsufficientPool = message => { }
        };

    [TestCase(1)]
    [TestCase(2)]
    public void ProductionGeneratorRetainsOwnedWeightWithoutCompoundingDuplicateCopies(int ownedCopies)
    {
        MagicData first = Magic(81001);
        MagicData second = Magic(81002);
        for (int slot = 0; slot < ownedCopies; slot++)
            player.SetMagicAtSlot(MagicFactory.Create(Magic(first.numericId), slot), slot);
        RewardGenerationServices services = GenerationServices(first, second);
        // Returning the same fraction for every roll works whether the rarity tier needs a roll or not.
        services.NextRandomInt = (min, max) => min + (max - min) / 4;
        var generator = new RewardOfferGenerator(player,
            economy: new EconomyConfigData { ownedMagicWeightMultiplier = 0.25f }, services: services);
        Assert.That(generator.GetMagicChoices(1).Single(), Is.SameAs(second));
        services.NextRandomInt = (min, max) => min + (max - min) / 10;
        Assert.That(generator.GetMagicChoices(1).Single(), Is.SameAs(first), "Two owned copies must not compound the ownership penalty.");
        player.MagicBook.Clear();
        services.NextRandomInt = (min, max) => min + (max - min) / 4;
        Assert.That(generator.GetMagicChoices(1).Single(), Is.SameAs(first), "A new roll reads current ownership, not a cached snapshot.");
    }

    [Test]
    public void ProductionGeneratorMultiplierOneDisablesPenaltyAndChoicesStayUnique()
    {
        MagicData first = Magic(81001);
        MagicData second = Magic(81002);
        player.SetMagicAtSlot(MagicFactory.Create(Magic(first.numericId), 0), 0);
        RewardGenerationServices services = GenerationServices(first, second);
        services.NextRandomInt = (min, max) => min + (max - min) / 4;
        var generator = new RewardOfferGenerator(player,
            economy: new EconomyConfigData { ownedMagicWeightMultiplier = 1f }, services: services);
        IReadOnlyList<MagicData> choices = generator.GetMagicChoices(2);
        Assert.That(choices, Is.EqualTo(new[] { first, second }));
        Assert.That(choices.Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public void ProductionEliteGuaranteeKeepsLegendaryRarityAndOwnedWeight()
    {
        MagicData common = Magic(81001);
        MagicData ownedLegendary = Magic(81002);
        MagicData newLegendary = Magic(81003);
        ownedLegendary.rarity = MagicRarity.Legendary;
        newLegendary.rarity = MagicRarity.Legendary;
        player.SetMagicAtSlot(MagicFactory.Create(ownedLegendary, 0), 0);
        RewardGenerationServices services = GenerationServices(common, ownedLegendary, newLegendary);
        services.EliteLegendaryGuarantee = () => true;
        int randomCalls = 0;
        services.NextRandomInt = (min, max) => { randomCalls++; return min + (max - min) / 4; };
        var generator = new RewardOfferGenerator(player, new LevelData { levelType = LevelType.Elite },
            economy: new EconomyConfigData { ownedMagicWeightMultiplier = 0.25f }, services: services);
        Assert.That(generator.RollBattleMagic(), Is.SameAs(newLegendary));
        Assert.That(randomCalls, Is.EqualTo(1), "Elite guaranteed rarity directly draws within the legendary tier.");
    }

    [Test]
    public void ProductionStrictRarityEmptyPoolDoesNotFallBackToGlobalContent()
    {
        MagicData common = Magic(81001);
        MagicData globalLegendary = Magic(81002);
        globalLegendary.rarity = MagicRarity.Legendary;
        var pool = new RewardPoolData
        {
            numericId = 81100, id = "strict_legendary", magicIds = new[] { common.numericId },
            allowedRarities = new[] { MagicRarity.Legendary }
        };
        RewardGenerationServices services = GenerationServices(common, globalLegendary);
        services.FindRewardPool = id => id == pool.numericId ? pool : null;
        services.AllMagic = () => { Assert.Fail("A strict rarity pool must not fall back to AllMagic."); return Array.Empty<MagicData>(); };
        services.NextRandomInt = (min, max) => { Assert.Fail("An empty strict pool must not consume RNG."); return min; };
        int reports = 0;
        services.ReportInsufficientPool = message => reports++;
        var generator = new RewardOfferGenerator(player, new LevelData { rewardPoolId = pool.numericId },
            economy: new EconomyConfigData(), services: services);
        Assert.That(generator.RollBattleMagic(), Is.Null);
        Assert.That(generator.GetMagicChoices(3), Is.Empty);
        Assert.That(reports, Is.EqualTo(1));
        Assert.That(player.MagicBook, Is.Empty);
    }

    [Test]
    public void GeneratedBattleSessionKeepsCandidatesWithoutFurtherRandomOrContentCalls()
    {
        MagicData magic = Magic(81001);
        MaterialModifierData modifier = ArrowModifier();
        RewardGenerationServices services = GenerationServices(magic);
        int randomCalls = 0;
        int modifierCalls = 0;
        int goldCalls = 0;
        services.NextRandomInt = (min, max) => { randomCalls++; return min; };
        services.MaterialModifierChoices = count => { modifierCalls++; return new[] { modifier }; };
        services.ModifyGold = amount => { goldCalls++; return amount * 2; };
        var generator = new RewardOfferGenerator(player, economy: new EconomyConfigData
        {
            battleGoldMin = 4, battleArrowRewardEnchantChance = 1f, ownedMagicWeightMultiplier = 1f
        }, services: services);
        BattleRewardChoices generated = generator.GenerateBattleChoices();
        int callsAfterGeneration = randomCalls;
        Assert.That(callsAfterGeneration, Is.GreaterThan(0));
        Assert.That(generated.GoldAmount, Is.EqualTo(8));
        Assert.That(generated.Magic, Is.SameAs(magic));
        Assert.That(generated.Arrow.modifierData, Is.SameAs(modifier));
        var session = new RewardSession(player, generated, 2);
        PendingChoice pending = session.PendingChoice;
        ChoiceCandidate[] candidates = pending.Candidates.ToArray();
        for (int i = 0; i < 4; i++)
        {
            Assert.That(session.PendingChoice, Is.SameAs(pending));
            Assert.That(session.PendingChoice.Candidates, Is.EqualTo(candidates));
            Assert.That(session.SubmitChoice(new ChoiceSubmission(pending.Id, "unknown")).Failure, Is.EqualTo(ChoiceFailure.UnknownCandidate));
        }
        ChoiceCandidate arrow = candidates.Single(candidate => candidate.Arrow != null);
        Assert.That(session.SubmitChoice(new ChoiceSubmission(pending.Id, arrow.Id)).Accepted, Is.True);
        Assert.That(randomCalls, Is.EqualTo(callsAfterGeneration));
        Assert.That(modifierCalls, Is.EqualTo(1));
        Assert.That(goldCalls, Is.EqualTo(1));
        Assert.That(player.Deck.Single().modifiers.Single(), Is.TypeOf<KindlingModifier>());
    }

    [Test]
    public void BattleCandidatesAreStableAndOpeningDoesNotGrantAnything()
    {
        var input = new BattleRewardChoices
        {
            GoldAmount = 7,
            Magic = Magic(81001),
            Arrow = new RewardArrowOption { material = MaterialEnum.Wind, modifierData = ArrowModifier() }
        };
        var session = new RewardSession(player, input, 2, "stable_reward");
        PendingChoice pending = session.PendingChoice;
        ChoiceCandidate[] candidates = pending.Candidates.ToArray();
        string[] ids = candidates.Select(candidate => candidate.Id).ToArray();
        input.GoldAmount = 900;
        input.Magic = Magic(81002);
        input.Arrow.material = MaterialEnum.Earth;
        input.Arrow.modifierData = null;

        Assert.That(pending.Kind, Is.EqualTo(ChoiceKind.BattleReward));
        Assert.That(pending.MinSelections, Is.EqualTo(1));
        Assert.That(pending.MaxSelections, Is.EqualTo(1));
        Assert.That(pending.CanCancel, Is.True);
        Assert.That(candidates.Length, Is.EqualTo(3));
        Assert.That(ids.Distinct().Count(), Is.EqualTo(3));
        for (int read = 0; read < 3; read++)
        {
            Assert.That(session.PendingChoice, Is.SameAs(pending));
            Assert.That(session.PendingChoice.Candidates.Select(candidate => candidate.Id), Is.EqualTo(ids));
            for (int i = 0; i < candidates.Length; i++)
                Assert.That(session.PendingChoice.Candidates[i], Is.SameAs(candidates[i]));
        }
        Assert.That(candidates.Single(candidate => candidate.Amount > 0).Amount, Is.EqualTo(7));
        Assert.That(candidates.Single(candidate => candidate.MagicData != null).MagicData.numericId, Is.EqualTo(81001));
        RewardArrowOption arrow = candidates.Single(candidate => candidate.Arrow != null).Arrow;
        Assert.That(arrow.material, Is.EqualTo(MaterialEnum.Wind));
        Assert.That(arrow.modifierData.script, Is.EqualTo(nameof(KindlingModifier)));
        Assert.That(player.Gold, Is.EqualTo(10));
        Assert.That(player.Deck, Is.Empty);
        Assert.That(player.MagicBook, Is.Empty);
        Assert.That(session.IsComplete, Is.False);
        Assert.That(session.LastResult, Is.Null);
    }

    [Test]
    public void GoldCommitsOneRelicAdjustedGainAndRepeatedSubmissionIsRejected()
    {
        player.Relics.TryAcquire(new GoldBonusRelicModel(new RelicData { numericId = 81991, value = 2 }));
        int gains = 0;
        player.Relics.Resolved += fact => { if (fact.Kind == RelicEventKind.GoldChanged) gains++; };
        RewardSession session = Battle();
        PendingChoice pending = session.PendingChoice;
        var submission = new ChoiceSubmission(pending.Id, pending.Candidates.Single(candidate => candidate.Amount > 0).Id);

        ChoiceResult result = session.SubmitChoice(submission);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.GoldBefore, Is.EqualTo(10));
        Assert.That(result.GoldAfter, Is.EqualTo(19), "Battle gold is already difficulty-adjusted; only the relic bonus is added here.");
        Assert.That(result.HealthBefore, Is.EqualTo(50));
        Assert.That(result.HealthAfter, Is.EqualTo(50));
        Assert.That(session.LastResult, Is.SameAs(result));
        Assert.That(session.PendingChoice, Is.Null);
        Assert.That(session.IsComplete, Is.True);
        Assert.That(session.SubmitChoice(submission).Failure, Is.EqualTo(ChoiceFailure.NoPendingChoice));
        Assert.That(session.SubmitChoice(ChoiceSubmission.CancelChoice(pending.Id)).Failure, Is.EqualTo(ChoiceFailure.NoPendingChoice));
        Assert.That(player.Gold, Is.EqualTo(19));
        Assert.That(gains, Is.EqualTo(1));
        Assert.That(player.Deck, Is.Empty);
        Assert.That(player.MagicBook, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SkipCompletesBattleOrMagicOnlyWithoutGrantingAnything(bool magicOnly)
    {
        RewardSession session = magicOnly ? RewardSession.MagicOnly(player, new[] { Magic(81001) }, 2) : Battle();
        PendingChoice pending = session.PendingChoice;

        ChoiceResult result = session.SubmitChoice(ChoiceSubmission.CancelChoice(pending.Id));

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Cancelled, Is.True);
        Assert.That(result.GoldBefore, Is.EqualTo(10));
        Assert.That(result.GoldAfter, Is.EqualTo(10));
        Assert.That(result.AcquiredMagic, Is.Null);
        Assert.That(result.AddedMaterial, Is.Null);
        Assert.That(session.IsComplete, Is.True);
        Assert.That(session.PendingChoice, Is.Null);
        Assert.That(player.Gold, Is.EqualTo(10));
        Assert.That(player.Deck, Is.Empty);
        Assert.That(player.MagicBook, Is.Empty);
        Assert.That(session.SubmitChoice(ChoiceSubmission.CancelChoice(pending.Id)).Failure, Is.EqualTo(ChoiceFailure.NoPendingChoice));
    }

    [Test]
    public void FullMagicInventoryDoesNotConsumeRewardAndSameCandidateCanBeRetried()
    {
        MagicModel first = MagicFactory.Create(Magic(81011), 0);
        MagicModel second = MagicFactory.Create(Magic(81012), 1);
        player.SetMagicAtSlot(first, 0);
        player.SetMagicAtSlot(second, 1);
        RewardSession session = Battle();
        PendingChoice pending = session.PendingChoice;
        ChoiceCandidate candidate = pending.Candidates.Single(item => item.MagicData != null);
        var submission = new ChoiceSubmission(pending.Id, candidate.Id);

        Assert.That(session.SubmitChoice(submission).Failure, Is.EqualTo(ChoiceFailure.NoMagicSlot));
        Assert.That(session.PendingChoice, Is.SameAs(pending));
        Assert.That(session.PendingChoice.Find(candidate.Id), Is.SameAs(candidate));
        Assert.That(session.IsComplete, Is.False);
        Assert.That(session.LastResult, Is.Null);
        Assert.That(player.GetMagicAtSlot(0), Is.SameAs(first));
        Assert.That(player.GetMagicAtSlot(1), Is.SameAs(second));
        Assert.That(player.Gold, Is.EqualTo(10));
        Assert.That(player.Deck, Is.Empty);

        player.ClearMagicSlot(1);
        ChoiceResult result = session.SubmitChoice(submission);
        Assert.That(result.Accepted, Is.True);
        Assert.That(result.MagicSlotIndex, Is.EqualTo(1));
        Assert.That(result.ResultingMagicCount, Is.EqualTo(2));
        Assert.That(result.AcquiredMagic.Data, Is.SameAs(candidate.MagicData));
        Assert.That(player.GetMagicAtSlot(1), Is.SameAs(result.AcquiredMagic));
        Assert.That(player.GetMagicAtSlot(0), Is.SameAs(first));
        Assert.That(session.SubmitChoice(submission).Failure, Is.EqualTo(ChoiceFailure.NoPendingChoice));
    }

    [Test]
    public void FullMagicOnlyInventoryCanStillSkipAfterRejectedClaim()
    {
        player.SetMagicAtSlot(MagicFactory.Create(Magic(81011), 0), 0);
        RewardSession session = RewardSession.MagicOnly(player, new[] { Magic(81001) }, 1);
        PendingChoice pending = session.PendingChoice;
        Assert.That(session.SubmitChoice(new ChoiceSubmission(pending.Id, pending.Candidates[0].Id)).Failure,
            Is.EqualTo(ChoiceFailure.NoMagicSlot));
        Assert.That(session.SubmitChoice(ChoiceSubmission.CancelChoice(pending.Id)).Accepted, Is.True);
        Assert.That(player.MagicBook.Count, Is.EqualTo(1));
        Assert.That(player.GetMagicAtSlot(0).NumericId, Is.EqualTo(81011));
        Assert.That(session.IsComplete, Is.True);
    }

    [Test]
    public void MagicRewardFillsFirstHoleAndDoesNotReplaceExistingSlots()
    {
        MagicModel existing = MagicFactory.Create(Magic(81011), 1);
        player.SetMagicAtSlot(existing, 1);
        RewardSession session = RewardSession.MagicOnly(player, new[] { Magic(81001) }, 3);
        PendingChoice pending = session.PendingChoice;

        ChoiceResult result = session.SubmitChoice(new ChoiceSubmission(pending.Id, pending.Candidates[0].Id));

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.MagicSlotIndex, Is.Zero);
        Assert.That(player.GetMagicAtSlot(0), Is.SameAs(result.AcquiredMagic));
        Assert.That(player.GetMagicAtSlot(1), Is.SameAs(existing));
        Assert.That(player.GetMagicAtSlot(2), Is.Null);
        Assert.That(result.ResultingMagicCount, Is.EqualTo(2));
        Assert.That(player.Gold, Is.EqualTo(10));
    }

    [Test]
    public void ArrowRewardAddsOneRealDeckIdentityWithExactlyOneModifier()
    {
        RewardSession session = Battle();
        PendingChoice pending = session.PendingChoice;
        ChoiceCandidate candidate = pending.Candidates.Single(item => item.Arrow != null);
        var submission = new ChoiceSubmission(pending.Id, candidate.Id);

        ChoiceResult result = session.SubmitChoice(submission);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.AddedMaterial, Is.Not.Null);
        Assert.That(result.AddedMaterial.material, Is.EqualTo(MaterialEnum.Wind));
        Assert.That(player.Deck.Single(), Is.SameAs(result.AddedMaterial));
        Assert.That(player.DrawPile.Single(), Is.SameAs(result.AddedMaterial));
        Assert.That(result.AddedMaterial.modifiers.Count, Is.EqualTo(1));
        Assert.That(result.AddedMaterial.modifiers[0], Is.TypeOf<KindlingModifier>());
        Assert.That(result.AddedMaterial.modifiers[0].model, Is.SameAs(result.AddedMaterial));
        Assert.That(session.SubmitChoice(submission).Failure, Is.EqualTo(ChoiceFailure.NoPendingChoice));
        Assert.That(player.Deck.Count, Is.EqualTo(1));
        Assert.That(result.AddedMaterial.modifiers.Count, Is.EqualTo(1));
        Assert.That(player.MagicBook, Is.Empty);
        Assert.That(player.Gold, Is.EqualTo(10));
    }

    [Test]
    public void InvalidAnswersLeaveTheOriginalRewardAvailable()
    {
        RewardSession session = Battle();
        PendingChoice pending = session.PendingChoice;
        string first = pending.Candidates[0].Id;
        ChoiceSubmission[] invalid =
        {
            null,
            new ChoiceSubmission("expired_reward", first),
            new ChoiceSubmission(pending.Id),
            new ChoiceSubmission(pending.Id, first, pending.Candidates[1].Id),
            new ChoiceSubmission(pending.Id, "unknown_candidate"),
            new ChoiceSubmission(pending.Id, true, first)
        };
        ChoiceFailure[] failures =
        {
            ChoiceFailure.StaleChoice, ChoiceFailure.StaleChoice, ChoiceFailure.InvalidCount,
            ChoiceFailure.InvalidCount, ChoiceFailure.UnknownCandidate, ChoiceFailure.CannotCancel
        };
        for (int i = 0; i < invalid.Length; i++)
        {
            Assert.That(session.SubmitChoice(invalid[i]).Failure, Is.EqualTo(failures[i]));
            Assert.That(session.PendingChoice, Is.SameAs(pending));
            Assert.That(session.IsComplete, Is.False);
            Assert.That(session.LastResult, Is.Null);
            Assert.That(player.Gold, Is.EqualTo(10));
            Assert.That(player.Deck, Is.Empty);
            Assert.That(player.MagicBook, Is.Empty);
        }
        Assert.That(session.SubmitChoice(new ChoiceSubmission(pending.Id, first)).Accepted, Is.True);
    }

    [Test]
    public void MagicOnlySnapshotsCandidateListAndKeepsDuplicateContentIdentitiesDistinct()
    {
        MagicData shared = Magic(81001);
        MagicData[] supplied = { shared, null, shared };
        RewardSession session = RewardSession.MagicOnly(player, supplied, 2);
        PendingChoice pending = session.PendingChoice;
        supplied[0] = Magic(81002);
        supplied[2] = null;

        Assert.That(pending.Kind, Is.EqualTo(ChoiceKind.MagicReward));
        Assert.That(pending.Candidates.Count, Is.EqualTo(2));
        Assert.That(pending.Candidates[0].Id, Is.Not.EqualTo(pending.Candidates[1].Id));
        Assert.That(pending.Candidates.All(candidate => ReferenceEquals(candidate.MagicData, shared)), Is.True);
        ChoiceResult result = session.SubmitChoice(new ChoiceSubmission(pending.Id, pending.Candidates[1].Id));
        Assert.That(result.Accepted, Is.True);
        Assert.That(result.AcquiredMagic.Data, Is.SameAs(shared));
        Assert.That(player.MagicBook.Count, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AnswerFromPreviousRewardCannotClaimNewSessionWithSamePrefixAndContent(bool magicOnly)
    {
        MagicData data = Magic(81001);
        RewardSession previous = magicOnly
            ? RewardSession.MagicOnly(player, new[] { data }, 3, "repeated_reward")
            : Battle();
        PendingChoice oldChoice = previous.PendingChoice;
        var oldAnswer = new ChoiceSubmission(oldChoice.Id, oldChoice.Candidates[0].Id);
        Assert.That(previous.SubmitChoice(oldAnswer).Accepted, Is.True);
        int gold = player.Gold;
        int magicCount = player.MagicBook.Count;
        RewardSession next = magicOnly
            ? RewardSession.MagicOnly(player, new[] { data }, 3, "repeated_reward")
            : Battle();
        PendingChoice pending = next.PendingChoice;
        Assert.That(pending.Id, Is.Not.EqualTo(oldChoice.Id));
        Assert.That(next.SubmitChoice(oldAnswer).Failure, Is.EqualTo(ChoiceFailure.StaleChoice));
        Assert.That(next.PendingChoice, Is.SameAs(pending));
        Assert.That(next.IsComplete, Is.False);
        Assert.That(player.Gold, Is.EqualTo(gold));
        Assert.That(player.MagicBook.Count, Is.EqualTo(magicCount));
        Assert.That(next.SubmitChoice(new ChoiceSubmission(pending.Id, pending.Candidates[0].Id)).Accepted, Is.True);
    }

    [Test]
    public void SynchronousGoldObserverCannotReenterAndClaimTheRewardTwice()
    {
        RewardSession session = Battle();
        PendingChoice pending = session.PendingChoice;
        var answer = new ChoiceSubmission(pending.Id, pending.Candidates.Single(candidate => candidate.Amount > 0).Id);
        int attempts = 0;
        player.Relics.Resolved += fact =>
        {
            if (fact.Kind != RelicEventKind.GoldChanged) return;
            attempts++;
            Assert.That(session.SubmitChoice(answer).Failure, Is.EqualTo(ChoiceFailure.Busy));
        };
        Assert.That(session.SubmitChoice(answer).Accepted, Is.True);
        Assert.That(attempts, Is.EqualTo(1));
        Assert.That(player.Gold, Is.EqualTo(17));
        Assert.That(session.IsComplete, Is.True);
        Assert.That(session.SubmitChoice(answer).Failure, Is.EqualTo(ChoiceFailure.NoPendingChoice));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EmptyRewardIsAnExplicitZeroSelectionDecision(bool magicOnly)
    {
        RewardSession session = magicOnly
            ? RewardSession.MagicOnly(player, Array.Empty<MagicData>(), 0)
            : new RewardSession(player, new BattleRewardChoices { GoldAmount = -3, Arrow = new RewardArrowOption { material = MaterialEnum.None } }, 0);
        PendingChoice pending = session.PendingChoice;
        Assert.That(pending.Candidates, Is.Empty);
        Assert.That(pending.MinSelections, Is.Zero);
        Assert.That(pending.MaxSelections, Is.Zero);
        Assert.That(session.IsComplete, Is.False, "A missing view must not silently answer even an empty reward.");
        Assert.That(session.SubmitChoice(new ChoiceSubmission(pending.Id)).Accepted, Is.True);
        Assert.That(session.IsComplete, Is.True);
        Assert.That(player.Gold, Is.EqualTo(10));
        Assert.That(player.Deck, Is.Empty);
        Assert.That(player.MagicBook, Is.Empty);
    }
}
