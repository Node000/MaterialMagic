using System.Collections.Generic;
using NUnit.Framework;

public class MagicReadingOrderTests
{
    [Test]
    public void SlotPolicySurvivesReorderAndEmptySlots()
    {
        var player = new PlayerState(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData());
        var first = new MagicModel(new MagicData { id = "first", recipe = new[] { MaterialEnum.Fire } });
        var second = new MagicModel(new MagicData { id = "second", recipe = new[] { MaterialEnum.Fire } });
        player.SetMagicAtSlot(first, 0); player.SetMagicAtSlot(second, 2);
        player.SetMagicReadingOrder(new[] { new SlotReadingRank(0, 2), new SlotReadingRank(1, 0), new SlotReadingRank(2, 1) });
        Assert.That(first.ReadingRank, Is.EqualTo(2));
        Assert.That(second.ReadingRank, Is.EqualTo(1));
        Assert.That(player.MagicReadingOrder.SlotSpan, Is.EqualTo(3));
        player.RearrangeMagicSlotsByVisualOrder(0, 2, 3);
        Assert.That(first.SlotIndex, Is.EqualTo(2));
        Assert.That(first.ReadingRank, Is.EqualTo(1), "Reading rank belongs to the destination slot, not the moved entity.");
        var restored = new MagicReadingOrder(); restored.Set(player.MagicReadingOrder.Export());
        Assert.That(restored.GetRank(1), Is.Zero);
        Assert.That(restored.GetRank(8), Is.EqualTo(8));
    }

    [Test]
    public void FixedSlotsKeepHolesAndInsertInVisualOrder()
    {
        var player = new PlayerState(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData());
        var first = new MagicModel(new MagicData { id = "first" });
        var second = new MagicModel(new MagicData { id = "second" });
        var third = new MagicModel(new MagicData { id = "third" });
        player.SetMagicAtSlot(first, 0);
        player.SetMagicAtSlot(second, 3);
        player.SetMagicAtSlot(third, 1);
        player.SetMagicReadingOrder(new[] { new SlotReadingRank(0, 0), new SlotReadingRank(1, 2),
            new SlotReadingRank(2, 1), new SlotReadingRank(3, 3), new SlotReadingRank(4, 4) });
        var commands = new BattleCommandSession(player);
        commands.Execute(BattleCommand.ReorderMagic(3, 2, 5, true));
        Assert.That(player.GetMagicAtSlot(3), Is.Null);
        Assert.That(player.GetMagicAtSlot(2), Is.SameAs(second));
        commands.Execute(BattleCommand.ReorderMagic(0, 1, 5, true));
        Assert.That(player.GetMagicAtSlot(0), Is.SameAs(second));
        Assert.That(player.GetMagicAtSlot(2), Is.SameAs(third));
        Assert.That(player.GetMagicAtSlot(1), Is.SameAs(first));
        Assert.That(player.GetMagicAtSlot(3), Is.Null);
        Assert.That(first.ReadingRank, Is.EqualTo(2));
    }

    [Test]
    public void MatchingWithoutVisualRanksUsesPersistedPolicy()
    {
        var player = new PlayerState(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData());
        var first = new MagicModel(new MagicData { id = "first", recipe = new[] { MaterialEnum.Fire } });
        var second = new MagicModel(new MagicData { id = "second", recipe = new[] { MaterialEnum.Fire } });
        player.SetMagicAtSlot(first, 0); player.SetMagicAtSlot(second, 1);
        player.SetMagicReadingOrder(new[] { new SlotReadingRank(0, 1), new SlotReadingRank(1, 0) });
        var tokens = new[] { new ArrowReadToken(new MaterialModel("one", MaterialEnum.Fire), 0, 0) };
        var matches = new List<int>();
        MagicMatchOrderUtility.CollectMatchedMagicIndices(player.MagicBook, tokens, 0, matches);
        Assert.That(matches, Is.EqualTo(new[] { 1, 0 }));
        IReadOnlyList<int> frozen = player.CaptureMagicReadingRanks(player.MagicBook);
        player.SetMagicReadingOrder(new[] { new SlotReadingRank(0, 0), new SlotReadingRank(1, 1) });
        Assert.That(frozen, Is.EqualTo(new[] { 1, 0 }));
    }

    [Test]
    public void ArrowModifierLayersPersistInOrderAndRunIndependently()
    {
        var catalog = new RuleContentSnapshot().AddNamed(new Dictionary<string, MaterialModifierData>
        {
            { "repeat_arrow", new MaterialModifierData { id = "repeat_arrow", script = nameof(RepeatArrowModifier), stackable = true } },
            { "temporary", new MaterialModifierData { id = "temporary", script = nameof(TemporaryModifier) } }
        }).Freeze();
        var player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData(), catalog, RuleProfilePolicy.Empty);
        using (player.Rules.Enter())
        {
            var card = new MaterialModel("stack", MaterialEnum.Fire);
            var first = new RepeatArrowModifier();
            Assert.That(card.TryAddModifier(first), Is.True);
            Assert.That(card.TryAddModifier(first), Is.False);
            Assert.That(card.TryAddModifier(new TemporaryModifier()), Is.True);
            Assert.That(card.TryAddModifier(new TemporaryModifier()), Is.False);
            Assert.That(card.TryAddModifier(new RepeatArrowModifier()), Is.True);
            Assert.That(card.GetArrowSequenceReplayCount(), Is.EqualTo(2));
            var data = RuleMaterialCodec.Export(card);
            Assert.That(data.modifierIds, Is.EqualTo(new[] { "repeat_arrow", "temporary", "repeat_arrow" }));
            foreach (MaterialModel copy in new[] { RuleMaterialCodec.Create(data), RuleMaterialCodec.CloneForDisplay(card), card.CloneForBattle("battle_copy") })
            {
                Assert.That(RuleMaterialCodec.Export(copy).modifierIds, Is.EqualTo(data.modifierIds));
                Assert.That(copy.modifiers[0], Is.Not.SameAs(copy.modifiers[2]));
                Assert.That(copy.modifiers[0], Is.Not.SameAs(first));
                Assert.That(copy.GetArrowSequenceReplayCount(), Is.EqualTo(2));
            }
        }
    }

    [Test]
    public void InvalidPolicyDoesNotPartlyReplaceCurrentState()
    {
        var policy = new MagicReadingOrder(); policy.Set(new[] { new SlotReadingRank(0, 3) });
        Assert.Throws<System.ArgumentException>(() => policy.Set(new[] { new SlotReadingRank(1, 0), new SlotReadingRank(1, 1) }));
        Assert.That(policy.GetRank(0), Is.EqualTo(3));
        Assert.That(policy.SlotSpan, Is.EqualTo(1));
    }
}
