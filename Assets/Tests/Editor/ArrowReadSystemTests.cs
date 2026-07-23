using System.Collections.Generic;
using NUnit.Framework;

public class ArrowReadSystemTests
{
    [Test]
    public void RepeatArrow_ReplaysFromItsOwnStepWithoutChangingFirstSequenceTokens()
    {
        MaterialModel fire = new MaterialModel("fire", MaterialEnum.Fire);
        fire.AddModifier(new RepeatArrowModifier());
        MaterialModel wind = new MaterialModel("wind", MaterialEnum.Wind);

        ArrowReadSequence sequence = ArrowReadSystem.BuildSequence(new List<MaterialModel> { fire, wind });

        Assert.That(sequence.Steps.Count, Is.EqualTo(2));
        Assert.That(sequence.Tokens.Count, Is.EqualTo(2));
        Assert.That(sequence.ResolveSequences.Count, Is.EqualTo(2));
        Assert.That(sequence.ResolveSequences[1].Steps.Count, Is.EqualTo(2));
        Assert.That(sequence.ResolveSequences[1].Steps[0].SourceCard, Is.SameAs(fire));
        Assert.That(sequence.ResolveSequences[1].Steps[1].SourceCard, Is.SameAs(wind));
        Assert.That(sequence.ResolveSequences[1].Tokens.Count, Is.EqualTo(2));
    }

    [Test]
    public void RepeatArrow_EventRecipeUsesOnlyTheFirstSequenceTokens()
    {
        MaterialModel fire = new MaterialModel("fire", MaterialEnum.Fire);
        fire.AddModifier(new RepeatArrowModifier());
        MaterialModel wind = new MaterialModel("wind", MaterialEnum.Wind);
        ArrowReadSequence sequence = ArrowReadSystem.BuildSequence(new List<MaterialModel> { fire, wind });
        EventModel eventModel = new EventModel(new EventData
        {
            id = "test",
            startNodeId = "start",
            nodes = new[]
            {
                new EventNodeData
                {
                    id = "start",
                    options = new[] { new EventOptionData { id = "match", recipe = "12" } }
                }
            }
        });

        bool matched = eventModel.TryGetMatchedOption(sequence.Tokens, out EventOptionData option);

        Assert.That(matched, Is.True);
        Assert.That(option.id, Is.EqualTo("match"));
    }

    [Test]
    public void RepeatArrow_ReplayDoesNotRepeatAfterReadActions()
    {
        MaterialModel fire = new MaterialModel("fire", MaterialEnum.Fire);
        fire.AddModifier(new RepeatArrowModifier());
        fire.AddModifier(new FragileArrowModifier());

        ArrowReadSequence sequence = ArrowReadSystem.BuildSequence(new List<MaterialModel> { fire });
        ArrowReadStep firstStep = sequence.Steps[0];
        ArrowReadStep replayStep = sequence.ResolveSequences[1].Steps[0];

        Assert.That(firstStep.RemovesSourceAfterRead, Is.True);
        Assert.That(firstStep.AfterReadAction, Is.EqualTo(ArrowReadAfterReadAction.SplitIntoHalfArrowsToDiscard));
        Assert.That(replayStep.RemovesSourceAfterRead, Is.False);
        Assert.That(replayStep.AfterReadAction, Is.EqualTo(ArrowReadAfterReadAction.None));
        Assert.That(replayStep.DirectionChange, Is.EqualTo(ArrowReadDirectionChange.None));
    }
}
