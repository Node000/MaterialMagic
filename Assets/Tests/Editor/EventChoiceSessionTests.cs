using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class EventChoiceSessionTests
{
    private PlayerState player;
    private EventRuleServices services;
    private readonly Dictionary<int, MagicData> magicById = new Dictionary<int, MagicData>();
    private readonly Dictionary<string, MagicModifierData> magicModifiersById = new Dictionary<string, MagicModifierData>();
    private readonly Dictionary<string, MaterialModifierData> materialModifiersById = new Dictionary<string, MaterialModifierData>();
    private readonly List<MagicData> magicChoices = new List<MagicData>();
    private readonly List<MagicModifierData> magicModifierChoices = new List<MagicModifierData>();
    private readonly List<MaterialModifierData> materialModifierChoices = new List<MaterialModifierData>();
    private readonly List<int> magicRequests = new List<int>();
    private readonly List<int> magicModifierRequests = new List<int>();
    private readonly List<int> materialModifierRequests = new List<int>();
    private int randomCalls;

    [SetUp]
    public void SetUp()
    {
        player = new PlayerState(100, 10, PlayerStateServices.Simulation,
            new RunDifficultySaveData { upgradeIds = Array.Empty<string>() });
        magicById.Clear();
        magicModifiersById.Clear();
        materialModifiersById.Clear();
        magicChoices.Clear();
        magicModifierChoices.Clear();
        materialModifierChoices.Clear();
        magicRequests.Clear();
        magicModifierRequests.Clear();
        materialModifierRequests.Clear();
        randomCalls = 0;
        // Replace every content/RNG delegate. No test requires a database, unlock table, or panel.
        services = new EventRuleServices(player)
        {
            MagicSlotCapacity = 3,
            NextRandomInt = (min, max) => { randomCalls++; return min; },
            FindMagic = id => magicById.TryGetValue(id, out MagicData data) ? data : null,
            FindMagicModifier = id => id != null && magicModifiersById.TryGetValue(id, out MagicModifierData data) ? data : null,
            FindMaterialModifier = id => id != null && materialModifiersById.TryGetValue(id, out MaterialModifierData data) ? data : null,
            MagicChoices = count => { magicRequests.Add(count); return magicChoices.ToArray(); },
            MagicModifierChoices = count => { magicModifierRequests.Add(count); return magicModifierChoices.ToArray(); },
            MaterialModifierChoices = count => { materialModifierRequests.Add(count); return materialModifierChoices.ToArray(); }
        };
    }

    private static EventData Definition(params EventEffectData[] effects)
        => new EventData
        {
            numericId = 82001,
            id = "choice_event",
            titleKey = "choice_event_title",
            startNodeId = "start",
            nodes = new[]
            {
                new EventNodeData
                {
                    id = "start",
                    textKeys = new[] { "start_text" },
                    options = new[]
                    {
                        new EventOptionData
                        {
                            id = "accept", recipe = "1", nextNodeId = "done",
                            effects = effects
                        }
                    }
                },
                new EventNodeData { id = "done", textKeys = new[] { "done_text" } }
            }
        };

    private EventModel Model(EventData definition) => new EventModel(definition, services.NextRandomInt);

    private EventResolutionSession Session(params EventEffectData[] effects)
    {
        EventModel model = Model(Definition(effects));
        return new EventResolutionSession(player, model, model.CurrentOptions[0], services);
    }

    private MagicData AddMagic(int id = 82011)
    {
        var data = new MagicData { numericId = id, id = "event_magic_" + id, recipe = new[] { MaterialEnum.Fire } };
        magicById.Add(id, data);
        magicChoices.Add(data);
        return data;
    }

    private MagicModifierData AddMagicModifier(string id = "event_generic_modifier")
    {
        var data = new MagicModifierData { id = id, targetRule = MagicModifierTargetRule.Any };
        magicModifiersById.Add(id, data);
        magicModifierChoices.Add(data);
        return data;
    }

    private MaterialModifierData AddMaterialModifier(string id = "event_kindling", string script = nameof(KindlingModifier))
    {
        var data = new MaterialModifierData { id = id, script = script };
        materialModifiersById.Add(id, data);
        materialModifierChoices.Add(data);
        return data;
    }

    private MaterialModel PutInHand(MaterialEnum material = MaterialEnum.Fire)
    {
        MaterialModel card = player.AddDeckMaterial(material);
        player.DrawPile.Remove(card);
        player.Hand.Add(card);
        return card;
    }

    private static ChoiceResult Select(IChoiceSession session, int index = 0)
    {
        PendingChoice pending = session.PendingChoice;
        Assert.That(pending, Is.Not.Null);
        return session.SubmitChoice(new ChoiceSubmission(pending.Id, pending.Candidates[index].Id));
    }

    private sealed class DiscardObserver : MaterialModifierModel
    {
        private readonly Action observe;
        public DiscardObserver(Action observe) { this.observe = observe; }
        public override void OnDiscard() => observe();
    }

    [Test]
    public void FixedMagicPackFillsSparseSlotsWithoutOverwritingExistingMagic()
    {
        services.MagicSlotCapacity = 4;
        MagicData first = AddMagic(82021);
        MagicData second = AddMagic(82022);
        MagicData extra = AddMagic(82023);
        var existing = new MagicModel(AddMagic(82024), 1);
        var last = new MagicModel(AddMagic(82025), 3);
        player.SetMagicAtSlot(existing, 1);
        player.SetMagicAtSlot(last, 3);
        EventResolutionSession session = Session(new EventEffectData
        {
            rewardType = EventRewardType.GainMagicById,
            magicIds = new[] { first.numericId, second.numericId, extra.numericId }
        });

        while (!session.IsComplete) session.Advance();

        Assert.That(player.GetMagicAtSlot(0)?.Data, Is.SameAs(first));
        Assert.That(player.GetMagicAtSlot(1), Is.SameAs(existing));
        Assert.That(player.GetMagicAtSlot(2)?.Data, Is.SameAs(second));
        Assert.That(player.GetMagicAtSlot(3), Is.SameAs(last));
    }

    [Test]
    public void EventDataIsDeepClonedAndRestoringOneModelNeverChangesSourceOrSibling()
    {
        EventData source = Definition(new EventEffectData
        {
            rewardType = EventRewardType.GainMagicById, amount = 4, count = 2, choiceCount = 1,
            escalatePerUse = 3, material = MaterialEnum.Water, modifierId = "source_modifier",
            percent = 50, magicIds = new[] { 82011, 82012 }
        });
        source.nodes[0].options[0].tagIds = new[] { "original_tag" };
        EventModel first = Model(source);
        EventModel sibling = Model(source);
        EventOptionData option = first.CurrentOptions[0];
        EventEffectData effect = option.effects[0];

        Assert.That(first.Data, Is.Not.SameAs(source));
        Assert.That(first.Data.nodes, Is.Not.SameAs(source.nodes));
        Assert.That(first.CurrentNode, Is.Not.SameAs(source.nodes[0]));
        Assert.That(first.CurrentNode.textKeys, Is.Not.SameAs(source.nodes[0].textKeys));
        Assert.That(first.CurrentNode.options, Is.Not.SameAs(source.nodes[0].options));
        Assert.That(option, Is.Not.SameAs(source.nodes[0].options[0]));
        Assert.That(option.tagIds, Is.Not.SameAs(source.nodes[0].options[0].tagIds));
        Assert.That(option.effects, Is.Not.SameAs(source.nodes[0].options[0].effects));
        Assert.That(effect, Is.Not.SameAs(source.nodes[0].options[0].effects[0]));
        Assert.That(effect.magicIds, Is.Not.SameAs(source.nodes[0].options[0].effects[0].magicIds));
        Assert.That(effect.amount, Is.EqualTo(4));
        Assert.That(effect.count, Is.EqualTo(2));
        Assert.That(effect.choiceCount, Is.EqualTo(1));
        Assert.That(effect.escalatePerUse, Is.EqualTo(3));
        Assert.That(effect.material, Is.EqualTo(MaterialEnum.Water));
        Assert.That(effect.modifierId, Is.EqualTo("source_modifier"));
        Assert.That(effect.percent, Is.EqualTo(50));

        first.CurrentNode.textKeys[0] = "changed_text";
        option.tagIds[0] = "changed_tag";
        effect.magicIds[0] = -1;
        effect.amount = 99;
        first.MarkOptionResolved(option);
        first.RestoreSaveData(new EventNodeSaveData
        {
            currentNodeId = "done",
            optionRecipes = new[] { new EventOptionRecipeSaveData { nodeId = "start", optionId = "accept", recipe = "4321" } },
            optionResolveCounts = new[] { new EventOptionResolveCountSaveData { optionId = "accept", count = 7 } }
        });

        Assert.That(first.CurrentNode.id, Is.EqualTo("done"));
        Assert.That(option.recipe, Is.EqualTo("4321"));
        Assert.That(first.GetOptionResolveCount(option), Is.EqualTo(7));
        Assert.That(source.nodes[0].textKeys[0], Is.EqualTo("start_text"));
        Assert.That(source.nodes[0].options[0].recipe, Is.EqualTo("1"));
        Assert.That(source.nodes[0].options[0].tagIds[0], Is.EqualTo("original_tag"));
        Assert.That(source.nodes[0].options[0].effects[0].amount, Is.EqualTo(4));
        Assert.That(source.nodes[0].options[0].effects[0].magicIds, Is.EqualTo(new[] { 82011, 82012 }));
        Assert.That(sibling.CurrentNode.id, Is.EqualTo("start"));
        Assert.That(sibling.CurrentTexts[0], Is.EqualTo("start_text"));
        Assert.That(sibling.CurrentOptions[0].recipe, Is.EqualTo("1"));
        Assert.That(sibling.CurrentOptions[0].effects[0].magicIds[0], Is.EqualTo(82011));
        Assert.That(sibling.GetOptionResolveCount(sibling.CurrentOptions[0]), Is.Zero);
    }

    [Test]
    public void RandomRecipeGenerationUsesInjectedRandomAndNeverWritesSharedDefinition()
    {
        EventData source = Definition();
        EventOptionData sourceOption = source.nodes[0].options[0];
        sourceOption.recipe = "shared_source_recipe";
        sourceOption.randomRecipeLength = 1;
        int firstCalls = 0;
        int secondCalls = 0;
        var first = new EventModel(source, (min, max) => { firstCalls++; return min; });
        var second = new EventModel(source, (min, max) => { secondCalls++; return max - 1; });

        Assert.That(firstCalls, Is.EqualTo(1));
        Assert.That(secondCalls, Is.EqualTo(1));
        Assert.That(first.CurrentOptions[0].recipe, Is.EqualTo("1"));
        Assert.That(second.CurrentOptions[0].recipe, Is.EqualTo("4"));
        Assert.That(sourceOption.recipe, Is.EqualTo("shared_source_recipe"));
        first.CurrentOptions[0].recipe = "changed_in_first_run";
        Assert.That(second.CurrentOptions[0].recipe, Is.EqualTo("4"));
        Assert.That(sourceOption.recipe, Is.EqualTo("shared_source_recipe"));
    }

    [Test]
    public void ImmediateEffectsThenForcedDiscardThenCountThenDeferredChoiceKeepSourceOrder()
    {
        AddMagic();
        EventResolutionSession session = Session(
            new EventEffectData { rewardType = EventRewardType.LoseHealth, amount = 3 },
            new EventEffectData { rewardType = EventRewardType.GainMagic },
            new EventEffectData { rewardType = EventRewardType.Heal, amount = 1 });
        EventOptionData option = session.Option;
        MaterialModel hand = PutInHand();
        MaterialModel temporary = player.AddTemporaryMaterialToHand(MaterialEnum.Wind);
        MaterialModel played = player.AddDeckMaterial(MaterialEnum.Water);
        player.DrawPile.Remove(played);
        player.PlayZone.Add(played);
        played.isPlayed = true;
        int discards = 0;
        hand.AddModifier(new RetainedArrowModifier());
        hand.AddModifier(new DiscardObserver(() =>
        {
            discards++;
            Assert.That(player.CurrentHealth, Is.EqualTo(98), "Both immediate effects must precede discard hooks.");
            Assert.That(session.Event.GetOptionResolveCount(option), Is.Zero, "Count increments after forced discard.");
            Assert.That(magicRequests, Is.Empty, "Deferred candidates must not be rolled before discard.");
        }));
        player.KeepHandOnEndTurnOnce();
        services.MagicChoices = count =>
        {
            magicRequests.Add(count);
            Assert.That(player.Hand, Is.Empty);
            Assert.That(player.PlayZone, Is.Empty);
            Assert.That(player.DiscardPile, Does.Contain(hand));
            Assert.That(session.Event.GetOptionResolveCount(option), Is.EqualTo(1));
            return magicChoices.ToArray();
        };

        EventResolutionStep lose = session.Advance();
        Assert.That(lose.Kind, Is.EqualTo(EventResolutionStepKind.Effect));
        Assert.That(lose.Effect, Is.SameAs(option.effects[0]));
        Assert.That(lose.Result.HealthBefore, Is.EqualTo(100));
        Assert.That(lose.Result.HealthAfter, Is.EqualTo(97));
        Assert.That(player.Hand, Does.Contain(hand));
        EventResolutionStep heal = session.Advance();
        Assert.That(heal.Effect, Is.SameAs(option.effects[2]));
        Assert.That(player.CurrentHealth, Is.EqualTo(98));
        Assert.That(session.Event.GetOptionResolveCount(option), Is.Zero);

        EventResolutionStep discard = session.Advance();
        Assert.That(discard.Kind, Is.EqualTo(EventResolutionStepKind.Discard));
        Assert.That(discard.RemovedTemporaryCards, Is.EqualTo(new[] { temporary }));
        Assert.That(player.ConsumedPile, Does.Contain(temporary));
        Assert.That(player.DiscardPile, Does.Contain(played));
        Assert.That(player.Hand, Is.Empty, "Forced discard ignores retained/keep-hand settings.");
        Assert.That(player.PlayZone, Is.Empty);
        Assert.That(discards, Is.EqualTo(1));
        Assert.That(session.Event.GetOptionResolveCount(option), Is.Zero);

        Assert.That(session.Advance().Kind, Is.EqualTo(EventResolutionStepKind.Choice));
        Assert.That(magicRequests, Is.EqualTo(new[] { 3 }));
        PendingChoice pending = session.PendingChoice;
        Assert.That(session.Event.GetOptionResolveCount(option), Is.EqualTo(1));
        Assert.That(session.Event.CurrentNode.id, Is.EqualTo("start"));
        session.RunUntilChoiceOrComplete();
        session.Advance();
        Assert.That(session.PendingChoice, Is.SameAs(pending));
        Assert.That(session.Event.GetOptionResolveCount(option), Is.EqualTo(1));
        Assert.That(discards, Is.EqualTo(1));
        Assert.That(player.MagicBook, Is.Empty);
        Assert.That(Select(session).Accepted, Is.True);
        Assert.That(session.Advance().Kind, Is.EqualTo(EventResolutionStepKind.Complete));
        Assert.That(session.NodeAdvanced, Is.True);
        Assert.That(session.Event.CurrentNode.id, Is.EqualTo("done"));
        Assert.That(session.Advance(), Is.Null);
        Assert.That(session.Event.GetOptionResolveCount(option), Is.EqualTo(1));
    }

    [Test]
    public void RepeatedOptionEscalatesDamageFromPriorResolveCountOnly()
    {
        EventModel model = Model(Definition(new EventEffectData
        {
            rewardType = EventRewardType.LoseHealth, amount = 4, escalatePerUse = 3
        }));
        EventOptionData option = model.CurrentOptions[0];
        option.nextNodeId = null;
        for (int use = 0; use < 3; use++)
        {
            var session = new EventResolutionSession(player, model, option, services);
            int before = player.CurrentHealth;
            EventResolutionStep effect = session.Advance();
            Assert.That(before - player.CurrentHealth, Is.EqualTo(4 + use * 3));
            Assert.That(effect.Result.HealthBefore, Is.EqualTo(before));
            Assert.That(effect.Result.HealthAfter, Is.EqualTo(player.CurrentHealth));
            Assert.That(model.GetOptionResolveCount(option), Is.EqualTo(use));
            session.RunUntilChoiceOrComplete();
            Assert.That(model.GetOptionResolveCount(option), Is.EqualTo(use + 1));
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.Advance(), Is.Null);
            Assert.That(model.GetOptionResolveCount(option), Is.EqualTo(use + 1));
        }
        Assert.That(player.CurrentHealth, Is.EqualTo(79));
    }

    [Test]
    public void MultipleDeferredChoicesResumeInOrderAndRejectAnswersFromPreviousChoices()
    {
        MagicData magic = AddMagic();
        MagicModifierData modifier = AddMagicModifier();
        MaterialModifierData arrowModifier = AddMaterialModifier();
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Water);
        EventResolutionSession session = Session(
            new EventEffectData { rewardType = EventRewardType.GainMagic },
            new EventEffectData { rewardType = EventRewardType.GainMagicModifier, modifierId = modifier.id },
            new EventEffectData { rewardType = EventRewardType.GainMaterialModifier, modifierId = arrowModifier.id });

        session.RunUntilChoiceOrComplete();
        PendingChoice reward = session.PendingChoice;
        var rewardAnswer = new ChoiceSubmission(reward.Id, reward.Candidates[0].Id);
        Assert.That(session.SubmitChoice(rewardAnswer).Accepted, Is.True);
        Assert.That(player.MagicBook.Single().Data, Is.SameAs(magic));
        Assert.That(session.IsComplete, Is.False);
        session.RunUntilChoiceOrComplete();
        PendingChoice magicModifier = session.PendingChoice;
        Assert.That(magicModifier.Kind, Is.EqualTo(ChoiceKind.MagicModifier));
        Assert.That(session.SubmitChoice(rewardAnswer).Failure, Is.EqualTo(ChoiceFailure.StaleChoice));
        Assert.That(Select(session).Accepted, Is.True);
        PendingChoice magicTarget = session.PendingChoice;
        Assert.That(magicTarget.Kind, Is.EqualTo(ChoiceKind.MagicTarget));
        Assert.That(Select(session).Accepted, Is.True);
        Assert.That(player.MagicBook.Single().PrimaryModifier.Data, Is.SameAs(modifier));
        session.RunUntilChoiceOrComplete();
        PendingChoice materialModifier = session.PendingChoice;
        Assert.That(materialModifier.Kind, Is.EqualTo(ChoiceKind.MaterialModifier));
        Assert.That(Select(session).Accepted, Is.True);
        PendingChoice materialTarget = session.PendingChoice;
        Assert.That(materialTarget.Kind, Is.EqualTo(ChoiceKind.MaterialTarget));
        Assert.That(Select(session).Accepted, Is.True);
        Assert.That(card.modifiers.Single(), Is.TypeOf<KindlingModifier>());
        session.RunUntilChoiceOrComplete();

        Assert.That(new[] { reward.Id, magicModifier.Id, magicTarget.Id, materialModifier.Id, materialTarget.Id }.Distinct().Count(), Is.EqualTo(5));
        Assert.That(session.IsComplete, Is.True);
        Assert.That(session.NodeAdvanced, Is.True);
        Assert.That(session.Event.CurrentNode.id, Is.EqualTo("done"));
        Assert.That(session.Event.GetOptionResolveCount(session.Option), Is.EqualTo(1));
        Assert.That(session.SubmitChoice(rewardAnswer).Failure, Is.EqualTo(ChoiceFailure.NoPendingChoice));
        Assert.That(player.MagicBook.Count, Is.EqualTo(1));
        Assert.That(card.modifiers.Count, Is.EqualTo(1));
        Assert.That(randomCalls, Is.Zero);
    }

    [Test]
    public void NoPanelLeavesChoicePendingUntilScriptSubmitsStableIds()
    {
        AddMagic();
        AddMagic(82012);
        MaterialModel first = player.AddDeckMaterial(MaterialEnum.Fire);
        MaterialModel second = player.AddDeckMaterial(MaterialEnum.Wind);
        EventResolutionSession concrete = Session(
            new EventEffectData { rewardType = EventRewardType.RemoveMaterial },
            new EventEffectData { rewardType = EventRewardType.GainMagic });
        IChoiceSession scriptSession = concrete;
        concrete.RunUntilChoiceOrComplete();
        PendingChoice removal = scriptSession.PendingChoice;
        for (int i = 0; i < 3; i++)
        {
            concrete.RunUntilChoiceOrComplete();
            Assert.That(concrete.Advance().Kind, Is.EqualTo(EventResolutionStepKind.Choice));
            Assert.That(scriptSession.PendingChoice, Is.SameAs(removal));
            Assert.That(scriptSession.IsComplete, Is.False);
            Assert.That(player.Deck.Count, Is.EqualTo(2));
            Assert.That(magicRequests, Is.Empty);
        }
        Assert.That(scriptSession.SubmitChoice(new ChoiceSubmission(removal.Id,
            removal.Candidates.Single(candidate => ReferenceEquals(candidate.Material, first)).Id)).Accepted, Is.True);
        concrete.RunUntilChoiceOrComplete();
        PendingChoice reward = scriptSession.PendingChoice;
        Assert.That(reward.Kind, Is.EqualTo(ChoiceKind.MagicReward));
        Assert.That(player.Deck.Single(), Is.SameAs(second));
        Assert.That(player.MagicBook, Is.Empty);
        Assert.That(scriptSession.SubmitChoice(new ChoiceSubmission(reward.Id, reward.Candidates[1].Id)).Accepted, Is.True);
        concrete.RunUntilChoiceOrComplete();
        Assert.That(scriptSession.IsComplete, Is.True);
        Assert.That(player.MagicBook.Single().NumericId, Is.EqualTo(82012));
    }

    [TestCase(0, ChoiceFailure.InvalidCount)]
    [TestCase(1, ChoiceFailure.InvalidCount)]
    [TestCase(2, ChoiceFailure.InvalidCount)]
    [TestCase(3, ChoiceFailure.DuplicateCandidate)]
    [TestCase(4, ChoiceFailure.UnknownCandidate)]
    [TestCase(5, ChoiceFailure.StaleChoice)]
    [TestCase(6, ChoiceFailure.StaleChoice)]
    [TestCase(7, ChoiceFailure.CannotCancel)]
    public void InvalidRemovalAnswersDoNotChangeInventoryOrConsumePendingChoice(int answerKind, ChoiceFailure expected)
    {
        player.AddDeckMaterial(MaterialEnum.Fire);
        player.AddDeckMaterial(MaterialEnum.Wind);
        player.AddDeckMaterial(MaterialEnum.Water);
        EventResolutionSession session = Session(new EventEffectData { rewardType = EventRewardType.RemoveMaterial, choiceCount = 2 });
        session.RunUntilChoiceOrComplete();
        PendingChoice pending = session.PendingChoice;
        string a = pending.Candidates[0].Id;
        string b = pending.Candidates[1].Id;
        string c = pending.Candidates[2].Id;
        ChoiceSubmission submission;
        switch (answerKind)
        {
            case 0: submission = new ChoiceSubmission(pending.Id); break;
            case 1: submission = new ChoiceSubmission(pending.Id, a); break;
            case 2: submission = new ChoiceSubmission(pending.Id, a, b, c); break;
            case 3: submission = new ChoiceSubmission(pending.Id, a, a); break;
            case 4: submission = new ChoiceSubmission(pending.Id, a, "unknown_material"); break;
            case 5: submission = new ChoiceSubmission("expired_choice", a, b); break;
            case 6: submission = null; break;
            default: submission = ChoiceSubmission.CancelChoice(pending.Id); break;
        }
        Assert.That(pending.MinSelections, Is.EqualTo(2));
        Assert.That(pending.MaxSelections, Is.EqualTo(2));
        Assert.That(session.SubmitChoice(submission).Failure, Is.EqualTo(expected));
        Assert.That(session.PendingChoice, Is.SameAs(pending));
        Assert.That(player.Deck.Count, Is.EqualTo(3));
        Assert.That(player.DrawPile.Count, Is.EqualTo(3));
        Assert.That(player.Gold, Is.EqualTo(10));
        Assert.That(session.Event.GetOptionResolveCount(session.Option), Is.Zero);
        Assert.That(session.IsComplete, Is.False);
        Assert.That(session.SubmitChoice(new ChoiceSubmission(pending.Id, a, b)).Accepted, Is.True);
        Assert.That(player.Deck.Single(), Is.SameAs(pending.Candidates[2].Material));
        session.RunUntilChoiceOrComplete();
        Assert.That(session.IsComplete, Is.True);
        Assert.That(session.Event.GetOptionResolveCount(session.Option), Is.EqualTo(1));
    }

    [TestCase(0, ChoiceFailure.InvalidCount)]
    [TestCase(1, ChoiceFailure.None)]
    [TestCase(2, ChoiceFailure.None)]
    [TestCase(3, ChoiceFailure.InvalidCount)]
    public void SharedPendingChoiceEnforcesBothInclusiveSelectionBounds(int count, ChoiceFailure expected)
    {
        var candidates = new List<ChoiceCandidate>
        {
            new ChoiceCandidate("a"), new ChoiceCandidate("b"), new ChoiceCandidate("c")
        };
        var pending = new PendingChoice("bounds", ChoiceKind.RemoveMaterial, 1, 2, false, candidates);
        var submission = new ChoiceSubmission(pending.Id, candidates.Take(count).Select(candidate => candidate.Id).ToArray());
        Assert.That(pending.Validate(submission, out List<ChoiceCandidate> selected), Is.EqualTo(expected));
        if (expected == ChoiceFailure.None)
            for (int i = 0; i < count; i++) Assert.That(selected[i], Is.SameAs(candidates[i]));
    }

    [Test]
    public void ChoiceAndSubmissionSnapshotTheirListsAndRejectAmbiguousCandidateIds()
    {
        var first = new ChoiceCandidate("first");
        var second = new ChoiceCandidate("second");
        var candidates = new List<ChoiceCandidate> { first, second };
        var pending = new PendingChoice("immutable", ChoiceKind.RemoveMaterial, 1, 2, true, candidates);
        candidates.Clear();
        string[] submitted = { first.Id, second.Id };
        var answer = new ChoiceSubmission(pending.Id, submitted);
        submitted[0] = "changed_after_submission";
        Assert.That(pending.Validate(answer, out List<ChoiceCandidate> selected), Is.EqualTo(ChoiceFailure.None));
        Assert.That(selected[0], Is.SameAs(first));
        Assert.That(selected[1], Is.SameAs(second));
        Assert.That(pending.Validate(ChoiceSubmission.CancelChoice(pending.Id), out _), Is.EqualTo(ChoiceFailure.None));
        Assert.That(pending.Validate(new ChoiceSubmission(pending.Id, true, first.Id), out _), Is.EqualTo(ChoiceFailure.CannotCancel));
        Assert.That(pending.Find("missing"), Is.Null);
        Assert.Throws<ArgumentException>(() => new PendingChoice("duplicate", ChoiceKind.RemoveMaterial, 1, 1, false,
            new List<ChoiceCandidate> { first, new ChoiceCandidate(first.Id) }));
        Assert.Throws<ArgumentException>(() => new PendingChoice("null_candidate", ChoiceKind.RemoveMaterial, 1, 1, false,
            new List<ChoiceCandidate> { null }));
        Assert.Throws<ArgumentException>(() => new PendingChoice("bad_bounds", ChoiceKind.RemoveMaterial, 2, 1, false,
            new List<ChoiceCandidate> { first, second }));
    }

    [Test]
    public void ForcedRemovalCannotCancelAndRemovesOnlyRealDeckCardFromEveryPile()
    {
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Fire);
        MaterialModel placeholder = player.AddDeckPlaceholderMaterial();
        MaterialModel temporary = player.AddTemporaryMaterialToHand(MaterialEnum.Water);
        EventResolutionSession session = Session(new EventEffectData { rewardType = EventRewardType.RemoveMaterial, choiceCount = 9 });
        session.RunUntilChoiceOrComplete();
        PendingChoice pending = session.PendingChoice;
        Assert.That(pending.Kind, Is.EqualTo(ChoiceKind.RemoveMaterial));
        Assert.That(pending.CanCancel, Is.False);
        Assert.That(pending.MinSelections, Is.EqualTo(1));
        Assert.That(pending.MaxSelections, Is.EqualTo(1), "Selection count is bounded by real removable candidates.");
        Assert.That(pending.Candidates.Single().Material, Is.SameAs(card));
        Assert.That(session.SubmitChoice(ChoiceSubmission.CancelChoice(pending.Id)).Failure, Is.EqualTo(ChoiceFailure.CannotCancel));
        Assert.That(session.PendingChoice, Is.SameAs(pending));
        Assert.That(player.Hand, Does.Contain(temporary), "An immediate forced choice pauses before discard.");
        player.DiscardPile.Add(card);
        player.PlayZone.Add(card);
        player.ConsumedPile.Add(card);
        player.TemporaryMaterialsNextTurn.Add(card);
        player.Hand.Add(card);
        var submission = new ChoiceSubmission(pending.Id, pending.Candidates[0].Id);
        Assert.That(session.SubmitChoice(submission).Accepted, Is.True);
        Assert.That(player.Deck, Is.EqualTo(new[] { placeholder }));
        Assert.That(player.DrawPile, Has.No.Member(card));
        Assert.That(player.Hand, Has.No.Member(card));
        Assert.That(player.DiscardPile, Has.No.Member(card));
        Assert.That(player.PlayZone, Has.No.Member(card));
        Assert.That(player.ConsumedPile, Has.No.Member(card));
        Assert.That(player.TemporaryMaterialsNextTurn, Has.No.Member(card));
        Assert.That(session.SubmitChoice(submission).Failure, Is.EqualTo(ChoiceFailure.NoPendingChoice));
        session.RunUntilChoiceOrComplete();
        Assert.That(player.Hand, Is.Empty);
        Assert.That(player.PaidShopArrowRemovalCount, Is.Zero);
    }

    [Test]
    public void UnavailableRemovalTargetRejectsWholeMultiSelectionBeforeRemovingAnyOtherCard()
    {
        MaterialModel available = player.AddDeckMaterial(MaterialEnum.Fire);
        MaterialModel removed = player.AddDeckMaterial(MaterialEnum.Wind);
        EventResolutionSession session = Session(new EventEffectData { rewardType = EventRewardType.RemoveMaterial, choiceCount = 2 });
        session.RunUntilChoiceOrComplete();
        PendingChoice pending = session.PendingChoice;
        player.RemoveCardEverywhere(removed);
        var submission = new ChoiceSubmission(pending.Id, pending.Candidates.Select(candidate => candidate.Id).ToArray());
        Assert.That(session.SubmitChoice(submission).Failure, Is.EqualTo(ChoiceFailure.TargetUnavailable));
        Assert.That(session.PendingChoice, Is.SameAs(pending));
        Assert.That(player.Deck.Single(), Is.SameAs(available));
        Assert.That(player.DrawPile.Single(), Is.SameAs(available));
        Assert.That(session.IsComplete, Is.False);
    }

    [Test]
    public void MagicTargetCancelReturnsExactOriginalModifierCandidatesWithoutRerolling()
    {
        MagicModel target = MagicFactory.Create(AddMagic(), 0);
        player.SetMagicAtSlot(target, 0);
        AddMagicModifier("modifier_one");
        MagicModifierData selectedData = AddMagicModifier("modifier_two");
        EventResolutionSession session = Session(new EventEffectData { rewardType = EventRewardType.GainMagicModifier, choiceCount = 2 });
        session.RunUntilChoiceOrComplete();
        PendingChoice choices = session.PendingChoice;
        ChoiceCandidate[] original = choices.Candidates.ToArray();
        Assert.That(magicModifierRequests, Is.EqualTo(new[] { 2 }));
        Assert.That(Select(session, 1).Accepted, Is.True);
        PendingChoice targetChoice = session.PendingChoice;
        Assert.That(targetChoice.Kind, Is.EqualTo(ChoiceKind.MagicTarget));
        Assert.That(target.Modifiers, Is.Empty, "Choosing the modifier alone must not apply it.");
        ChoiceResult back = session.SubmitChoice(ChoiceSubmission.CancelChoice(targetChoice.Id));
        Assert.That(back.Accepted, Is.True);
        Assert.That(back.Cancelled, Is.True);
        Assert.That(back.GoldBefore, Is.EqualTo(10));
        Assert.That(back.GoldAfter, Is.EqualTo(10));
        Assert.That(back.HealthBefore, Is.EqualTo(100));
        Assert.That(back.HealthAfter, Is.EqualTo(100));
        Assert.That(session.PendingChoice, Is.SameAs(choices));
        Assert.That(session.PendingChoice.Candidates, Is.EqualTo(original));
        for (int i = 0; i < original.Length; i++) Assert.That(session.PendingChoice.Candidates[i], Is.SameAs(original[i]));
        magicModifierChoices.Clear();
        session.RunUntilChoiceOrComplete();
        Assert.That(magicModifierRequests, Is.EqualTo(new[] { 2 }));
        Assert.That(Select(session, 1).Accepted, Is.True);
        PendingChoice retryTarget = session.PendingChoice;
        Assert.That(retryTarget.Id, Is.Not.EqualTo(targetChoice.Id));
        Assert.That(session.SubmitChoice(new ChoiceSubmission(targetChoice.Id, targetChoice.Candidates[0].Id)).Failure, Is.EqualTo(ChoiceFailure.StaleChoice));
        var answer = new ChoiceSubmission(retryTarget.Id, retryTarget.Candidates[0].Id);
        Assert.That(session.SubmitChoice(answer).Accepted, Is.True);
        Assert.That(target.Modifiers.Single().Data, Is.SameAs(selectedData));
        Assert.That(session.SubmitChoice(answer).Failure, Is.EqualTo(ChoiceFailure.NoPendingChoice));
        session.RunUntilChoiceOrComplete();
        Assert.That(session.IsComplete, Is.True);
        Assert.That(magicModifierRequests.Count, Is.EqualTo(1));
        Assert.That(randomCalls, Is.Zero);
    }

    [Test]
    public void MaterialTargetCancelReturnsOriginalRestCandidatesAndDoesNotApplyOrReroll()
    {
        MaterialModel target = player.AddDeckMaterial(MaterialEnum.Fire);
        AddMaterialModifier();
        AddMaterialModifier("event_charge", nameof(ChargeModifier));
        EventModel model = Model(Definition());
        EventOptionData option = model.CurrentOptions[0];
        option.resultId = 302;
        var session = new EventResolutionSession(player, model, option, services, isRest: true);
        session.RunUntilChoiceOrComplete();
        PendingChoice choices = session.PendingChoice;
        ChoiceCandidate[] original = choices.Candidates.ToArray();
        Assert.That(choices.Kind, Is.EqualTo(ChoiceKind.MaterialModifier));
        Assert.That(materialModifierRequests, Is.EqualTo(new[] { 2 }));
        Assert.That(Select(session).Accepted, Is.True);
        PendingChoice targetChoice = session.PendingChoice;
        Assert.That(targetChoice.Kind, Is.EqualTo(ChoiceKind.MaterialTarget));
        Assert.That(target.modifiers, Is.Empty);
        ChoiceResult back = session.SubmitChoice(ChoiceSubmission.CancelChoice(targetChoice.Id));
        Assert.That(back.Accepted, Is.True);
        Assert.That(back.Cancelled, Is.True);
        Assert.That(back.GoldBefore, Is.EqualTo(10));
        Assert.That(back.GoldAfter, Is.EqualTo(10));
        Assert.That(back.HealthBefore, Is.EqualTo(100));
        Assert.That(back.HealthAfter, Is.EqualTo(100));
        Assert.That(session.PendingChoice, Is.SameAs(choices));
        for (int i = 0; i < original.Length; i++) Assert.That(session.PendingChoice.Candidates[i], Is.SameAs(original[i]));
        materialModifierChoices.Clear();
        Assert.That(Select(session).Accepted, Is.True);
        Assert.That(session.SubmitChoice(new ChoiceSubmission(targetChoice.Id, targetChoice.Candidates[0].Id)).Failure, Is.EqualTo(ChoiceFailure.StaleChoice));
        Assert.That(Select(session).Accepted, Is.True);
        Assert.That(target.modifiers.Single(), Is.TypeOf<KindlingModifier>());
        session.RunUntilChoiceOrComplete();
        Assert.That(session.IsComplete, Is.True);
        Assert.That(materialModifierRequests, Is.EqualTo(new[] { 2 }));
        Assert.That(randomCalls, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CancellingModifierSelectionSkipsOnlyThatEffectAndContinuesNextChoice(bool material)
    {
        player.SetMagicAtSlot(MagicFactory.Create(AddMagic(), 0), 0);
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Fire);
        MagicModifierData magicModifier = AddMagicModifier();
        MaterialModifierData materialModifier = AddMaterialModifier();
        EventResolutionSession session = Session(
            new EventEffectData
            {
                rewardType = material ? EventRewardType.GainMaterialModifier : EventRewardType.GainMagicModifier,
                modifierId = material ? materialModifier.id : magicModifier.id
            },
            new EventEffectData { rewardType = EventRewardType.GainMagic });
        session.RunUntilChoiceOrComplete();
        PendingChoice skipped = session.PendingChoice;
        ChoiceResult result = session.SubmitChoice(ChoiceSubmission.CancelChoice(skipped.Id));
        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Cancelled, Is.True);
        Assert.That(player.MagicBook[0].Modifiers, Is.Empty);
        Assert.That(card.modifiers, Is.Empty);
        Assert.That(session.IsComplete, Is.False);
        session.RunUntilChoiceOrComplete();
        Assert.That(session.PendingChoice.Kind, Is.EqualTo(ChoiceKind.MagicReward));
        Assert.That(session.SubmitChoice(ChoiceSubmission.CancelChoice(skipped.Id)).Failure, Is.EqualTo(ChoiceFailure.StaleChoice));
        Assert.That(Select(session).Accepted, Is.True);
        session.RunUntilChoiceOrComplete();
        Assert.That(session.IsComplete, Is.True);
        Assert.That(player.MagicBook.Count, Is.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ModifierTargetRemovedAfterOpeningIsRejectedWithoutApplyingToDetachedEntity(bool material)
    {
        MagicModel magic = MagicFactory.Create(AddMagic(), 0);
        player.SetMagicAtSlot(magic, 0);
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Fire);
        MagicModifierData magicModifier = AddMagicModifier();
        MaterialModifierData materialModifier = AddMaterialModifier();
        EventResolutionSession session = Session(new EventEffectData
        {
            rewardType = material ? EventRewardType.GainMaterialModifier : EventRewardType.GainMagicModifier,
            modifierId = material ? materialModifier.id : magicModifier.id
        });
        session.RunUntilChoiceOrComplete();
        Assert.That(Select(session).Accepted, Is.True);
        PendingChoice targets = session.PendingChoice;
        if (material) player.RemoveCardEverywhere(card);
        else player.ClearMagicSlot(0);
        Assert.That(Select(session).Failure, Is.EqualTo(ChoiceFailure.TargetUnavailable));
        Assert.That(session.PendingChoice, Is.SameAs(targets));
        Assert.That(session.IsComplete, Is.False);
        Assert.That(card.modifiers, Is.Empty);
        Assert.That(magic.Modifiers, Is.Empty);
    }

    [Test]
    public void FullEventMagicRewardRetainsSameGeneratedCandidatesUntilSlotIsFreed()
    {
        services.MagicSlotCapacity = 1;
        MagicModel existing = MagicFactory.Create(AddMagic(82012), 0);
        MagicData offered = AddMagic();
        magicChoices.Clear();
        magicChoices.Add(offered);
        player.SetMagicAtSlot(existing, 0);
        EventResolutionSession session = Session(new EventEffectData { rewardType = EventRewardType.GainMagic });
        session.RunUntilChoiceOrComplete();
        PendingChoice pending = session.PendingChoice;
        var answer = new ChoiceSubmission(pending.Id, pending.Candidates[0].Id);
        Assert.That(session.SubmitChoice(answer).Failure, Is.EqualTo(ChoiceFailure.NoMagicSlot));
        session.RunUntilChoiceOrComplete();
        Assert.That(session.PendingChoice, Is.SameAs(pending));
        Assert.That(magicRequests, Is.EqualTo(new[] { 3 }));
        Assert.That(session.IsComplete, Is.False);
        Assert.That(player.GetMagicAtSlot(0), Is.SameAs(existing));
        player.ClearMagicSlot(0);
        Assert.That(session.SubmitChoice(answer).Accepted, Is.True);
        session.RunUntilChoiceOrComplete();
        Assert.That(player.GetMagicAtSlot(0).Data, Is.SameAs(offered));
        Assert.That(session.IsComplete, Is.True);
        Assert.That(magicRequests.Count, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PreviousEventSessionAnswerCannotSubmitToSameModelOptionAgain(bool magicReward)
    {
        AddMagic();
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Fire);
        EventModel model = Model(Definition(new EventEffectData
        {
            rewardType = magicReward ? EventRewardType.GainMagic : EventRewardType.RemoveMaterial
        }));
        EventOptionData option = model.CurrentOptions[0];
        var previous = new EventResolutionSession(player, model, option, services);
        previous.RunUntilChoiceOrComplete();
        PendingChoice oldChoice = previous.PendingChoice;
        var oldAnswer = new ChoiceSubmission(oldChoice.Id, oldChoice.Candidates[0].Id);
        var next = new EventResolutionSession(player, model, option, services);
        next.RunUntilChoiceOrComplete();
        PendingChoice pending = next.PendingChoice;
        Assert.That(pending.Id, Is.Not.EqualTo(oldChoice.Id));
        Assert.That(next.SubmitChoice(oldAnswer).Failure, Is.EqualTo(ChoiceFailure.StaleChoice));
        Assert.That(next.PendingChoice, Is.SameAs(pending));
        Assert.That(next.IsComplete, Is.False);
        Assert.That(player.Deck.Single(), Is.SameAs(card));
        Assert.That(player.MagicBook, Is.Empty);
        Assert.That(Select(next).Accepted, Is.True);
        next.RunUntilChoiceOrComplete();
        Assert.That(next.IsComplete, Is.True);
    }

    [Test]
    public void RandomAndSpecifiedMaterialEffectsUseInjectedContentAndRandomOnly()
    {
        MaterialModifierData modifier = AddMaterialModifier();
        var rolls = new Queue<int>(new[] { (int)MaterialEnum.Wind, (int)MaterialEnum.Water, (int)MaterialEnum.Earth });
        services.NextRandomInt = (min, max) =>
        {
            randomCalls++;
            Assert.That(min, Is.EqualTo((int)MaterialEnum.Fire));
            Assert.That(max, Is.EqualTo((int)MaterialEnum.Earth + 1));
            return rolls.Dequeue();
        };
        EventResolutionSession session = Session(
            new EventEffectData { rewardType = EventRewardType.GainRandomMaterial, count = 2 },
            new EventEffectData { rewardType = EventRewardType.GainSameRandomMaterials, count = 3 },
            new EventEffectData { rewardType = EventRewardType.GainMaterial, material = MaterialEnum.Fire, count = 2, modifierId = modifier.id });
        session.RunUntilChoiceOrComplete();
        Assert.That(session.IsComplete, Is.True);
        Assert.That(randomCalls, Is.EqualTo(3));
        Assert.That(rolls, Is.Empty);
        Assert.That(player.Deck.Select(card => card.material), Is.EqualTo(new[]
        {
            MaterialEnum.Wind, MaterialEnum.Water, MaterialEnum.Earth, MaterialEnum.Earth,
            MaterialEnum.Earth, MaterialEnum.Fire, MaterialEnum.Fire
        }));
        MaterialModel[] enchanted = player.Deck.Skip(5).ToArray();
        Assert.That(enchanted.All(card => card.modifiers.Count == 1 && card.modifiers[0] is KindlingModifier), Is.True);
        Assert.That(enchanted[0].modifiers[0], Is.Not.SameAs(enchanted[1].modifiers[0]));
        Assert.That(magicRequests, Is.Empty);
        Assert.That(magicModifierRequests, Is.Empty);
        Assert.That(materialModifierRequests, Is.Empty);
    }

    [Test]
    public void RestDefinitionKeepsDistinctRecipeRollOrderAndConfiguredTexts()
    {
        var rolls = new Queue<int>(new[] { 1, 1, 4 });
        EventModel model = EventRestRules.CreateModel(new LevelData
        {
            id = "rest_fixture", titleKey = "rest_title", restTextKeys = new[] { "intro", "healed" }
        }, 6, (min, max) =>
        {
            Assert.That(min, Is.EqualTo(1)); Assert.That(max, Is.EqualTo(5));
            return rolls.Dequeue();
        });
        Assert.That(rolls, Is.Empty);
        Assert.That(model.Data.drawCount, Is.EqualTo(6));
        Assert.That(model.CurrentTexts, Is.EqualTo(new[] { "intro" }));
        Assert.That(model.CurrentOptions.Select(option => option.resultId), Is.EqualTo(new[] { 300, 301, 302 }));
        Assert.That(model.CurrentOptions.Select(option => option.recipe), Is.EqualTo(new string[] { null, "1", "4" }));
        Assert.That(model.CurrentOptions[0].isExitOption, Is.True);
        Assert.That(model.AdvanceToNextNode(model.CurrentOptions[0]), Is.True);
        Assert.That(model.CurrentTexts, Is.EqualTo(new[] { "healed" }));
    }

    [Test]
    public void RestHealUsesCeilingThirtyPercentAfterDiscardAndAdvancesWithoutCountingOption()
    {
        player = new PlayerState(101, 10, PlayerStateServices.Simulation,
            new RunDifficultySaveData { upgradeIds = Array.Empty<string>() });
        player.TakeDirectDamage(61);
        MaterialModel hand = PutInHand();
        EventModel model = EventRestRules.CreateModel(null, 5, (min, max) => randomCalls++ == 0 ? 1 : 2);
        EventOptionData option = model.CurrentOptions[0];
        var session = new EventResolutionSession(player, model, option, services, isRest: true);
        Assert.That(session.Advance().Kind, Is.EqualTo(EventResolutionStepKind.Discard));
        Assert.That(player.Hand, Is.Empty);
        Assert.That(player.DiscardPile, Does.Contain(hand));
        Assert.That(player.CurrentHealth, Is.EqualTo(40));
        EventResolutionStep heal = session.Advance();
        Assert.That(heal.Result.HealthBefore, Is.EqualTo(40));
        Assert.That(heal.Result.HealthAfter, Is.EqualTo(71));
        session.RunUntilChoiceOrComplete();
        Assert.That(session.IsComplete, Is.True);
        Assert.That(session.NodeAdvanced, Is.True);
        Assert.That(model.CurrentNode.id, Is.EqualTo("rest_result"));
        Assert.That(model.GetOptionResolveCount(option), Is.Zero);
    }

    [Test]
    public void LegacyHandChoiceCapturesIdentityBeforeDiscardAndScriptCanAnswerIt()
    {
        MaterialModel inHand = PutInHand();
        MaterialModel deckOnly = player.AddDeckMaterial(MaterialEnum.Wind);
        EventModel model = Model(Definition());
        EventOptionData option = model.CurrentOptions[0];
        option.resultId = 100;
        var session = new EventResolutionSession(player, model, option, services);
        session.RunUntilChoiceOrComplete();
        PendingChoice pending = session.PendingChoice;
        Assert.That(player.Hand, Is.Empty);
        Assert.That(player.DiscardPile, Does.Contain(inHand));
        Assert.That(pending.Kind, Is.EqualTo(ChoiceKind.LegacyMaterial));
        Assert.That(pending.Candidates.Single().Material, Is.SameAs(inHand));
        Assert.That(session.SubmitChoice(ChoiceSubmission.CancelChoice(pending.Id)).Failure, Is.EqualTo(ChoiceFailure.CannotCancel));
        Assert.That(Select(session).Accepted, Is.True);
        Assert.That(player.ConsumedPile, Does.Contain(inHand));
        Assert.That(player.DiscardPile, Has.No.Member(inHand));
        Assert.That(player.Deck, Does.Contain(inHand), "Legacy consume is battle-only, not permanent removal.");
        Assert.That(player.DrawPile, Does.Contain(deckOnly));
        session.RunUntilChoiceOrComplete();
        Assert.That(session.IsComplete, Is.True);
    }
}
